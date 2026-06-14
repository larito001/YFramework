using System.Collections.Generic;
using UnityEngine;
using YFramework.Config;

/// <summary>
/// 宝箱系统(<see cref="IGameService"/>,由 <see cref="GameProjectBootstrapper"/> 注册)。
/// 读 chest / chestDrop 两张配表,提供「随机生成宝箱内容」工厂 + 「当前打开的宝箱」引用(供 <see cref="ChestPanel"/> 渲染)。
///
/// **刷新策略**:宝箱首次打开 <see cref="Roll"/> 一次,生成的 <see cref="GridBag"/> 由各宝箱世界 actor
/// (<see cref="ChestActor"/>.RolledGrid)自己持有、整局保持(玩家拿走/放入都留在该实例上);
/// **不持久化到磁盘**,游戏重开即丢失 → 重新 roll。本系统只负责生成与「当前引用」,不缓存各实例内容。
///
/// **生成规则**:<c>Chest.GroupSeq[i]</c> 这组掉落抽 <c>Chest.RollSeq[i]</c> 次(平行数组,i 为开启次序);
/// 因当前是 roll-once,通常只用到 i=0。每次抽取在该组 chestDrop 池里**加权**选 1 条,按 [minCount,maxCount] 随机数量放入。
/// 宝箱网格与玩家背包共用物品配置(<see cref="BagSystem.GetItem"/>),故背包↔宝箱可 <see cref="GridBag.Transfer"/> 互拖。
/// </summary>
public class ChestSystem : IGameService
{
    private ConfigManager config;
    private BagSystem bagSystem;

    /// <summary>groupId → 该组掉落条目列表(Init 时从 chestDrop 配表建索引)。</summary>
    private readonly Dictionary<uint, List<ChestDrop>> groupIndex = new Dictionary<uint, List<ChestDrop>>();

    /// <summary>当前打开的宝箱网格(未打开为 null)。ChestPanel 渲染它。</summary>
    public GridBag Current { get; private set; }
    /// <summary>当前打开的宝箱配置。</summary>
    public Chest CurrentConfig { get; private set; }

    public void Init(GameContext ctx)
    {
        config = ctx.Get<ConfigManager>();
        bagSystem = ctx.Get<BagSystem>();
        BuildGroupIndex();
    }

    public void Shutdown()
    {
        groupIndex.Clear();
        Current = null;
        CurrentConfig = null;
    }

    private void BuildGroupIndex()
    {
        groupIndex.Clear();
        var all = config.chestDropConfig.items; // Dictionary<uint, ChestDrop>
        if (all == null) return;
        foreach (var kv in all)
        {
            var entry = kv.Value;
            if (entry == null) continue;
            if (!groupIndex.TryGetValue(entry.GroupId, out var list))
            {
                list = new List<ChestDrop>();
                groupIndex[entry.GroupId] = list;
            }
            list.Add(entry);
        }
    }

    /// <summary>取宝箱配置,未找到返回 null。</summary>
    public Chest GetChestConfig(int chestId) => config?.chestConfig.Get((uint)chestId);

    /// <summary>
    /// 工厂:按宝箱配置 + 开启次序 <paramref name="openIndex"/> 随机生成一个**新**宝箱网格并返回。
    /// 不缓存、不设为 Current —— 由调用方(<see cref="ChestView"/>,存到 <see cref="ChestActor"/>.RolledGrid)持有以实现整局保持。
    /// </summary>
    public GridBag Roll(int chestId, int openIndex)
    {
        var cfg = GetChestConfig(chestId);
        if (cfg == null)
        {
            Debug.LogWarning($"[ChestSystem] 配表中不存在宝箱 id: {chestId}");
            return null;
        }

        var grid = new GridBag(cfg.Width, cfg.Height, bagSystem.GetItem);
        if (cfg.GroupSeq != null && cfg.GroupSeq.Count > 0)
        {
            int idx = Mathf.Clamp(openIndex, 0, cfg.GroupSeq.Count - 1);
            uint groupId = cfg.GroupSeq[idx];
            int rolls = (cfg.RollSeq != null && cfg.RollSeq.Count > 0)
                ? cfg.RollSeq[Mathf.Min(idx, cfg.RollSeq.Count - 1)]
                : 1;
            RollInto(grid, groupId, rolls);
        }
        return grid;
    }

    /// <summary>把某宝箱网格设为当前(ChestPanel 打开前调)。</summary>
    public void SetCurrent(GridBag grid, int chestId)
    {
        Current = grid;
        CurrentConfig = GetChestConfig(chestId);
    }

    /// <summary>关闭宝箱:清当前引用(内容仍由宝箱实例持有,不丢)。</summary>
    public void CloseChest()
    {
        Current = null;
        CurrentConfig = null;
    }

    /// <summary>从某组加权抽取 rolls 次,放入网格(放不下的条目自动跳过)。</summary>
    private void RollInto(GridBag grid, uint groupId, int rolls)
    {
        if (!groupIndex.TryGetValue(groupId, out var pool) || pool.Count == 0) return;

        int totalWeight = 0;
        for (int i = 0; i < pool.Count; i++)
            totalWeight += Mathf.Max(0, pool[i].Weight);
        if (totalWeight <= 0) return;

        for (int r = 0; r < rolls; r++)
        {
            var entry = WeightedPick(pool, totalWeight);
            if (entry == null) continue;
            int min = Mathf.Max(1, entry.MinCount);
            int max = Mathf.Max(min, entry.MaxCount);
            int count = Random.Range(min, max + 1);
            grid.TryAddItem((int)entry.ItemId, count);
        }
    }

    private static ChestDrop WeightedPick(List<ChestDrop> pool, int totalWeight)
    {
        int roll = Random.Range(0, totalWeight); // [0,totalWeight)
        int acc = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            acc += Mathf.Max(0, pool[i].Weight);
            if (roll < acc) return pool[i];
        }
        return pool[pool.Count - 1];
    }
}
