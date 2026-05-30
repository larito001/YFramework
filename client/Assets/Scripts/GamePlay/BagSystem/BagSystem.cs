using System.Collections.Generic;
using UnityEngine;
using YOTO;
using YFramework.Config;

/// <summary>
/// 背包系统（<see cref="IGameService"/>，由 <see cref="GameProjectBootstrapper"/> 注册到 <see cref="GameContext"/>）。
/// 持有玩家背包实例 <see cref="Bag"/>，对外提供加入/移除/使用/整理等 API。
///
/// **数据来源**：物品定义全部走配表——通过 <c>ConfigManager.itemConfig.Get(id)</c> 读 protobuf 生成的 <see cref="Item"/>
/// （外部打表工具从 Excel 生成 Item.proto/ItemConfig.cs/Item.bytes，运行时 Resources 加载 Config/Data/Item.bytes），
/// **不使用 ScriptableObject**。
/// **事件**：背包变化桥接到 <see cref="EventMgr"/> 的 <see cref="YOTOEventType.RefreshBagList"/>，UI 据此刷新。
/// **使用逻辑**：消耗品等效果实现 <see cref="IItemUseHandler"/>，以物品 <c>Item.Id</c> 为键调
///   <see cref="RegisterUseHandler"/> 注册。
/// **存档**：背包内容用 PlayerPrefs + JsonUtility 持久化（物品定义不存，只存 id + 数量）。
/// </summary>
public class BagSystem : IGameService
{
    /// <summary>存档 key。</summary>
    private const string SaveKey = "BAG_SYSTEM_SAVE_V1";
    /// <summary>默认初始容量（槽位数）。</summary>
    private const int DefaultCapacity = 30;

    private ConfigManager config;
    private EventMgr eventMgr;
    private Bag bag;
    private readonly Dictionary<int, IItemUseHandler> useHandlers = new Dictionary<int, IItemUseHandler>();

    /// <summary>玩家背包实例。</summary>
    public Bag Bag => bag;

    public void Init(GameContext ctx)
    {
        config = ctx.Get<ConfigManager>();
        eventMgr = ctx.Get<EventMgr>();

        bag = new Bag(DefaultCapacity, GetItem);
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

    /// <summary>根据 id 从 item 配表获取物品定义，未找到返回 null。</summary>
    public Item GetItem(int itemId)
    {
        if (config == null || itemId <= 0) return null;
        return config.itemConfig.Get((uint)itemId);
    }

    /// <summary>读取某物品的类型（配表不存在时返回 <see cref="ItemType.Other"/>）。</summary>
    public ItemType GetItemType(int itemId)
    {
        var cfg = GetItem(itemId);
        return cfg != null ? (ItemType)cfg.Type : ItemType.Other;
    }

    // ---------------- 对外操作 ----------------

    /// <summary>加入物品，返回未放入的剩余数量（0 表示全部成功；&gt;0 表示背包已满）。</summary>
    public int AddItem(int itemId, int count = 1)
    {
        if (bag == null) return count;
        if (GetItem(itemId) == null)
        {
            Debug.LogWarning($"[BagSystem] 配表中不存在物品 id: {itemId}");
            return count;
        }
        return bag.AddItem(itemId, count);
    }

    /// <summary>移除物品，返回实际移除数量。</summary>
    public int RemoveItem(int itemId, int count = 1) => bag != null ? bag.RemoveItem(itemId, count) : 0;

    public bool HasItem(int itemId, int count = 1) => bag != null && bag.HasItem(itemId, count);
    public int GetItemCount(int itemId) => bag != null ? bag.GetItemCount(itemId) : 0;
    public bool MoveItem(int from, int to) => bag != null && bag.MoveItem(from, to);
    public bool SplitStack(int from, int amount, int target = -1) => bag != null && bag.SplitStack(from, amount, target);
    public void SortBag() => bag?.SortBag();
    public void ExpandCapacity(int extraSlots) => bag?.Expand(extraSlots);

    /// <summary>丢弃指定槽位的若干物品（<see cref="ItemType.QuestItem"/> 任务物品不可丢弃）。</summary>
    public bool DiscardAt(int slot, int count = 1)
    {
        if (bag == null) return false;
        var s = bag.GetSlot(slot);
        if (s == null) return false;
        if (GetItemType(s.itemId) == ItemType.QuestItem) return false;
        return bag.RemoveAt(slot, count) > 0;
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
    /// 使用指定槽位的物品：按物品 id 找处理器并调用，成功且为消耗品（Type==Consumable）时扣除一个。
    /// 返回是否使用成功。
    /// </summary>
    public bool UseItem(int slot)
    {
        if (bag == null) return false;
        var stack = bag.GetSlot(slot);
        if (stack == null) return false;

        var cfg = GetItem(stack.itemId);
        if (cfg == null) return false;

        if (!useHandlers.TryGetValue(stack.itemId, out var handler))
        {
            Debug.LogWarning($"[BagSystem] 物品 {stack.itemId} 没有注册使用处理器。");
            return false;
        }

        var context = new ItemUseContext { Item = cfg, SlotIndex = slot, Amount = 1 };
        if (!handler.OnUse(in context)) return false;

        if ((ItemType)cfg.Type == ItemType.Consumable) bag.RemoveAt(slot, 1);
        return true;
    }

    // ---------------- 存档 ----------------

    /// <summary>保存背包到 PlayerPrefs。</summary>
    public void Save()
    {
        if (bag == null) return;
        string json = JsonUtility.ToJson(bag.ToSaveData());
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
    }

    /// <summary>从 PlayerPrefs 读取背包（无存档则保持空背包）。</summary>
    public void Load()
    {
        if (bag == null || !PlayerPrefs.HasKey(SaveKey)) return;
        try
        {
            var data = JsonUtility.FromJson<BagSaveData>(PlayerPrefs.GetString(SaveKey));
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
