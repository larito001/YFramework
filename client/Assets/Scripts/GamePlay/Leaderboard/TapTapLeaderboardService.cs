using System;
using System.Collections.Generic;
using UnityEngine;
#if TAPTAP_LEADERBOARD && !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
using System.Threading.Tasks;
using TapSDK.Leaderboard;
#endif

namespace YOTO
{
    /// <summary>
    /// TapTap 排行榜接入(实现 <see cref="ILeaderboardService"/>,<see cref="IGameService"/>,由
    /// <see cref="GameBootstrapper.RegisterProjectServices"/> 注册)。SDK 文档:
    /// https://developer.taptap.cn/docs/sdk/leaderboard/guide/
    ///
    /// **平台支持**:TapTap 排行榜 SDK 只有 Mobile(Android/iOS)实现,**没有 Standalone/PC 桥接**,
    /// 故 PC/编辑器下 SDK 内部 _platform 为 null,任何调用都会 NRE。运行策略(对齐云存档):
    ///   · Editor:走"模拟榜单"(造假数据 + 把当前账号塞进榜),便于联调界面——即使开了宏也不碰真 SDK;
    ///   · Android/iOS 真机 + <c>TAPTAP_LEADERBOARD</c> 宏:走真实 SDK,依赖 TapTap 登录已成功;
    ///   · PC/其它(无 Mobile 桥接):"未接入"回退,提交/拉取回失败,界面显示空榜提示。
    /// 接入步骤见文件末尾注释。
    ///
    /// **契约映射**(框架风格:操作 fire-and-forget,结果走回调,不泄漏 Task):
    ///   Submit → SubmitScores;LoadTop → LoadLeaderboardScores + LoadCurrentPlayerLeaderboardScore;
    ///   TryOpenNative → OpenLeaderboard。是否登录由 <see cref="ILoginService"/> 判定。
    /// </summary>
    public class TapTapLeaderboardService : ILeaderboardService, IGameService
    {
        // 排行榜 ID:TapTap 开发者后台 → 应用 → 游戏服务 → 排行榜 创建后分配。
        private const string LeaderboardId = "a54n16ndiu1jn6h79j";
        private const string CollectionPublic = "public"; // 全球榜(好友榜传 "friends")

        private const string NotConfiguredMsg = "排行榜未配置:LeaderboardId 仍是占位 \"default\",请在 TapTapLeaderboardService 顶部替换为后台真实排行榜 ID。";

        /// <summary>排行榜 ID 是否已配置为真实值(仍是占位 "default" 视为未配置)。
        /// 用 string.Equals(而非 == 常量比较)避免编译期常量折叠把守卫之后的 SDK 调用判为不可达代码(CS0162)。</summary>
        private static bool IsConfigured() => !string.Equals(LeaderboardId, "default");

        private ILoginService login;

        public void Init(GameContext ctx)
        {
            ctx.TryGet<ILoginService>(out login);
#if TAPTAP_LEADERBOARD && !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
            // 注册状态回调(记录 SDK 侧错误码,如 500102=未登录;纯日志,不影响业务回调)。
            TapTapLeaderboard.RegisterLeaderboardCallback(callback);
#endif
        }

        public void Shutdown()
        {
#if TAPTAP_LEADERBOARD && !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
            TapTapLeaderboard.UnregisterLeaderboardCallback(callback);
#endif
            login = null;
        }

        // ---------------- 提交分数 ----------------

