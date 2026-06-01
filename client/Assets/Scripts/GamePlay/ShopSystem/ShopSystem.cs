using System.Collections.Generic;
using UnityEngine;
using YFramework.Config;

namespace YOTO
{
    /// <summary>
    /// 商店系统(<see cref="IGameService"/>,由 <see cref="GameProjectBootstrapper"/> 注册)。
    /// 目录直接读 item 配表:凡 <see cref="Item.Price"/> &gt; 0 的物品即上架,售价与货币种类来自配表
    /// (<see cref="Item.Price"/> + <see cref="Item.PriceType"/> → <see cref="CurrencyType"/>),按 sortPriority 降序排列。
    ///
    /// **购买**:<see cref="Buy"/> 扣货币(<see cref="CurrencySystem.TrySpend"/>)→ 放背包(<see cref="BagSystem.AddItem"/>);
    /// 背包放不下的部分按比例退款。货币/背包变化各自触发 RefreshCurrency / RefreshBagList 事件,UI 据此刷新。
    /// 不持有自己的存档——余额在 <see cref="CurrencySystem"/>,物品在 <see cref="BagSystem"/>,商店只做撮合。
    /// </summary>
    public class ShopSystem : IGameService
    {
        private ConfigManager config;
        private CurrencySystem currency;
        private BagSystem bag;
        private LoadoutSystem loadout;

        private readonly List<Item> catalog = new();

        /// <summary>全部在售物品(price&gt;0,按 sortPriority 降序)。</summary>
        public IReadOnlyList<Item> Catalog => catalog;

        /// <summary>某分类页签下的在售物品(price&gt;0 且 shopCategory 匹配,保持 sortPriority 顺序)。</summary>
        public List<Item> CatalogOf(ShopCategory category)
        {
            var list = new List<Item>();
            for (int i = 0; i < catalog.Count; i++)
            {
                if ((ShopCategory)catalog[i].ShopCategory == category) list.Add(catalog[i]);
            }
            return list;
        }

        public void Init(GameContext ctx)
        {
            config = ctx.Get<ConfigManager>();
            currency = ctx.Get<CurrencySystem>();
            bag = ctx.Get<BagSystem>();
            loadout = ctx.Get<LoadoutSystem>();
            BuildCatalog();
        }

        public void Shutdown()
        {
            catalog.Clear();
            config = null;
            currency = null;
            bag = null;
            loadout = null;
        }

        private void BuildCatalog()
        {
            catalog.Clear();
            var all = config?.itemConfig.items; // 触发懒加载
            if (all == null) return;
            foreach (var kv in all)
            {
                var it = kv.Value;
                if (it != null && it.Price > 0) catalog.Add(it);
            }
            catalog.Sort((a, b) => b.SortPriority.CompareTo(a.SortPriority));
        }

        // ---------------- 价格 / 可购买性 ----------------

        /// <summary>某物品的计价货币。</summary>
        public CurrencyType CurrencyOf(Item item) => (CurrencyType)item.PriceType;

        /// <summary>买 count 个的总价(count 至少按 1 计)。</summary>
        public long PriceOf(Item item, int count = 1) => (long)item.Price * Mathf.Max(1, count);

        /// <summary>是否买得起(物品在售且对应货币足够)。</summary>
        public bool CanAfford(Item item, int count = 1)
            => item != null && item.Price > 0 && currency.Has((CurrencyType)item.PriceType, PriceOf(item, count));

        // ---------------- 购买 ----------------

        /// <summary>
        /// 购买。分类商品(枪械/瞄准镜/子弹)为一次性**装备解锁**:已拥有则不再出售,买成功后 <see cref="LoadoutSystem.Grant"/>;
        /// 其余商品扣货币后放背包,背包放不下的部分按单价退款。货币不足直接失败。返回是否购买成功。
        /// </summary>
        public bool Buy(int itemId, int count = 1)
        {
            count = Mathf.Max(1, count);
            var item = config?.itemConfig.Get((uint)itemId);
            if (item == null || item.Price <= 0)
            {
                Debug.LogWarning($"[ShopSystem] 物品不可购买: id={itemId}");
                return false;
            }

            bool isEquip = item.ShopCategory != 0;
            if (isEquip && loadout != null && loadout.IsOwned(itemId))
            {
                Debug.Log($"[ShopSystem] 已拥有该装备,无需重复购买: id={itemId}");
                return false;
            }

            var type = (CurrencyType)item.PriceType;
            // 装备按解锁价(单件),其余按数量计价
            long cost = isEquip ? item.Price : PriceOf(item, count);
            if (!currency.TrySpend(type, cost))
            {
                Debug.Log($"[ShopSystem] {type} 不足,购买失败: id={itemId}(需 {cost})");
                return false;
            }

            if (isEquip)
            {
                loadout?.Grant(itemId); // 解锁装备(进入 LoadoutSystem,不占空间背包)
                PersistPurchase(true);  // 购买完成即写盘:货币 + 装备
                return true;
            }

            int leftover = bag.AddItem(itemId, count); // 触发 RefreshBagList
            if (leftover > 0)
            {
                long refund = (long)item.Price * leftover; // 放不下的部分退款
                currency.Add(type, refund);
                int placed = count - leftover;
                Debug.Log($"[ShopSystem] 背包空间不足,实际购入 {placed} 个,退款 {refund} {type}");
                if (placed > 0) PersistPurchase(false); // 实际购入才写盘(全退则净额未变,无需落盘)
                return placed > 0;
            }
            PersistPurchase(false); // 购买完成即写盘:货币 + 背包
            return true;
        }

        /// <summary>购买完成后写入相应进度:货币必写,装备解锁写 Loadout、入包写 Bag。各 Save 走 StoreMgr 异步落到当前激活槽。</summary>
        private void PersistPurchase(bool isEquip)
        {
            currency?.Save();
            if (isEquip) loadout?.Save();
            else bag?.Save();
        }
    }
}
