using System.Collections.Generic;
using UnityEngine;
using YOTO;
using YFramework.Config;

/// <summary>
/// 背包系统(<see cref="IGameService"/>,由 <see cref="GameProjectBootstrapper"/> 注册到 <see cref="GameContext"/>)。
/// 持有玩家的 2D 网格空间背包 <see cref="GridBag"/>,对外提供放入/移动/交换/旋转/移除/使用等 API。
///
/// **模型**:物品按配表形状(<c>Width/Height/Shape</c>,支持 L/T 等不规则多边形)占一组格子,
/// 可叠加(<c>MaxStack</c>)、可自由拖放、可 4 向旋转、可合并/拆分、拖到他人上可快速交换(类似暗黑/塔科夫)。
/// **数据来源**:全部走配表——<c>ConfigManager.itemConfig.Get(id)</c> 读 protobuf 生成的 <see cref="Item"/>,
/// 不使用 ScriptableObject。
/// **事件**:背包变化桥接到 <see cref="EventMgr"/> 的 <see cref="YOTOEventType.RefreshBagList"/>,UI 据此刷新。
/// **使用逻辑**:消耗品等效果实现 <see cref="IItemUseHandler"/>,以物品 <c>Item.Id</c> 为键调
///   <see cref="RegisterUseHandler"/> 注册。
/// **存档**:走框架 <see cref="StoreMgr"/>(<see cref="StoreMgr.Register{T}"/> 一行注册 + 异步 JSON 文件,只存 实例 id/物品 id/坐标/朝向/数量)。
/// </summary>
public class BagSystem : IGameService
{
    private const int DefaultGridWidth = 10;
    private const int DefaultGridHeight = 8;

    private GameContext ctx;
    private ConfigManager config;
    private EventMgr eventMgr;
    private InputService input;
    private UIMgr uiMgr;
    private StoreMgr store;
    private DropItemSystem dropSystem;
    private ISaveHandle saveHandle;
    private GridBag bag;
    private readonly Dictionary<int, IItemUseHandler> useHandlers = new Dictionary<int, IItemUseHandler>();

    /// <summary>玩家网格背包实例。</summary>
    public GridBag Bag => bag;

    public void Init(GameContext ctx)
    {
        this.ctx = ctx; // 丢弃时懒取 DropItemSystem(注册晚于本系统)
        config = ctx.Get<ConfigManager>();
        eventMgr = ctx.Get<EventMgr>();
        // InputService 在本系统之前注册、UIMgr 框架层先注册,此处可直接取。
        input = ctx.Get<InputService>();
        uiMgr = ctx.Get<UIMgr>();
        store = ctx.Get<StoreMgr>(); // 框架自带存储服务(注册表 + 异步文件读写)
        input.OnToggleBagDown += ToggleBagPanel;

        bag = new GridBag(DefaultGridWidth, DefaultGridHeight, GetItem);
        bag.OnChanged += OnBagChanged;

        // 一行注册即接入存档系统:采集=背包快照,还原=套用到背包(无存档时收到空 data)。
        saveHandle = store.Register("BagSave", () => bag.ToSaveData(), (GridBagSaveData d) => bag.LoadFromSaveData(d));

        Load();
    }

    public void Shutdown()
    {
        Save();
        if (input != null) input.OnToggleBagDown -= ToggleBagPanel;
        if (bag != null) bag.OnChanged -= OnBagChanged;
        useHandlers.Clear();
    }

