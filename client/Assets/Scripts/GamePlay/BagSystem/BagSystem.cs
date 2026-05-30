using System.Collections.Generic;
using UnityEngine;
using YOTO;
using YFramework.Config;

/// <summary>
/// 背包系统(<see cref="IGameService"/>,由 <see cref="GameProjectBootstrapper"/> 注册到 <see cref="GameContext"/>)。
/// 持有玩家的 2D 网格空间背包 <see cref="GridBag"/>,对外提供放入/移动/旋转/移除/使用等 API。
///
/// **模型**:物品按配表宽高占一片矩形格子,**不堆叠**,可自由拖放、可 90° 旋转(类似暗黑/塔科夫)。
/// **数据来源**:物品定义全部走配表——<c>ConfigManager.itemConfig.Get(id)</c> 读 protobuf 生成的 <see cref="Item"/>
/// (含 Width/Height 字段),不使用 ScriptableObject。
/// **事件**:背包变化桥接到 <see cref="EventMgr"/> 的 <see cref="YOTOEventType.RefreshBagList"/>,UI 据此刷新。
/// **使用逻辑**:消耗品等效果实现 <see cref="IItemUseHandler"/>,以物品 <c>Item.Id</c> 为键调
///   <see cref="RegisterUseHandler"/> 注册。
/// **存档**:背包内容用 PlayerPrefs + JsonUtility 持久化(物品定义不存,只存 实例 id/物品 id/坐标/朝向)。
/// </summary>
public class BagSystem : IGameService
{
    private const string SaveKey = "BAG_SYSTEM_SAVE_V2"; // 空间背包模型,换 key 避免读旧槽位存档
    private const int DefaultGridWidth = 10;
    private const int DefaultGridHeight = 8;

    private ConfigManager config;
    private EventMgr eventMgr;
    private GridBag bag;
    private readonly Dictionary<int, IItemUseHandler> useHandlers = new Dictionary<int, IItemUseHandler>();

    /// <summary>玩家网格背包实例。</summary>
    public GridBag Bag => bag;

    public void Init(GameContext ctx)
    {
        config = ctx.Get<ConfigManager>();
        eventMgr = ctx.Get<EventMgr>();

        bag = new GridBag(DefaultGridWidth, DefaultGridHeight, GetItem);
        bag.OnChanged += OnBagChanged;

        Load();
    }

    public void Shutdown()
    {
        Save();
        if (bag != null) bag.OnChanged -= OnBagChanged;
        useHandlers.Clear();
    }

    // ---------------- 配表查询 ----------------

    /// <summary>根据 id 从 item 配表获取物品定义,未找到返回 null。</summary>
    public Item GetItem(int itemId)
    {
        if (config == null || itemId <= 0) return null;
        return config.itemConfig.Get((uint)itemId);
    }

    /// <summary>读取某物品的类型(配表不存在时返回 <see cref="ItemType.Other"/>)。</summary>
    public ItemType GetItemType(int itemId)
    {
        var cfg = GetItem(itemId);
        return cfg != null ? (ItemType)cfg.Type : ItemType.Other;
    }

    // ---------------- 对外操作 ----------------

    /// <summary>自动找空位放入一个物品(放不下会尝试旋转)。返回实例,背包满返回 null。</summary>
    public PlacedItem AddItem(int itemId)
    {
        if (bag == null) return null;
        if (GetItem(itemId) == null)
        {
            Debug.LogWarning($"[BagSystem] 配表中不存在物品 id: {itemId}");
            return null;
        }
        var placed = bag.TryAddItem(itemId);
        if (placed == null) Debug.Log($"[BagSystem] 背包放不下物品 {itemId}(空间不足)");
        return placed;
    }

    /// <summary>批量放入同种物品,返回成功放入的个数。</summary>
    public int AddItems(int itemId, int count)
    {
        int ok = 0;
        for (int i = 0; i < count; i++)
        {
            if (AddItem(itemId) == null) break;
            ok++;
        }
        return ok;
    }