        public void Submit(long score, Action<bool> onComplete = null)
        {
            if (login == null || !login.IsLoggedIn)
            {
                Debug.LogWarning("[Leaderboard] 未登录,跳过分数提交。");
                onComplete?.Invoke(false);
                return;
            }
#if UNITY_EDITOR
            Debug.Log($"[Leaderboard] (编辑器模拟) 提交分数 {score} 成功。");
            mockSelfScore = Math.Max(mockSelfScore, score); // 记进模拟榜,LoadTop 时体现"我的成绩"
            onComplete?.Invoke(true);
#elif TAPTAP_LEADERBOARD && (UNITY_ANDROID || UNITY_IOS)
            if (!IsConfigured()) { Debug.LogError(NotConfiguredMsg); onComplete?.Invoke(false); return; }
            SubmitAsync(score, onComplete);
#else
            Debug.LogWarning("[Leaderboard] 当前平台无 TapTap 排行榜实现(仅移动端),分数未提交。");
            onComplete?.Invoke(false);
#endif
        }

        // ---------------- 拉取榜单 ----------------

        public void LoadTop(int count, Action<LeaderboardResult> onComplete)
        {
            if (count <= 0) count = 50;
#if UNITY_EDITOR
            onComplete?.Invoke(BuildMockResult(count));
#elif TAPTAP_LEADERBOARD && (UNITY_ANDROID || UNITY_IOS)
            if (!IsConfigured()) { onComplete?.Invoke(LeaderboardResult.Fail(NotConfiguredMsg)); return; }
            LoadTopAsync(count, onComplete);
#else
            onComplete?.Invoke(LeaderboardResult.Fail("排行榜未接入"));
#endif
        }

        // ---------------- 原生界面 ----------------

        public bool TryOpenNative()
        {
#if TAPTAP_LEADERBOARD && !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
            if (!IsConfigured()) return false; // 未配置真实 ID:回退自绘面板(会显示未配置提示)
            TapTapLeaderboard.OpenLeaderboard(LeaderboardId, CollectionPublic);
            return true;
#else
            return false; // 编辑器/PC/未接入:用自绘 LeaderboardPanel 兜底
#endif
        }

#if TAPTAP_LEADERBOARD && !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        private readonly LeaderboardCallback callback = new LeaderboardCallback();

        // 状态回调:仅记录 SDK 侧错误码(业务结果走各方法自己的回调)。
        private class LeaderboardCallback : ITapTapLeaderboardCallback
        {
            public void OnLeaderboardResult(int code, string message)
            {
                if (code != 0) Debug.LogWarning($"[Leaderboard] SDK 回调 code={code} msg={message}");
            }
        }

        // async void:把 SDK 的 Task API 收进内部,对外仍是 fire-and-forget + 回调(不泄漏 Task)。
        private async void SubmitAsync(long score, Action<bool> onComplete)
        {
            try
            {
                var scores = new List<SubmitScoresRequest.ScoreItem>
                {
                    new SubmitScoresRequest.ScoreItem { LeaderboardId = LeaderboardId, Score = score },
                };
                await TapTapLeaderboard.SubmitScores(scores);
                onComplete?.Invoke(true);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Leaderboard] 提交分数失败: {e}");
                onComplete?.Invoke(false);
            }
        }

        private async void LoadTopAsync(int count, Action<LeaderboardResult> onComplete)
        {
            try
            {
                var result = new LeaderboardResult { success = true };
                string selfOpenId = login?.Current?.openId;

                // 前 N 名(单页;如需超过单页上限再用 nextPage 翻页累积)。
                var scores = await TapTapLeaderboard.LoadLeaderboardScores(LeaderboardId, CollectionPublic, null, null);
                if (scores?.scores != null)
                {
                    foreach (var s in scores.scores) // s 为 SDK 的 Score(用 var 推断,避免 dynamic 在 AOT 下失效)
                    {
                        if (result.entries.Count >= count) break;
                        result.entries.Add(ToEntry(s.rank, s.score, s.user?.name, s.user?.openid, selfOpenId));
                    }
                }

                // 当前玩家自身排名(可能不在前 N)。
                var mine = await TapTapLeaderboard.LoadCurrentPlayerLeaderboardScore(LeaderboardId, CollectionPublic, null);
                var cur = mine?.currentUserScore;
                if (cur != null)
                {
                    result.self = ToEntry(cur.rank, cur.score, cur.user?.name, cur.user?.openid, selfOpenId);
                    result.self.isSelf = true;
                }

                onComplete?.Invoke(result);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Leaderboard] 拉取榜单失败: {e}");
                onComplete?.Invoke(LeaderboardResult.Fail(e.Message));
            }
        }

        /// <summary>把 SDK 的 Score 字段映射为渠道无关的 <see cref="LeaderboardEntry"/>(显式传字段,避免 dynamic)。
        /// rank/score 为 long?(可空),空按 0;头像 SDK 是 Image 对象非 URL,面板不显示故略过。</summary>
        private static LeaderboardEntry ToEntry(long? rank, long? score, string name, string openId, string selfOpenId)
        {
            return new LeaderboardEntry
            {
                rank = (int)(rank ?? 0),
                score = score ?? 0,
                name = string.IsNullOrEmpty(name) ? "玩家" : name,
                openId = openId,
                isSelf = !string.IsNullOrEmpty(selfOpenId) && openId == selfOpenId,
            };
        }
