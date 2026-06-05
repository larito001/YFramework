using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace YOTO
{
    public sealed class ResourceHandle<T> : IDisposable where T : Object
    {
        private Action releaseAction;

        internal ResourceHandle(string path, T asset, Action release)
        {
            Path = path;
            Asset = asset;
            releaseAction = release;
        }

        public string Path { get; }
        public T Asset { get; }
        public bool IsReleased { get; private set; }

        public void Release()
        {
            if (IsReleased)
            {
                return;
            }

            IsReleased = true;
            releaseAction?.Invoke();
            releaseAction = null;
        }

        public void Dispose()
        {
            Release();
        }
    }

    /// <summary>
    /// 资源管理门面:对外暴露渠道无关的加载/释放 API,内部维护 (path, Type) 引用计数缓存与 <see cref="ResourceHandle{T}"/>,
    /// 把"取原始 asset / 释放原始 asset / 场景切换清理"委托给可替换的 <see cref="IResProvider"/> 后端。
    /// 当前后端 <see cref="ResourcesResProvider"/>(Unity Resources);将来接 Addressables 只需换 Provider,业务零改动。
    ///
    /// **风格**:操作 fire-and-forget,结果走回调。同步 <see cref="Load{T}"/>/<see cref="LoadHandle{T}"/> 已标记过时
    /// (Addressables 后端无法同步加载),业务请用 <see cref="LoadAsync{T}"/>/<see cref="LoadHandleAsync{T}"/>。
    /// </summary>
    public class ResMgr : IGameService
    {
        private readonly struct ResourceCacheKey : IEquatable<ResourceCacheKey>
        {
            public ResourceCacheKey(string path, Type assetType)
            {
                Path = path;
                AssetType = assetType;
            }

            public string Path { get; }
            public Type AssetType { get; }

            public bool Equals(ResourceCacheKey other)
            {
                return Path == other.Path && AssetType == other.AssetType;
            }

            public override bool Equals(object obj)
            {
                return obj is ResourceCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Path != null ? Path.GetHashCode() : 0) * 397) ^ (AssetType != null ? AssetType.GetHashCode() : 0);
                }
            }
        }

        private sealed class ResourceEntry
        {
            public Object asset;
            public int refCount;
            public bool isLoading;
            public object releaseToken;            // 后端私有释放凭据(Resources=null;Addressables=AsyncOperationHandle)
            public List<Action<Object>> pendingCallbacks;
        }

        private readonly Dictionary<ResourceCacheKey, ResourceEntry> cache = new();
        private readonly IResProvider provider;

        public ResMgr(IResProvider provider = null)
        {
            this.provider = provider ?? new ResourcesResProvider();
        }

        public void LoadUI(string key, Action<GameObject> callback)
        {
            LoadAsync(key, callback);
        }

        public void LoadGameObject(string path, Action<GameObject> callback)
        {
            LoadAsync(path, callback);
        }

        public void LoadAudio(string path, Action<AudioClip> callback)
        {
            LoadAsync(path, callback);
        }

        public void LoadBytes(string path, Action<TextAsset> callback)
        {
            LoadAsync(path, callback);
        }

        public void LoadAsync<T>(string path, Action<T> callback) where T : Object
        {
            if (!TryCreateKey<T>(path, out var key))
            {
                callback?.Invoke(null);
                return;
            }

            if (cache.TryGetValue(key, out var entry))
            {
                entry.refCount++;
                if (entry.asset != null)
                {
                    callback?.Invoke(entry.asset as T);
                    return;
                }

                if (callback != null)
                {
                    AddPendingCallback(entry, callback);
                }

                if (entry.isLoading)
                {
                    return;
                }
            }
            else
            {
                entry = new ResourceEntry
                {
                    refCount = 1
                };

                if (callback != null)
                {
                    AddPendingCallback(entry, callback);
                }

                cache[key] = entry;
            }

            entry.isLoading = true;
            provider.LoadAssetAsync(key.Path, typeof(T), (asset, token) => CompleteLoad<T>(key, asset as T, token));
        }

        public void LoadHandleAsync<T>(string path, Action<ResourceHandle<T>> callback) where T : Object
        {
            if (callback == null)
            {
                Debug.LogWarning($"[ResMgr] Ignored async handle load because callback is null. path={path}");
                return;
            }

            if (!TryCreateKey<T>(path, out var key))
            {
                callback(null);
                return;
            }

            LoadAsync<T>(path, asset => callback(asset == null ? null : CreateHandle(key, asset)));
        }

        public void Release<T>(string path) where T : Object
        {
            if (!TryCreateKey<T>(path, out var key))
            {
                return;
            }

            Release(key);
        }

        public void Release(string path, Type assetType)
        {
            if (string.IsNullOrEmpty(path) || assetType == null)
            {
                return;
            }

            Release(new ResourceCacheKey(path, assetType));
        }

        public void ReleasePack(string path, Object obj = null)
        {
            if (obj is GameObject)
            {
                Release<GameObject>(path);
                Object.Destroy(obj);
                return;
            }

            if (obj == null)
            {
                Release<GameObject>(path);
                return;
            }

            if (obj is AudioClip)
            {
                Release<AudioClip>(path);
                return;
            }

            if (obj is TextAsset)
            {
                Release<TextAsset>(path);
                return;
            }

            if (obj is ScriptableObject)
            {
                Release(path, obj.GetType());
            }
        }

        public IEnumerator OnChangeScene(Action callback = null)
        {
            yield return provider.OnChangeScene();

            if (cache.Count == 0)
            {
                yield return null;
            }

            GC.Collect();
            yield return null;
            callback?.Invoke();
        }

        public void Init(GameContext ctx)
        {
            provider.Init(ctx);
        }

        public void Shutdown()
        {
            foreach (var entry in cache.Values)
            {
                if (entry.asset != null)
                {
                    provider.ReleaseAsset(entry.asset, entry.releaseToken);
                }
            }

            cache.Clear();
            provider.Shutdown();
            GC.Collect();
        }

        private static void AddPendingCallback<T>(ResourceEntry entry, Action<T> callback) where T : Object
        {
            entry.pendingCallbacks ??= new List<Action<Object>>();
            entry.pendingCallbacks.Add(asset => callback(asset as T));
        }

        private ResourceHandle<T> CreateHandle<T>(ResourceCacheKey key, T asset) where T : Object
        {
            return new ResourceHandle<T>(key.Path, asset, () => Release(key));
        }

        private void CompleteLoad<T>(ResourceCacheKey key, T loadedAsset, object token) where T : Object
        {
            if (!cache.TryGetValue(key, out var entry))
            {
                // entry 已在加载途中被释放(秒开秒关):此 asset 已无人引用,直接释放原始 asset 避免泄漏。
                if (loadedAsset != null)
                {
                    provider.ReleaseAsset(loadedAsset, token);
                }

                return;
            }

            entry.isLoading = false;
            entry.asset = loadedAsset;
            entry.releaseToken = token;

            var callbacks = entry.pendingCallbacks;
            entry.pendingCallbacks = null;

            if (loadedAsset == null)
            {
                cache.Remove(key);
                callbacks?.ForEach(callback => callback(null));
                return;
            }

            callbacks?.ForEach(callback => callback(loadedAsset));
        }

        private void Release(ResourceCacheKey key)
        {
            if (!cache.TryGetValue(key, out var entry))
            {
                return;
            }

            entry.refCount--;
            if (entry.refCount > 0)
            {
                return;
            }

            cache.Remove(key);
            if (entry.asset == null)
            {
                // 还在加载途中:CompleteLoad 发现 entry 已移除会自行释放原始 asset。
                return;
            }

            provider.ReleaseAsset(entry.asset, entry.releaseToken);
        }

        private static bool TryCreateKey<T>(string path, out ResourceCacheKey key) where T : Object
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[ResMgr] Resource path is null or empty.");
                key = default;
                return false;
            }

            key = new ResourceCacheKey(path, typeof(T));
            return true;
        }
    }
}