    /// <summary>在指定锚点+朝向放入一个物品,返回实例或 null。</summary>
    public PlacedItem AddItemAt(int itemId, int x, int y, bool rotated = false) => bag?.TryAddItemAt(itemId, x, y, rotated);

    /// <summary>移动实例到新锚点(朝向不变,UI 拖放调用)。非法位置返回 false。</summary>
    public bool MoveItem(int instanceId, int x, int y) => bag != null && bag.MoveItem(instanceId, x, y);

    /// <summary>原地旋转实例 90°(UI 右键调用)。旋转后放不下返回 false。</summary>
    public bool RotateItem(int instanceId) => bag != null && bag.RotateItem(instanceId);

    /// <summary>某物品(指定朝向)能否放在锚点 (x,y);移动校验时传 ignoreInstance 忽略自身。</summary>
    public bool CanPlace(int itemId, int x, int y, bool rotated, int ignoreInstance = 0)
        => bag != null && bag.CanPlace(itemId, x, y, rotated, ignoreInstance);

    /// <summary>移除实例。</summary>
    public bool RemoveItem(int instanceId) => bag != null && bag.RemoveItem(instanceId);

    public int CountItem(int itemId) => bag != null ? bag.CountItem(itemId) : 0;

    /// <summary>整理(自动旋转 + 紧凑重排)。</summary>
    public void SortBag() => bag?.SortBag();

    /// <summary>丢弃实例(<see cref="ItemType.QuestItem"/> 任务物品不可丢弃)。</summary>
    public bool Discard(int instanceId)
    {
        if (bag == null) return false;
        var item = bag.GetByInstance(instanceId);
        if (item == null) return false;
        if (GetItemType(item.itemId) == ItemType.QuestItem) return false;
        return bag.RemoveItem(instanceId);
    }

    // ---------------- 物品使用 ----------------

    /// <summary>注册某物品 id 的使用逻辑处理器。</summary>
    public void RegisterUseHandler(int itemId, IItemUseHandler handler)
    {
        if (itemId <= 0 || handler == null) return;
        useHandlers[itemId] = handler;
    }

    /// <summary>注销某物品 id 的使用逻辑处理器。</summary>
    public void UnregisterUseHandler(int itemId) => useHandlers.Remove(itemId);

    /// <summary>
    /// 使用某实例:按物品 id 找处理器并调用,成功且为消耗品(Type==Consumable)时移除该实例。返回是否使用成功。
    /// </summary>
    public bool UseItem(int instanceId)
    {
        if (bag == null) return false;
        var item = bag.GetByInstance(instanceId);
        if (item == null) return false;

        var cfg = GetItem(item.itemId);
        if (cfg == null) return false;

        if (!useHandlers.TryGetValue(item.itemId, out var handler))
        {
            Debug.LogWarning($"[BagSystem] 物品 {item.itemId} 没有注册使用处理器。");
            return false;
        }

        var context = new ItemUseContext { Item = cfg, Placed = item };
        if (!handler.OnUse(in context)) return false;

        if ((ItemType)cfg.Type == ItemType.Consumable) bag.RemoveItem(instanceId);
        return true;
    }

    // ---------------- 存档 ----------------

    public void Save()
    {
        if (bag == null) return;
        string json = JsonUtility.ToJson(bag.ToSaveData());
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
    }

    public void Load()
    {
        if (bag == null || !PlayerPrefs.HasKey(SaveKey)) return;
        try
        {
            var data = JsonUtility.FromJson<GridBagSaveData>(PlayerPrefs.GetString(SaveKey));
            bag.LoadFromSaveData(data);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[BagSystem] 读取背包存档失败: {e.Message}");
        }
    }

    // ---------------- 内部 ----------------

    private void OnBagChanged() => eventMgr?.Trigger(YOTOEventType.RefreshBagList);
}
