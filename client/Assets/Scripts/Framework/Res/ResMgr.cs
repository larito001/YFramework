using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace YOTO
{
    public class ResMgr : IGameService
    {
        private class CachedResource<T> where T : Object
        {
            public T asset;
            public int refCount;
        }

        private readonly Dictionary<string, CachedResource<GameObject>> prefabCache = new();
        private readonly Dictionary<string, CachedResource<AudioClip>> audioCache = new();
        private readonly Dictionary<string, CachedResource<TextAsset>> textCache = new();

        private ResLoader<T> CreateLoader<T>() where T : Object
        {
            return ResLoader<T>.pool.GetItem(Vector3.zero);
        }

        private void RecycleLoader<T>(ResLoader<T> loader) where T : Object
        {
            ResLoader<T>.pool.RecoverItem(loader);
        }

        public void Init()
        {
        }

        public void LoadUI(string key, Action<GameObject> callback)
        {
            LoadGameObject(key, callback);
        }

        public void LoadGameObject(string path, Action<GameObject> callback)
        {
            if (prefabCache.TryGetValue(path, out var cached))
            {
                cached.refCount++;
                callback(cached.asset);
                return;
            }

            var loader = CreateLoader<GameObject>();
            loader.LoadAsync(path, loadedAsset =>
            {
                if (loadedAsset == null)
                {
                    callback(null);
                    RecycleLoader(loader);
                    return;
                }

                prefabCache[path] = new CachedResource<GameObject> { asset = loadedAsset, refCount = 1 };
                callback(loadedAsset);
                RecycleLoader(loader);
            });
        }

        public void LoadAudio(string path, Action<AudioClip> callback)
        {
            if (audioCache.TryGetValue(path, out var cached))
            {
                cached.refCount++;
                callback(cached.asset);
                return;
            }

            var loader = CreateLoader<AudioClip>();
            loader.LoadAsync(path, loadedAsset =>
            {
                if (loadedAsset == null)
                {
                    callback(null);
                    RecycleLoader(loader);
                    return;
                }

                audioCache[path] = new CachedResource<AudioClip> { asset = loadedAsset, refCount = 1 };
                callback(loadedAsset);
                RecycleLoader(loader);
            });
        }

        public void LoadBytes(string path, Action<TextAsset> callback)
        {
            if (textCache.TryGetValue(path, out var cached))
            {
                cached.refCount++;
                callback(cached.asset);
                return;
            }

            var loader = CreateLoader<TextAsset>();
            loader.LoadAsync(path, loadedAsset =>
            {
                if (loadedAsset == null)
                {
                    callback(null);
                    RecycleLoader(loader);
                    return;
                }

                textCache[path] = new CachedResource<TextAsset> { asset = loadedAsset, refCount = 1 };
                callback(loadedAsset);
                RecycleLoader(loader);
            });
        }

        public void ReleasePack(string path, Object obj = null)
        {
            if (obj is GameObject)
            {
                ReleasePrefabReference(path);
                Object.Destroy(obj);
                return;
            }

            if (obj == null)
            {
                ReleasePrefabReference(path);
                return;
            }

            if (obj is AudioClip && audioCache.TryGetValue(path, out var cachedAudio))
            {
                cachedAudio.refCount--;
                if (cachedAudio.refCount <= 0)
                {
                    Resources.UnloadAsset(cachedAudio.asset);
                    audioCache.Remove(path);
                }
            }
        }

        public IEnumerator OnChangeScene(Action callback = null)
        {
            for (int i = 0; i < 2; i++)
            {
                yield return null;
            }

            var unloadAsset = Resources.UnloadUnusedAssets();
            while (!unloadAsset.isDone)
            {
                yield return null;
            }

            GC.Collect();
            yield return null;
            callback?.Invoke();
        }

        public void Init(GameContext ctx)
        {
            ResLoader<GameObject>.Configure(ctx.Get<ICoroutineRunner>());
            ResLoader<AudioClip>.Configure(ctx.Get<ICoroutineRunner>());
            ResLoader<TextAsset>.Configure(ctx.Get<ICoroutineRunner>());
        }

        public void Shutdown()
        {
            prefabCache.Clear();
            audioCache.Clear();
            textCache.Clear();
            GC.Collect();
        }

        private void ReleasePrefabReference(string path)
        {
            if (!prefabCache.TryGetValue(path, out var cachedPrefab))
            {
                return;
            }

            cachedPrefab.refCount--;
            if (cachedPrefab.refCount <= 0)
            {
                prefabCache.Remove(path);
                Resources.UnloadUnusedAssets();
            }
        }
    }
}
