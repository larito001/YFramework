using System;
using UnityEngine;

namespace YOTO
{
    /// <summary>
    /// 「看广告补体力」的每日次数限制(<see cref="IGameService"/>)。记录当天日期 + 已看次数,跨天自动归零。
    /// 走 <see cref="StoreMgr"/>(Progress 分类,随存档槽)落盘 → 本地 + 云(由 <c>CloudSaveSyncService</c> 自动同步),
    /// 与货币/图鉴等一致。主界面 <c>StartPanel</c> 的体力广告按钮据此放行/置灰。
    /// </summary>
    public class DailyAdEnergySystem : IGameService
    {
        private const string SaveKey = "DailyAdEnergySave";

        [Tooltip("每天最多看几次广告补体力")]
        public int DailyLimit = 3;

        private StoreMgr store;
        private ISaveHandle saveHandle;
        private string date = "";  // 最近一次计数的日期(yyyyMMdd)
        private int count;          // 当天已看次数

        public void Init(GameContext ctx)
        {
            store = ctx.Get<StoreMgr>();
            saveHandle = store.Register(SaveKey, Capture, Restore); // 一行接入存档(随槽 Progress → 本地+云)
            saveHandle.Load();
        }

        public void Shutdown()
        {
            saveHandle?.Save();
            saveHandle = null;
            store = null;
        }

        private static string Today => DateTime.Now.ToString("yyyyMMdd");

        /// <summary>今天已看次数(日期变了视为 0)。</summary>
        public int CountToday => date == Today ? count : 0;

        /// <summary>今天还能看几次。</summary>
        public int Remaining => Mathf.Max(0, DailyLimit - CountToday);

        /// <summary>今天是否还能看广告补体力。</summary>
        public bool CanWatch => CountToday < DailyLimit;

        /// <summary>记一次"今天看完广告补了体力"(跨天先归零),并立即落盘(→ 本地+云)。</summary>
        public void Record()
        {
            if (date != Today) { date = Today; count = 0; }
            count++;
            saveHandle?.Save();
        }

        private DailyAdSaveData Capture() => new DailyAdSaveData { date = date, count = count };

        private void Restore(DailyAdSaveData data)
        {
            date = data?.date ?? "";
            count = data != null && data.count > 0 ? data.count : 0;
        }
    }

    /// <summary>每日广告补体力存档体(日期 + 当天次数)。</summary>
    [Serializable]
    public class DailyAdSaveData
    {
        public string date = "";
        public int count;
    }
}