#endif

#if UNITY_EDITOR
        // ---------------- 编辑器模拟榜单(SDK 仅移动端,编辑器无桥接,用模拟联调界面)----------------
        private long mockSelfScore = -1; // <0 表示本次会话还没提交过分数(未上榜)

        private LeaderboardResult BuildMockResult(int count)
        {
            var r = new LeaderboardResult { success = true };
            string selfName = login?.Current?.name ?? "我";
            string selfOpenId = login?.Current?.openId ?? "editor_mock_openid";

            // 造一批递减分数的假玩家;若本会话提交过分数,把"我"按分数插进榜中正确名次。
            var raw = new List<(string name, string openId, long score)>();
            for (int i = 0; i < Math.Max(count, 20); i++)
                raw.Add(($"猎手_{i + 1:00}", $"mock_{i}", 5000 - i * 137));
            if (mockSelfScore >= 0) raw.Add((selfName, selfOpenId, mockSelfScore));
            raw.Sort((a, b) => b.score.CompareTo(a.score));

            for (int i = 0; i < raw.Count; i++)
            {
                bool self = raw[i].openId == selfOpenId;
                var e = new LeaderboardEntry
                {
                    rank = i + 1, name = raw[i].name, score = raw[i].score,
                    openId = raw[i].openId, isSelf = self,
                };
                if (i < count) r.entries.Add(e);
                if (self) r.self = e;
            }
            return r;
        }
#endif
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// TapTap 排行榜接入步骤(代码侧已就绪,以下为工程/打包侧手动操作):
//
// 0. 平台:排行榜 SDK 只有 Mobile 实现(无 PC/Standalone)。故仅 Android/iOS 真机能读真实榜单;
//    编辑器永远走模拟榜单(本类已自动处理),PC 走"未接入"回退。要看真实数据须在移动真机上跑。
// 1. 后台建榜:开发者后台 → 应用 → 游戏服务 → 排行榜,新建一个排行榜,拿到「排行榜 ID」,
//    把本文件顶部 LeaderboardId 的占位 "default" 换成真实 ID。排序方式(越大越好/越小越好)在后台配。
// 2. 导入 SDK:manifest.json 已加 com.taptap.sdk.leaderboard(与 core/login 同版本)。
//    文档:https://developer.taptap.cn/docs/sdk/leaderboard/guide/
// 3. 开启编译宏:Project Settings → Player → Scripting Define Symbols 添加 TAPTAP_LEADERBOARD。
//    (编辑器即使加了宏也只走模拟,不碰真 SDK;移动真机才走真实 SDK,且需 TapTap 登录已成功。)
// 4. 注册已在 GameBootstrapper.RegisterProjectServices 接好(ctx.Register<ILeaderboardService>)。
// 5. 提交时机:对局结束(GameMainPanel.ShowResult)已调用 Submit(本局总分)。
// ─────────────────────────────────────────────────────────────────────────────
