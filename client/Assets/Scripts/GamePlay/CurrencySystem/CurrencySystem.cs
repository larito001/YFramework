using System;
using System.Collections.Generic;
using UnityEngine;
using YFramework.Config;

namespace YOTO
{
    /// <summary>
    /// 独立货币系统(<see cref="IGameService"/>,由 <see cref="GameProjectBootstrapper"/> 注册到 <see cref="GameContext"/>)。
    /// 一个钱包持有多种货币(<see cref="CurrencyType"/>),与背包/物品解耦——货币不占背包格子,单独记账与存档。
    ///
    /// **API 约定**(见框架风格):操作同步、fire-and-forget;余额变化即触发 <see cref="YOTOEventType.RefreshCurrency"/>,
    /// UI 收到事件后重新 <see cref="Get"/> 各币种刷新显示。增加走 <see cref="Add"/>,扣减走 <see cref="TrySpend"/>(不足返回 false)。
    /// **存档**:走框架 <see cref="StoreMgr"/>,按 <see cref="SaveCategory.Progress"/> 随存档槽隔离(开新游戏会清),
    /// 一行注册 + 异步 JSON 文件,只存 币种 id/数量。
    /// </summary>
    public class CurrencySystem : IGameService
    {
        private const string SaveKey = "CurrencySave";

        private EventMgr eventMgr;
        private StoreMgr store;
        private ConfigManager config;
        private ISaveHandle saveHandle;

        // 余额表:币种 -> 数量。缺省视为 0,不为某币种显式建项也能查/加。
        private readonly Dictionary<CurrencyType, long> balances = new();

        public void Init(GameContext ctx)
        {
            eventMgr = ctx.Get<EventMgr>();
            store = ctx.Get<StoreMgr>();
            config = ctx.Get<ConfigManager>();

            // 一行接入存档:采集=钱包快照,还原=套用到钱包(无存档时收到空 data → 钱包清零)。
            saveHandle = store.Register(SaveKey, Capture, Restore);
            saveHandle.Load();
        }

        public void Shutdown()
        {
            Save(); // 尽力而为:退出时协程可能来不及跑完,关键节点应主动调 Save
            balances.Clear();
            saveHandle = null;
            eventMgr = null;
            store = null;
            config = null;
        }

        // ---------------- 查询 ----------------

        /// <summary>某币种当前数量(未持有返回 0)。</summary>
        public long Get(CurrencyType type) => balances.TryGetValue(type, out var v) ? v : 0;

        /// <summary>币种显示名(来自 currency 配表;取不到回退枚举名)。</summary>
        public string DisplayName(CurrencyType type)
        {
            var c = config?.currencyConfig.Get((uint)type);
            return c != null && !string.IsNullOrEmpty(c.Name) ? c.Name : type.ToString();
        }

        /// <summary>币种图标路径(来自 currency 配表 iconPath;取不到回退空串)。</summary>
        public string IconPath(CurrencyType type)
        {
            var c = config?.currencyConfig.Get((uint)type);
            return c != null ? c.IconPath : string.Empty;
        }

        /// <summary>某币种是否够 <paramref name="amount"/>(amount &lt;= 0 恒为 true)。</summary>
        public bool Has(CurrencyType type, long amount) => amount <= 0 || Get(type) >= amount;

        // ---------------- 操作(同步,变化即触发刷新事件) ----------------

        /// <summary>增加某币种(amount 必须为正;扣减请用 <see cref="TrySpend"/>)。</summary>
        public void Add(CurrencyType type, long amount)
        {
            if (amount <= 0)
            {
                if (amount < 0) Debug.LogWarning($"[CurrencySystem] Add 仅用于增加,扣减请用 TrySpend。type={type}, amount={amount}");
                return;
            }
            SetInternal(type, Get(type) + amount);
        }

        /// <summary>尝试扣减某币种:足够则扣并返回 true;不足(或 amount 非法)不动余额返回 false。</summary>
        public bool TrySpend(CurrencyType type, long amount)
        {
            if (amount < 0) return false;
            if (amount == 0) return true;
            long cur = Get(type);
            if (cur < amount) return false;
            SetInternal(type, cur - amount);
            return true;
        }

        /// <summary>直接设置某币种数量(负数夹到 0)。用于发奖/GM/读档后校正等场景。</summary>
        public void Set(CurrencyType type, long amount) => SetInternal(type, amount < 0 ? 0 : amount);

        // ---------------- 存档 ----------------

        /// <summary>写盘(StoreMgr 异步,JSON)。关键节点(商店购买/关卡结算等)应主动调用。</summary>
        public void Save() => saveHandle?.Save();

        /// <summary>读盘并还原(StoreMgr 异步)。无存档则钱包清零。</summary>
        public void Load() => saveHandle?.Load();

        // ---------------- 内部 ----------------

        private void SetInternal(CurrencyType type, long amount)
        {
            balances[type] = amount;
            eventMgr?.Trigger(YOTOEventType.RefreshCurrency);
        }

        private CurrencySaveData Capture()
        {
            var data = new CurrencySaveData();
            foreach (var kv in balances)
            {
                data.entries.Add(new CurrencySaveData.Entry { type = (int)kv.Key, amount = kv.Value });
            }
            return data;
        }

        private void Restore(CurrencySaveData data)
        {
            balances.Clear();
            if (data?.entries != null)
            {
                foreach (var e in data.entries)
                {
                    balances[(CurrencyType)e.type] = e.amount < 0 ? 0 : e.amount;
                }
            }
            if (balances.Count == 0) SeedStarter(); // 新档发放初始资源,方便上手/测试商店
            eventMgr?.Trigger(YOTOEventType.RefreshCurrency);
        }

        /// <summary>新档初始资源。仅在无任何存档余额(全新槽)时发放一次;数值后续可调或改为运营发放。</summary>
        private void SeedStarter()
        {
            balances[CurrencyType.Gold] = 1000;
            balances[CurrencyType.Energy] = 50; // 体力:每次进图消耗 1 点
        }
    }

    /// <summary>钱包存档体。JsonUtility 不支持字典,故用列表落盘(币种 id + 数量)。</summary>
    [Serializable]
    public class CurrencySaveData
    {
        public List<Entry> entries = new List<Entry>();

        [Serializable]
        public class Entry
        {
            public int type;
            public long amount;
        }
    }
}
