using System;
using System.Collections.Generic;

namespace YOTO
{
    /// <summary>
    /// 渠道无关的排行榜服务(注册进 <see cref="GameContext"/> 的公开契约)。界面与游戏逻辑只依赖本接口,
    /// 不直接碰任何渠道 SDK。当前实现为 TapTap 排行榜(见 <see cref="TapTapLeaderboardService"/>)。
    ///
    /// **风格**:操作 fire-and-forget(不暴露 Task),结果走回调——与 <see cref="ILoginService"/> 一致。
    /// 排行榜读写依赖已登录(<see cref="ILoginService"/>);未登录时提交/拉取会回失败。
    /// </summary>
    public interface ILeaderboardService
    {
        /// <summary>
        /// 提交一次分数(通常在对局结束时调用,best-effort)。TapTap 排行榜按"更优分数保留"策略聚合,
        /// 故每局直接提交本局分数即可,无需客户端自己比较历史最高。完成回调 true=成功 / false=失败(未登录/网络/未接入)。
        /// </summary>
        void Submit(long score, Action<bool> onComplete = null);

        /// <summary>拉取榜单前 <paramref name="count"/> 名 + 当前玩家自身排名(自身可能不在前 N,单独放在 <see cref="LeaderboardResult.self"/>)。</summary>
        void LoadTop(int count, Action<LeaderboardResult> onComplete);

        /// <summary>调起渠道内置排行榜原生界面(TapTap 自带 UI)。仅真机 + 已接入 SDK 可用;不可用返回 false(此时上层用自绘 <c>LeaderboardPanel</c> 兜底)。</summary>
        bool TryOpenNative();
    }

    /// <summary>榜单单条记录(渠道无关)。</summary>
    public sealed class LeaderboardEntry
    {
        public int rank;          // 名次(从 1 起)
        public string name;       // 玩家昵称
        public long score;        // 分数
        public string avatarUrl;  // 头像 URL(可空)
        public string openId;     // 渠道 openId(用于判定是否为本人)
        public bool isSelf;       // 是否当前登录玩家
    }

    /// <summary>榜单拉取结果。</summary>
    public sealed class LeaderboardResult
    {
        public bool success;
        public string error;                                          // 失败原因(success=false 时有意义)
        public readonly List<LeaderboardEntry> entries = new();       // 前 N 名(rank 升序)
        public LeaderboardEntry self;                                 // 当前玩家自身记录;未上榜/未登录为 null

        public static LeaderboardResult Fail(string err) => new LeaderboardResult { success = false, error = err };
    }
}
