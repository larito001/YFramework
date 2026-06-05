using System;
using System.Collections;
using UnityEngine;
using Object = UnityEngine.Object;

namespace YOTO
{
    /// <summary>
    /// 基于 Unity <see cref="Resources"/> 的资源后端(默认实现)。行为与重构前 <see cref="ResMgr"/> 直连 Resources 等价:
    /// 异步走 <see cref="Resources.LoadAsync(string, Type)"/>;非 GameObject 释放即时 <see cref="Resources.UnloadAsset"/>,
    /// GameObject 不能直接卸载,改标记延迟到场景切换批量 <see cref="Resources.UnloadUnusedAssets"/>。
    /// 释放凭据(token)恒为 null —— Resources 按引用对象卸载,无需后端 handle。
    /// </summary>
    public sealed class ResourcesResProvider : IResProvider
    {
        private ICoroutineRunner runner;
        private bool pendingUnusedAssetSweep;

        public void Init(GameContext ctx)
        {
            runner = ctx.Get<ICoroutineRunner>();
        }

        public void Shutdown()
        {
            runner = null;
        }

        public void LoadAssetAsync(string path, Type type, Action<Object, object> onComplete)
        {
            if (onComplete == null)
            {
                return;
            }

            if (runner == null)
            {
                Debug.LogError("[ResourcesResProvider] Coroutine runner is not configured.");
                onComplete(null, null);
                return;
            }

            runner.Run(LoadCoroutine(path, type, onComplete));
        }

        public bool ReleaseAsset(Object asset, object token)
        {
            if (asset == null)
            {
                return false;
            }

            if (asset is GameObject)
            {
                // GameObject 不能用 Resources.UnloadAsset 直接卸载,标记待场景切换时批量清扫。
                pendingUnusedAssetSweep = true;
                return true;
            }

            Resources.UnloadAsset(asset);
            return false;
        }

        public IEnumerator OnChangeScene()
        {
            for (int i = 0; i < 2; i++)
            {
                yield return null;
            }

            if (pendingUnusedAssetSweep)
            {
                var unloadAsset = Resources.UnloadUnusedAssets();
                while (!unloadAsset.isDone)
                {
                    yield return null;
                }

                pendingUnusedAssetSweep = false;
            }
        }

        private static IEnumerator LoadCoroutine(string path, Type type, Action<Object, object> onComplete)
        {
            var request = Resources.LoadAsync(path, type);
            yield return request;

            var asset = request.asset;
            if (asset == null)
            {
                Debug.LogError($"[ResourcesResProvider] Failed to async load resource: type={type.Name}, path={path}");
            }

            onComplete(asset, null);
        }
    }
}