    /// <summary>B 键开/关背包:已开则关;否则先关宝箱面板(互斥)再开背包。</summary>
    private void ToggleBagPanel()
    {
        if (uiMgr == null) return;
        if (uiMgr.IsShown(UIEnum.BagPanel))
        {
            uiMgr.Hide<BagPanel>();
            return;
        }
        uiMgr.Hide<ChestPanel>(); // 背包与宝箱面板互斥,打开背包前先关宝箱
        uiMgr.Show<BagPanel>();
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

    /// <summary>读取某物品的品质(配表不存在时返回 <see cref="ItemQuality.Common"/>)。</summary>
    public ItemQuality GetItemQuality(int itemId)
    {
        var cfg = GetItem(itemId);
        return cfg != null ? (ItemQuality)cfg.Quality : ItemQuality.Common;
    }

    // ---------------- 对外操作 ----------------

    /// <summary>
    /// 放入若干个物品(可叠加物品自动并堆,其余逐个占格)。返回**未能放入**的剩余数量(0 = 全部放入)。
    /// </summary>
    public int AddItem(int itemId, int count = 1)
    {
        if (bag == null) return count;
        if (GetItem(itemId) == null)
        {
            Debug.LogWarning($"[BagSystem] 配表中不存在物品 id: {itemId}");
            return count;
        }
        int leftover = bag.TryAddItem(itemId, count);
        if (leftover > 0) Debug.Log($"[BagSystem] 背包放不下物品 {itemId} x{leftover}(空间不足)");
        return leftover;
    }

    /// <summary>在指定锚点+朝向放入一堆物品,返回实例或 null。</summary>
    public PlacedItem AddItemAt(int itemId, int x, int y, int rotation = 0, int count = 1) => bag?.TryAddItemAt(itemId, x, y, rotation, count);

    /// <summary>从某堆拆出 amount 个到空位,返回新堆实例或 null(不可叠加/数量非法/无空位)。</summary>
    public PlacedItem SplitStack(int instanceId, int amount) => bag?.SplitStack(instanceId, amount);

    /// <summary>某物品堆叠上限(&lt;=1 为不可叠加)。</summary>
    public int MaxStack(int itemId) => bag != null ? bag.MaxStack(itemId) : 1;

    /// <summary>某物品是否可叠加。</summary>
    public bool IsStackable(int itemId) => bag != null && bag.IsStackable(itemId);

    /// <summary>放置或交换实例到锚点 (x,y)+朝向(UI 拖放调用)。非法返回 false。</summary>
    public bool PlaceOrSwap(int instanceId, int x, int y, int rotation) => bag != null && bag.PlaceOrSwap(instanceId, x, y, rotation);

    /// <summary>旋转实例 90°(顺时针)。放不下返回 false。</summary>
    public bool RotateItem(int instanceId) => bag != null && bag.RotateItem(instanceId);

    /// <summary>某物品(指定朝向)能否放在锚点 (x,y);校验拖放预览时传 ignoreInstance 忽略自身。</summary>
    public bool CanPlace(int itemId, int x, int y, int rotation, int ignoreInstance = 0)
        => bag != null && bag.CanPlace(itemId, x, y, rotation, ignoreInstance);

    /// <summary>移除实例。</summary>
    public bool RemoveItem(int instanceId) => bag != null && bag.RemoveItem(instanceId);

    public int CountItem(int itemId) => bag != null ? bag.CountItem(itemId) : 0;

    /// <summary>整理(各朝向择优 + 紧凑重排)。</summary>
    public void SortBag() => bag?.SortBag();

    /// <summary>
    /// 丢弃实例(<see cref="ItemType.QuestItem"/> 任务物品不可丢弃)。
    /// 成功从背包移除后,经 <see cref="DropItemSystem"/> 在玩家身前生成可拾取的世界掉落物。
    /// </summary>
    public bool Discard(int instanceId)
    {
        if (bag == null) return false;
        var item = bag.GetByInstance(instanceId);
        if (item == null) return false;
        if (GetItemType(item.itemId) == ItemType.QuestItem) return false;

        // 先抓数据(移除后实例失效),再移除,最后丢到世界。
        int itemId = item.itemId, count = item.count, rotation = item.rotation;
        if (!bag.RemoveItem(instanceId)) return false;

        if (dropSystem == null && ctx != null) ctx.TryGet(out dropSystem);
        dropSystem?.DropAtPlayer(itemId, count, rotation);
        return true;
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
    /// 使用某实例:按物品 id 找处理器并调用,成功且为消耗品(Type==Consumable)时**扣 1 个**
    /// (堆叠物品减数量,扣到 0 才移除整堆)。返回是否使用成功。
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

        // 消耗品使用成功只扣 1 个(堆叠物品减数量,扣到 0 才移除整堆),不再整堆删除。
        if ((ItemType)cfg.Type == ItemType.Consumable) bag.ConsumeItem(instanceId, 1);
        return true;
    }

    // ---------------- 存档 ----------------

    /// <summary>保存背包到本地文件(StoreMgr 异步写,JSON)。
    /// 由面板关闭时调用(可靠);Shutdown 也调但仅尽力而为——退出时协程可能来不及跑完。</summary>
    public void Save()
    {
        saveHandle?.Save(); // 采集走注册时的 bag.ToSaveData()
    }

    /// <summary>从本地文件读档(StoreMgr 异步读)。读到后由注册的还原回调套用到背包;无存档则背包保持空。</summary>
    public void Load()
    {
        saveHandle?.Load(); // 还原走注册时的 bag.LoadFromSaveData(d)
    }

    // ---------------- 内部 ----------------

    private void OnBagChanged() => eventMgr?.Trigger(YOTOEventType.RefreshBagList);
}
