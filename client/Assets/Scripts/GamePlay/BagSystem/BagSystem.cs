using System.Collections.Generic;
using UnityEngine;
using YOTO;
using YFramework.Config;

/// <summary>
/// 背包系统(简化版,<see cref="IGameService"/>,由 <see cref="GameProjectBootstrapper"/> 注册)。
/// 现在只是一个扁平的"物品 id → 数量"存储:供商店购买、任务/奖励发放的物品落袋并随存档持久化。
/// 物品定义走配表(<c>ConfigManager.itemConfig</c>),存档走框架 <see cref="StoreMgr"/>(键 "BagSave")。
///
/// 注:原先的网格空间背包(拖放/旋转/合并/拆分)、物品使用(IItemUseHandler)、世界拾取
/// (WorldInteractionSystem)以及背包 UI 已全部移除,仅保留"存物品 + 取配表"的基本能力。
/// </summary>
public class BagSystem : IGameService
{
    private ConfigManager config;
    private StoreMgr store;
    private ISaveHandle saveHandle;
    private readonly Dictionary<int, int> items = new(); // itemId -> 持有数量

    public void Init(GameContext ctx)
    {
        config = ctx.Get<ConfigManager>();
        store = ctx.Get<StoreMgr>(); // 框架自带存储服务(注册表 + 异步文件读写)
        // 一行注册即接入存档系统:采集=物品快照,还原=套用(无存档时收到 new BagSaveData())。
        saveHandle = store.Register("BagSave", Capture, Restore);
        Load();
    }

    public void Shutdown()
    {
        Save();
        items.Clear();
        config = null;
        store = null;
        saveHandle = null;
    }

    /// <summary>根据 id 从 item 配表获取物品定义,未找到返回 null。</summary>
    public Item GetItem(int itemId)
    {
        if (config == null || itemId <= 0) return null;
        return config.itemConfig.Get((uint)itemId);
    }

    /// <summary>放入若干个物品(同 id 累加)。返回**未能放入**的剩余数量:本简化版无容量上限,恒为 0;
    /// 配表中不存在该物品时原数返回(不放入)。</summary>
    public int AddItem(int itemId, int count = 1)
    {
        if (count <= 0) return 0;
        if (GetItem(itemId) == null)
        {
            Debug.LogWarning($"[BagSystem] 配表中不存在物品 id: {itemId}");
            return count;
        }
        items.TryGetValue(itemId, out var cur);
        items[itemId] = cur + count;
        return 0;
    }

    /// <summary>某物品当前持有数量(没有则 0)。</summary>
    public int CountItem(int itemId)
    {
        items.TryGetValue(itemId, out var c);
        return c;
    }

    // ---------------- 存档 ----------------

    /// <summary>保存背包到本地文件(StoreMgr 异步写,JSON)。</summary>
    public void Save() => saveHandle?.Save();

    /// <summary>从本地文件读档(StoreMgr 异步读);无存档则背包保持空。</summary>
    public void Load() => saveHandle?.Load();

    private BagSaveData Capture()
    {
        var data = new BagSaveData();
        foreach (var kv in items)
            if (kv.Value > 0) data.entries.Add(new BagSaveData.Entry { id = kv.Key, count = kv.Value });
        return data;
    }

    private void Restore(BagSaveData d)
    {
        items.Clear();
        if (d?.entries == null) return;
        foreach (var e in d.entries)
            if (e.count > 0 && GetItem(e.id) != null) items[e.id] = e.count;
    }
}

/// <summary>背包存档数据(扁平 id→数量列表;StoreMgr 走 JSON,故需 [Serializable] + 无参构造)。</summary>
[System.Serializable]
public class BagSaveData
{
    [System.Serializable]
    public struct Entry { public int id; public int count; }

    public List<Entry> entries = new();
}
