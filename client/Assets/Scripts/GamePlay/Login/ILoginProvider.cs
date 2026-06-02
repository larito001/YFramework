using System;

namespace YOTO
{
    /// <summary>
    /// 单个登录渠道的接入点(一个渠道一份实现,如 <see cref="TapTapLoginProvider"/>)。
    /// 由 <see cref="LoginManager"/> 持有并按渠道分发。
    ///
    /// **API 约定**(与框架其余服务一致,见 <see cref="IAdService"/>/<see cref="CurrencySystem"/>):
    /// 操作 fire-and-forget(不暴露 Task),结果通过回调返回。实现内部若调用 SDK 的 async API,
    /// 自行在内部 await 并在完成时回调,不把 Task 泄漏到本接口。
    /// </summary>
    public interface ILoginProvider
    {
        /// <summary>本实现负责的渠道。</summary>
        LoginChannel Channel { get; }

        /// <summary>初始化渠道 SDK(在 <see cref="GameContext.InitAll"/> 阶段调用,各渠道只初始化一次)。</summary>
        void Init(GameContext ctx);

        /// <summary>发起交互式登录(立即返回,不阻塞)。完成后回调:成功/取消/失败见 <see cref="LoginResult"/>。</summary>
        void Login(Action<LoginResult> onComplete);

        /// <summary>登出当前渠道会话(同步、尽力而为)。</summary>
        void Logout();

        /// <summary>静默恢复已有会话(自动登录)。有效会话回成功,无会话/失败回非成功(不弹任何界面)。</summary>
        void TryRestore(Action<LoginResult> onComplete);
    }
}
