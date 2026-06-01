using System;
using System.Collections.Generic;
using UnityEngine;
using YFramework.Config;

namespace YOTO
{
    /// <summary>
    /// 任务进度来源,对应 task 配表的 <c>objType</c> 列。配表里每条任务标一个类型,
    /// 进度由本系统按类型自动累计/计算,无需为每条任务写专门逻辑。
    /// </summary>
    public enum TaskObjType
    {
        None = 0,
        DailyLogin = 1,        // 每日登录:当天登录即完成(每日重置)
        ConsecutiveLogin = 2,  // 连续登录:进度=当前连登天数
        WatchAd = 3,           // 观看广告:每看完一次激励广告 +1
        KillAny = 4,           // 击杀任意动物:每击杀一只 +1
        AcquireWeapon = 5,     // 获得任意武器:拥有任一武器即完成
    }

    /// <summary>
    /// 任务进度系统(<see cref="IGameService"/>)。任务"定义"(名称/目标/奖励/类型)走 task 配表;本系统只管"玩家进度":
    /// 各任务当前进度 + 是否已领取,以及登录/连登/每日重置簿记。进度随存档槽落盘(<see cref="StoreMgr"/> Progress 分类)。
    ///
    /// 进度来源:每日/连续登录在 <see cref="RegisterLogin"/>(启动进大厅时调一次)里结算;击杀走 <see cref="AddKill"/>;
    /// 看广告走 <see cref="AddAdWatch"/>;获得武器监听 <see cref="YOTOEventType.RefreshLoadout"/>。
    /// 完成后由 UI 调 <see cref="Claim"/> 发奖(金币/体力/物品)。任何变化触发 <see cref="YOTOEventType.RefreshTask"/> 让任务界面刷新。
    /// </summary>
    public class TaskProgressSystem : IGameService
    {
        private const string SaveKey = "TaskProgressSave";
        private const uint CategoryDaily = 1; // 与 task 配表 category 约定一致(1=每日)

        private ConfigManager config;
        private CurrencySystem currency;
        private BagSystem bag;
        private LoadoutSystem loadout;
        private EventMgr eventMgr;
        private StoreMgr store;
        private ISaveHandle saveHandle;

        private class State { public int progress; public bool claimed; }
        private readonly Dictionary<uint, State> states = new();

        // 登录/连登/每日重置簿记(按 UTC 天序号,= Unix 秒 / 86400)
        private long lastResetDay;
        private long lastLoginDay;
        private int loginStreak;

        public void Init(GameContext ctx)
        {
            config = ctx.Get<ConfigManager>();
            currency = ctx.Get<CurrencySystem>();
            bag = ctx.Get<BagSystem>();
            loadout = ctx.Get<LoadoutSystem>();
            eventMgr = ctx.Get<EventMgr>();
            store = ctx.Get<StoreMgr>();

            // 随存档槽存档(与 CurrencySystem 同套路)。读档由启动流程的 LoadAll 统一触发 → Restore。
            saveHandle = store.Register(SaveKey, Capture, Restore);
            saveHandle.Load();

            // 装备拥有变化(购买/解锁)→ 刷新"获得武器"类任务
            eventMgr.Add(YOTOEventType.RefreshLoadout, OnLoadoutChanged);
        }

        public void Shutdown()
        {
            Save();
            eventMgr?.Remove(YOTOEventType.RefreshLoadout, OnLoadoutChanged);
            states.Clear();
            config = null; currency = null; bag = null; loadout = null; eventMgr = null; store = null; saveHandle = null;
        }

        // ---------------- 查询(供任务界面) ----------------

        public int GetProgress(uint taskId) => GetState(taskId).progress;
        public bool IsClaimed(uint taskId) => GetState(taskId).claimed;

        /// <summary>任务目标进度(取配表 targetAmount,至少 1)。</summary>
        public int GetTarget(uint taskId)
        {
            var t = config?.taskConfig.Get(taskId);
            return t != null ? Mathf.Max(1, t.TargetAmount) : 0;
        }

        public bool IsComplete(uint taskId) => GetProgress(taskId) >= GetTarget(taskId);

        /// <summary>是否可领取(已完成且未领过)。</summary>
        public bool CanClaim(uint taskId) => IsComplete(taskId) && !IsClaimed(taskId);

        // ---------------- 进度来源 ----------------

        /// <summary>
        /// 登录结算(启动进大厅、读档完成后调一次)。处理:跨天则重置每日任务 → 更新连登天数 →
        /// 标记每日登录完成、连续登录进度、检查是否已拥有武器。
        /// </summary>
        public void RegisterLogin()
        {
            long today = TodayIndex();

            if (lastResetDay != today)
            {
                ResetDailyTasks(); // 跨天:清空所有每日任务的进度与领取态
                lastResetDay = today;
            }

            if (lastLoginDay != today)
            {
                loginStreak = (lastLoginDay == today - 1) ? loginStreak + 1 : 1; // 昨天登过=连续,否则重新计
                lastLoginDay = today;
            }
            else if (loginStreak <= 0)
            {
                loginStreak = 1;
            }

            SetProgressForType(TaskObjType.DailyLogin, int.MaxValue);      // 登录即满足(夹到目标)
            SetProgressForType(TaskObjType.ConsecutiveLogin, loginStreak); // 进度=连登天数
            RefreshWeaponTasks();

            Save();
            eventMgr?.Trigger(YOTOEventType.RefreshTask);
        }

