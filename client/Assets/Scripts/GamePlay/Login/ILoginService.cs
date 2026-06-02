using System;
using System.Collections.Generic;

namespace YOTO
{
    /// <summary>
    /// 渠道无关的登录服务(注册进 <see cref="GameContext"/> 的公开契约)。界面与游戏逻辑只依赖本接口,
    /// 不直接碰任何渠道 SDK。当前仅接入 TapTap,后续接其它登录模块对上层透明(见 <see cref="LoginManager"/>)。
    ///
    /// **风格**:操作 fire-and-forget(不暴露 Task),结果走回调。
    /// </summary>
    public interface ILoginService
    {
        /// <summary>当前是否已登录(有有效账号)。</summary>
        bool IsLoggedIn { get; }

        /// <summary>当前登录账号(未登录为 null)。</summary>
        LoginAccount Current { get; }

        /// <summary>已接入的登录渠道(界面据此决定显示哪些登录按钮)。</summary>
        IReadOnlyList<LoginChannel> AvailableChannels { get; }

        /// <summary>用指定渠道发起交互式登录;未接入该渠道时回 <see cref="LoginResult.Fail"/>。</summary>
        void Login(LoginChannel channel, Action<LoginResult> onComplete);

        /// <summary>登出当前账号。</summary>
        void Logout();

        /// <summary>启动时静默自动登录:依次尝试各渠道的会话恢复,任一成功即用之;全失败回非成功(应展示登录界面)。</summary>
        void TryAutoLogin(Action<LoginResult> onComplete);
    }
}
