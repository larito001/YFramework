using System;

namespace YOTO
{
    /// <summary>
    /// 激励广告服务(预留接口)。接入广告 SDK 后,实现本接口并注册进 <see cref="GameContext"/> 即可让大厅「体力补充」按钮生效。
    /// 风格与框架其余服务一致:操作 fire-and-forget(无返回 Task),结果通过 <paramref name="onClosed"/> 回调。
    /// 未注册实现时,调用方(如 <c>StartPanel</c>)按「广告未接入」处理,不发奖。
    /// </summary>
    public interface IAdService
    {
        /// <summary>
        /// 请求播放一次激励广告(立即返回,不阻塞)。广告关闭后回调结果:
        /// <paramref name="onClosed"/> 传 true 表示用户完整观看、应发放奖励;false 表示中途关闭/加载失败/无填充,不发奖。
        /// </summary>
        /// <param name="placement">广告位标识(用于区分场景与统计),如 "energy_refill"。</param>
        /// <param name="onClosed">广告关闭回调,参数为「是否应发奖」。</param>
        void ShowRewardedAd(string placement, Action<bool> onClosed);

        /// <summary>
        /// 在屏幕底部展示横幅广告(原生浮层,覆盖在游戏 UI 之上,常驻直到 <see cref="HideBanner"/>)。
        /// 立即返回不阻塞;加载/填充失败则静默不显示。重复调用会先收掉旧横幅再加载新的。
        /// 未接入 SDK / 非 Android 时为空操作。
        /// </summary>
        /// <param name="placement">广告位标识(用于区分场景与统计),如 "settings"。</param>
        void ShowBottomBanner(string placement);

        /// <summary>隐藏并销毁当前横幅广告(界面关闭时调用,释放原生资源)。无横幅时为空操作。</summary>
        void HideBanner();
    }
}
