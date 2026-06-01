using YFramework.Config;

namespace YOTO
{
    /// <summary>
    /// 关卡(地图)系统(<see cref="IGameService"/>):读 map 配表(map.xlsx → Map.bytes),
    /// 记录玩家当前选中的关卡。选图界面 <c>MapSelectPanel</c> 列表/选择走这里,HUD 显示关卡名也读这里。
    /// 解锁与否目前由配表 <c>unlocked</c> 列驱动;接入存档进度后可改为按玩家进度解锁。
    /// </summary>
    public class MapSystem : IGameService
    {
        private ConfigManager config;

        public uint SelectedMapId { get; private set; }

        public void Init(GameContext ctx)
        {
            config = ctx.Get<ConfigManager>();
            SelectedMapId = FirstUnlockedId(); // 默认选中排序最靠前的已解锁关卡
        }

        public void Shutdown() => config = null;

        public Map Get(uint id) => config?.mapConfig.Get(id);

        public bool IsUnlocked(Map m) => m != null && m.Unlocked != 0;

        /// <summary>当前选中关卡名(供 HUD 显示);取不到返回空串。</summary>
        public string SelectedName => Get(SelectedMapId)?.Name ?? "";

        /// <summary>选择关卡(仅已解锁的生效)。</summary>
        public void Select(uint id)
        {
            if (IsUnlocked(Get(id))) SelectedMapId = id;
        }

        private uint FirstUnlockedId()
        {
            if (config == null) return 0;
            uint best = 0;
            int bestSort = int.MaxValue;
            foreach (var kv in config.mapConfig.items)
            {
                if (kv.Value.Unlocked != 0 && kv.Value.SortPriority < bestSort)
                {
                    bestSort = kv.Value.SortPriority;
                    best = kv.Key;
                }
            }
            return best;
        }
    }
}
