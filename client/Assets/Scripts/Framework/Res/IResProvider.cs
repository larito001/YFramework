using System;
using System.Collections;
using Object = UnityEngine.Object;

namespace YOTO
{
    /// <summary>
    /// 资源后端(可替换):<see cref="ResMgr"/> 门面把"按 key 异步取原始 asset / 释放原始 asset / 场景切换清理"
    /// 委托给本接口。当前实现 <see cref="ResourcesResProvider"/>(Unity Resources);
    /// 将来接 Addressables 只需新增一个 Provider 并在 <c>GameBootstrapper</c> 换一行注册,业务层零改动。
    ///
    /// **职责边界**:缓存 + 引用计数 + <see cref="ResourceHandle{T}"/> 全部留在 <see cref="ResMgr"/> 门面;
    /// Provider 只负责最小面 —— 取一个原始 asset、释放一个原始 asset、场景切换钩子。
    /// </summary>
    public interface IResProvider
    {
        /// <summary>在 <see cref="GameContext.InitAll"/> 阶段调用(此时 <see cref="ICoroutineRunner"/> 等依赖已注册)。</summary>
        void Init(GameContext ctx);

        /// <summary>释放后端自身资源(<see cref="ResMgr.Shutdown"/> 时调用)。</summary>
        void Shutdown();

        /// <summary>
        /// 按路径异步加载一个 asset。完成回调 (asset, releaseToken):失败 asset==null。
        /// <paramref name="releaseToken"/> 是后端私有释放凭据,门面原样存入缓存、释放时回传给 <see cref="ReleaseAsset"/>:
        /// Resources 后端恒为 null;Addressables 后端为装箱的 <c>AsyncOperationHandle</c>。
        /// </summary>
        void LoadAssetAsync(string path, Type type, Action<Object, object> onComplete);

        /// <summary>
        /// 释放一个原始 asset。<paramref name="token"/> 为加载时回传的释放凭据。
        /// 返回值仅供门面参考(true=可能延迟到场景切换才真正卸载,如 Resources 后端的 GameObject)。
        /// </summary>
        bool ReleaseAsset(Object asset, object token);

        /// <summary>场景切换时的清理钩子(Resources 后端跑 UnloadUnusedAssets;Addressables 后端可空)。</summary>
        IEnumerator OnChangeScene();
    }
}
