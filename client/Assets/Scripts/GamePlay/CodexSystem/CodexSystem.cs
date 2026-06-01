using System;
using System.Collections.Generic;

namespace YOTO
{
    /// <summary>
    /// 动物图鉴系统(<see cref="IGameService"/>,由 <see cref="GameProjectBootstrapper"/> 注册)。
    /// 记录玩家**已击杀(发现)的动物 id**——被杀死的动物即解锁图鉴条目。图鉴目录读 animal 配表,
    /// 这里只存"哪些已发现"。变化触发 <see cref="YOTOEventType.RefreshCodex"/>,图鉴界面据此刷新已解锁/未解锁。
    /// **存档**:走 <see cref="StoreMgr"/>,<see cref="SaveCategory.Progress"/> 随存档槽隔离。
    /// </summary>
    public class CodexSystem : IGameService
    {
        private const string SaveKey = "CodexSave";

        private StoreMgr store;
        private EventMgr eventMgr;
        private ISaveHandle saveHandle;

        private readonly HashSet<int> discovered = new(); // 已发现(击杀过)的动物 id

        public void Init(GameContext ctx)
        {
            store = ctx.Get<StoreMgr>();
            eventMgr = ctx.Get<EventMgr>();
            saveHandle = store.Register(SaveKey, Capture, Restore);
            saveHandle.Load();
        }

        public void Shutdown()
        {
            Save();
            discovered.Clear();
            store = null;
            eventMgr = null;
            saveHandle = null;
        }

        public void Save() => saveHandle?.Save();
        public void Load() => saveHandle?.Load();

        // ---------------- 查询 ----------------

        public bool IsDiscovered(int animalId) => discovered.Contains(animalId);
        public int DiscoveredCount => discovered.Count;

        // ---------------- 操作 ----------------

        /// <summary>击杀/发现一种动物:首次加入图鉴并落盘,触发 <see cref="YOTOEventType.RefreshCodex"/>。</summary>
        public void Discover(int animalId)
        {
            if (animalId <= 0 || !discovered.Add(animalId)) return;
            eventMgr?.Trigger(YOTOEventType.RefreshCodex);
            Save();
        }

        // ---------------- 存档 ----------------

        private CodexSaveData Capture()
        {
            var d = new CodexSaveData();
            foreach (var id in discovered) d.discovered.Add(id);
            return d;
        }

        private void Restore(CodexSaveData d)
        {
            discovered.Clear();
            if (d?.discovered != null) foreach (var id in d.discovered) discovered.Add(id);
            eventMgr?.Trigger(YOTOEventType.RefreshCodex);
        }
    }

    /// <summary>图鉴存档体(JsonUtility 不支持集合,用列表落盘)。</summary>
    [Serializable]
    public class CodexSaveData
    {
        public List<int> discovered = new();
    }
}