        /// <summary>击杀一只动物时调用(每只 +1)。</summary>
        public void AddKill(int n = 1) => AddProgressForType(TaskObjType.KillAny, n);

        /// <summary>看完一次激励广告时调用(每次 +1)。</summary>
        public void AddAdWatch(int n = 1) => AddProgressForType(TaskObjType.WatchAd, n);

        // ---------------- 领取 ----------------

        /// <summary>领取已完成任务的奖励:发金币/体力/物品,标记已领取并写盘。成功返回 true(未完成/已领过返回 false)。</summary>
        public bool Claim(uint taskId)
        {
            if (!CanClaim(taskId)) return false;
            var t = config?.taskConfig.Get(taskId);
            if (t == null) return false;

            GetState(taskId).claimed = true;
            GrantRewards(t);

            Save();
            eventMgr?.Trigger(YOTOEventType.RefreshTask);
            return true;
        }

        private void GrantRewards(Task t)
        {
            if (t.RewardCoin > 0) currency.Add(CurrencyType.Gold, t.RewardCoin);
            if (t.RewardEnergy > 0) currency.Add(CurrencyType.Energy, t.RewardEnergy);
            if (t.RewardItemId > 0 && t.RewardItemCount > 0) bag?.AddItem((int)t.RewardItemId, t.RewardItemCount);
            currency.Save(); // 货币关键节点主动写盘(发奖落地)
        }

        // ---------------- 内部 ----------------

        private static long TodayIndex() => DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 86400;

        private State GetState(uint taskId)
        {
            if (!states.TryGetValue(taskId, out var s)) { s = new State(); states[taskId] = s; }
            return s;
        }

        private void OnLoadoutChanged()
        {
            if (!RefreshWeaponTasks()) return;
            Save();
            eventMgr?.Trigger(YOTOEventType.RefreshTask);
        }

        /// <summary>已拥有任一武器则把"获得武器"类任务标记为完成。返回是否有进度变化。</summary>
        private bool RefreshWeaponTasks()
        {
            if (!OwnsAnyWeapon()) return false;
            return SetProgressForType(TaskObjType.AcquireWeapon, int.MaxValue);
        }

        private bool OwnsAnyWeapon()
        {
            var list = loadout?.CategoryItems(ShopCategory.Weapon);
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++)
                if (loadout.IsOwned((int)list[i].Id)) return true;
            return false;
        }

        /// <summary>把某类型所有任务的进度设为 value(只增不减,夹到各自目标)。返回是否有变化。</summary>
        private bool SetProgressForType(TaskObjType type, int value)
        {
            bool changed = false;
            foreach (var kv in config.taskConfig.items)
            {
                var t = kv.Value;
                if ((TaskObjType)t.ObjType != type) continue;
                var s = GetState(t.Id);
                int v = Mathf.Min(Mathf.Max(1, t.TargetAmount), value);
                if (v > s.progress) { s.progress = v; changed = true; }
            }
            return changed;
        }

        /// <summary>把某类型所有未领取任务的进度 +delta(夹到目标),有变化则写盘并刷新。</summary>
        private void AddProgressForType(TaskObjType type, int delta)
        {
            if (delta <= 0 || config == null) return;
            bool changed = false;
            foreach (var kv in config.taskConfig.items)
            {
                var t = kv.Value;
                if ((TaskObjType)t.ObjType != type) continue;
                var s = GetState(t.Id);
                if (s.claimed) continue;
                int target = Mathf.Max(1, t.TargetAmount);
                if (s.progress >= target) continue;
                s.progress = Mathf.Min(target, s.progress + delta);
                changed = true;
            }
            if (changed)
            {
                Save();
                eventMgr?.Trigger(YOTOEventType.RefreshTask);
            }
        }

        private void ResetDailyTasks()
        {
            foreach (var kv in config.taskConfig.items)
            {
                var t = kv.Value;
                if (t.Category != CategoryDaily) continue;
                var s = GetState(t.Id);
                s.progress = 0;
                s.claimed = false;
            }
        }

        // ---------------- 存档 ----------------

        public void Save() => saveHandle?.Save();
        public void Load() => saveHandle?.Load();

        private TaskProgressSaveData Capture()
        {
            var d = new TaskProgressSaveData
            {
                lastResetDay = lastResetDay,
                lastLoginDay = lastLoginDay,
                loginStreak = loginStreak,
            };
            foreach (var kv in states)
                d.entries.Add(new TaskProgressSaveData.Entry { taskId = kv.Key, progress = kv.Value.progress, claimed = kv.Value.claimed });
            return d;
        }

        private void Restore(TaskProgressSaveData d)
        {
            states.Clear();
            lastResetDay = lastLoginDay = 0;
            loginStreak = 0;
            if (d != null)
            {
                lastResetDay = d.lastResetDay;
                lastLoginDay = d.lastLoginDay;
                loginStreak = d.loginStreak;
                if (d.entries != null)
                    foreach (var e in d.entries) states[e.taskId] = new State { progress = e.progress, claimed = e.claimed };
            }
            eventMgr?.Trigger(YOTOEventType.RefreshTask);
        }
    }

    /// <summary>任务进度存档体(JsonUtility:用列表存各任务进度 + 登录簿记)。</summary>
    [Serializable]
    public class TaskProgressSaveData
    {
        public long lastResetDay;
        public long lastLoginDay;
        public int loginStreak;
        public List<Entry> entries = new();

        [Serializable]
        public class Entry
        {
            public uint taskId;
            public int progress;
            public bool claimed;
        }
    }
}
