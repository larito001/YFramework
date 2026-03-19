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
            public List<Action<Object>> pendingCallbacks;
        }

        private readonly Dictionary<ResourceCacheKey, ResourceEntry> cache = new();
        private ICoroutineRunner runner;
        private bool pendingUnusedAssetSweep;

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

        public T Load<T>(string path) where T : Object
        {
            if (!TryCreateKey<T>(path, out var key))
            {
                return null;
            }

            if (cache.TryGetValue(key, out var cached))
            {
                cached.refCount++;
                if (cached.asset == null && cached.isLoading)
                {
                    CompleteLoad(key, Resources.Load<T>(path));
                    if (!cache.TryGetValue(key, out cached))
                    {
                        return null;
                    }
                }

                return cached.asset as T;
            }

            var loadedAsset = Resources.Load<T>(path);
            if (loadedAsset == null)
            {
                Debug.LogError($"[ResMgr] Failed to load resource: type={typeof(T).Name}, path={path}");
                return null;
            }

            cache[key] = new ResourceEntry
            {
                asset = loadedAsset,
                refCount = 1
            };
            return loadedAsset;
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

            if (runner == null)
            {
                Debug.LogError("[ResMgr] Coroutine runner is not configured.");
                CompleteLoad<T>(key, null);
                return;
            }

            entry.isLoading = true;
            runner.Run(LoadResourceCoroutine<T>(key));
        }

        public ResourceHandle<T> LoadHandle<T>(string path) where T : Object
        {
            if (!TryCreateKey<T>(path, out var key))
            {
                return null;
            }

            var asset = Load<T>(path);
            return asset == null ? null : CreateHandle(key, asset);
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
            runner = ctx.Get<ICoroutineRunner>();
        }

        public void Shutdown()
        {
            foreach (var entry in cache.Values)
            {
                if (entry.asset != null && CanUnloadDirectly(entry.asset))
                {
                    Resources.UnloadAsset(entry.asset);
                }
            }

            cache.Clear();
            runner = null;
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

        private static bool CanUnloadDirectly(Object asset)
        {
            return asset != null && asset is not GameObject;
        }

        private void CompleteLoad<T>(ResourceCacheKey key, T loadedAsset) where T : Object
        {
            if (!cache.TryGetValue(key, out var entry))
            {
                return;
            }

            entry.isLoading = false;
            entry.asset = loadedAsset;

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

        private IEnumerator LoadResourceCoroutine<T>(ResourceCacheKey key) where T : Object
        {
            var request = Resources.LoadAsync<T>(key.Path);
            yield return request;

            var asset = request.asset as T;
            if (asset == null)
            {
                Debug.LogError($"[ResMgr] Failed to async load resource: type={typeof(T).Name}, path={key.Path}");
            }

            CompleteLoad(key, asset);
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
                return;
            }

            if (CanUnloadDirectly(entry.asset))
            {
                Resources.UnloadAsset(entry.asset);
                return;
            }

            pendingUnusedAssetSweep = true;
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
