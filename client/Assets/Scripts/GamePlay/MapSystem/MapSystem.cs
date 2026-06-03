using System;
using System.Collections.Generic;
using YFramework.Config;

namespace YOTO
{
    /// <summary>
    /// 关卡(地图)系统(<see cref="IGameService"/>):读 map 配表(map.xlsx → Map.bytes),记录玩家当前选中关卡 +
    /// 每关历史最高分(随存档槽落盘)。选图界面 <c>MapSelectPanel</c> 列表/选择走这里,HUD 显示关卡名也读这里。
    ///
    /// 解锁规则:配表 <c>unlocked=1</c> 的关(第一关)恒解锁;其余关需「上一关(按 SortPriority 的前一关)的历史最高分
    /// ≥ 本关 <c>unlockScore</c>」才解锁。每局结束由 <see cref="RecordScore"/> 记录本局分到当前关。
    /// </summary>
    public class MapSystem : IGameService
    {
        private const string SaveKey = "MapProgressSave";

        private ConfigManager config;
        private StoreMgr store;
        private ISaveHandle saveHandle;

        private readonly Dictionary<uint, int> bestScores = new(); // mapId -> 历史最高分

        public uint SelectedMapId { get; private set; }

        public void Init(GameContext ctx)
        {
            config = ctx.Get<ConfigManager>();
            store = ctx.Get<StoreMgr>();
            saveHandle = store.Register(SaveKey, Capture, Restore); // 一行接入存档(随槽 Progress)
            saveHandle.Load();
            SelectedMapId = FirstUnlockedId(); // 默认选中排序最靠前的已解锁关卡
        }

        public void Shutdown()
        {
            saveHandle?.Save();
            bestScores.Clear();
            saveHandle = null;
            store = null;
            config = null;
        }

        public Map Get(uint id) => config?.mapConfig.Get(id);

        /// <summary>本关历史最高分(没打过=0)。</summary>
        public int BestScore(uint id) => bestScores.TryGetValue(id, out var v) ? v : 0;

        /// <summary>
        /// 是否解锁:配表强制解锁(unlocked!=0,第一关)→ 是;否则看「上一关历史最高分 ≥ 本关 unlockScore」。
        /// 没有上一关(排第一却没设 unlocked)也视为解锁,避免死锁。
        /// </summary>
        public bool IsUnlocked(Map m)
        {
            if (m == null) return false;
            if (m.Unlocked != 0) return true;

            var prev = PreviousBySort(m);
            if (prev == null) return true;
            return BestScore(prev.Id) >= m.UnlockScore;
        }

        public bool IsUnlocked(uint id) => IsUnlocked(Get(id));

        /// <summary>当前选中关卡名(供 HUD 显示);取不到返回空串。</summary>
        public string SelectedName => Get(SelectedMapId)?.Name ?? "";

        /// <summary>选择关卡(仅已解锁的生效)。</summary>
        public void Select(uint id)
        {
            if (IsUnlocked(Get(id))) SelectedMapId = id;
        }

        /// <summary>每局结束记录本关分数:刷新历史最高并落盘(解锁下一关靠它)。</summary>
        public void RecordScore(uint id, int score)
        {
            if (id == 0 || score <= BestScore(id)) return; // 没提升就不写
            bestScores[id] = score;
            saveHandle?.Save();
        }

        /// <summary>按 SortPriority 找出排在 m 前面、最接近的一关(即"上一关");没有则 null。</summary>
        private Map PreviousBySort(Map m)
        {
            if (config == null) return null;
            Map prev = null;
            foreach (var kv in config.mapConfig.items)
            {
                var c = kv.Value;
                if (c == null || c.SortPriority >= m.SortPriority) continue;
                if (prev == null || c.SortPriority > prev.SortPriority) prev = c;
            }
            return prev;
        }

        private uint FirstUnlockedId()
        {
            if (config == null) return 0;
            uint best = 0;
            int bestSort = int.MaxValue;
            foreach (var kv in config.mapConfig.items)
            {
                if (IsUnlocked(kv.Value) && kv.Value.SortPriority < bestSort)
                {
                    bestSort = kv.Value.SortPriority;
                    best = kv.Key;
                }
            }
            return best;
        }

        // ---------------- 存档 ----------------

        private MapProgressSaveData Capture()
        {
            var data = new MapProgressSaveData();
            foreach (var kv in bestScores)
                data.entries.Add(new MapProgressSaveData.Entry { id = kv.Key, score = kv.Value });
            return data;
        }

        private void Restore(MapProgressSaveData data)
        {
            bestScores.Clear();
            if (data?.entries != null)
                foreach (var e in data.entries)
                    bestScores[e.id] = e.score < 0 ? 0 : e.score;
        }
    }

    /// <summary>关卡进度存档体(每关历史最高分)。JsonUtility 不支持字典,用列表落盘。</summary>
    [Serializable]
    public class MapProgressSaveData
    {
        public List<Entry> entries = new List<Entry>();

        [Serializable]
        public class Entry
        {
            public uint id;
            public int score;
        }
    }
}
