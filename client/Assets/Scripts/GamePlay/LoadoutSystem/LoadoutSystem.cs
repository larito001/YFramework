using System;
using System.Collections.Generic;
using YFramework.Config;

namespace YOTO
{
    /// <summary>
    /// 装备/出战配置系统(<see cref="IGameService"/>,由 <see cref="GameProjectBootstrapper"/> 注册)。
    /// 管理玩家**已拥有的装备**(枪械 / 瞄准镜 / 子弹,对应 <see cref="ShopCategory"/>)与**每个分类当前选中**的一件。
    /// 装备目录来自 item 配表(shopCategory 列);拥有关系独立成集合,不占空间背包(背包只放消耗品/战利品)。
    ///
    /// **解锁**:商店购买分类商品时调 <see cref="Grant"/>;<see cref="Select"/> 在已拥有的同类装备间切换出战项。
    /// 变化触发 <see cref="YOTOEventType.RefreshLoadout"/>,装备界面据此刷新绿/灰与选中高亮。
    /// **存档**:走 <see cref="StoreMgr"/>,<see cref="SaveCategory.Progress"/> 随槽隔离。新档自动发放每类第一件(种子),保证可出战。
    /// </summary>
    public class LoadoutSystem : IGameService
    {
        private const string SaveKey = "LoadoutSave";

        private ConfigManager config;
        private StoreMgr store;
        private EventMgr eventMgr;
        private ISaveHandle saveHandle;

        private readonly HashSet<int> owned = new();          // 已拥有装备的物品 id
        private readonly Dictionary<int, int> selected = new(); // 分类(int) -> 选中的物品 id

        public void Init(GameContext ctx)
        {
            config = ctx.Get<ConfigManager>();
            store = ctx.Get<StoreMgr>();
            eventMgr = ctx.Get<EventMgr>();
            saveHandle = store.Register(SaveKey, Capture, Restore);
            saveHandle.Load();
        }

        public void Shutdown()
        {
            Save();
            owned.Clear();
            selected.Clear();
            config = null;
            store = null;
            eventMgr = null;
            saveHandle = null;
        }

        public void Save() => saveHandle?.Save();
        public void Load() => saveHandle?.Load();

        // ---------------- 查询 ----------------

        /// <summary>某分类下的全部装备(item 配表 shopCategory 匹配),按 sortPriority 降序。供装备界面铺卡。</summary>
        public List<Item> CategoryItems(ShopCategory cat)
        {
            var list = new List<Item>();
            var all = config?.itemConfig.items;
            if (all != null)
            {
                foreach (var kv in all)
                {
                    if (kv.Value != null && (ShopCategory)kv.Value.ShopCategory == cat) list.Add(kv.Value);
                }
            }
            list.Sort((a, b) => b.SortPriority.CompareTo(a.SortPriority));
            return list;
        }

        public bool IsOwned(int itemId) => owned.Contains(itemId);

        public int GetSelected(ShopCategory cat) => selected.TryGetValue((int)cat, out var v) ? v : 0;

        // ---------------- 操作(变化即触发 RefreshLoadout) ----------------

        /// <summary>解锁一件装备(购买/奖励)。已拥有则忽略。该分类还没选中时自动选上。</summary>
        public void Grant(int itemId)
        {
            if (itemId <= 0 || !owned.Add(itemId)) return;
            var item = config?.itemConfig.Get((uint)itemId);
            if (item != null && item.ShopCategory != 0 && GetSelected((ShopCategory)item.ShopCategory) == 0)
                selected[(int)item.ShopCategory] = itemId;
            eventMgr?.Trigger(YOTOEventType.RefreshLoadout);
            Save(); // 立即落盘:出发时 LoadAll 会从磁盘重载装备,不存就会被旧存档覆盖
        }

        /// <summary>选中某装备出战(须已拥有且分类匹配)。成功返回 true。</summary>
        public bool Select(ShopCategory cat, int itemId)
        {
            if (!owned.Contains(itemId)) return false;
            var item = config?.itemConfig.Get((uint)itemId);
            if (item == null || (ShopCategory)item.ShopCategory != cat) return false;
            selected[(int)cat] = itemId;
            eventMgr?.Trigger(YOTOEventType.RefreshLoadout);
            Save(); // 立即落盘:否则出发时 LoadAll 重载装备会把刚选的出战项覆盖回旧值(表现为玩法里武器不变)
            return true;
        }

        // ---------------- 存档 ----------------

        private LoadoutSaveData Capture()
        {
            var d = new LoadoutSaveData();
            foreach (var id in owned) d.owned.Add(id);
            foreach (var kv in selected) d.selected.Add(new LoadoutSaveData.Sel { category = kv.Key, itemId = kv.Value });
            return d;
        }

        private void Restore(LoadoutSaveData d)
        {
            owned.Clear();
            selected.Clear();
            if (d?.owned != null) foreach (var id in d.owned) owned.Add(id);
            if (d?.selected != null) foreach (var s in d.selected) selected[s.category] = s.itemId;
            if (owned.Count == 0) SeedStarter(); // 新档:发放每类第一件,保证装备界面可用
            eventMgr?.Trigger(YOTOEventType.RefreshLoadout);
        }

        private static readonly ShopCategory[] AllCategories = { ShopCategory.Weapon, ShopCategory.Scope, ShopCategory.Bullet };

        private void SeedStarter()
        {
            for (int i = 0; i < AllCategories.Length; i++)
            {
                var items = CategoryItems(AllCategories[i]);
                if (items.Count == 0) continue;
                int id = (int)items[0].Id;
                owned.Add(id);
                selected[(int)AllCategories[i]] = id;
            }
        }
    }

    /// <summary>装备存档体(JsonUtility 不支持字典/集合,用列表落盘)。</summary>
    [Serializable]
    public class LoadoutSaveData
    {
        public List<int> owned = new();
        public List<Sel> selected = new();

        [Serializable]
        public class Sel
        {
            public int category;
            public int itemId;
        }
    }
}
