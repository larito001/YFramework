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
        private ICoroutineRunner runner;

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

            LoadAsyncResource<GameObject>(path, loadedAsset =>
            {
                if (loadedAsset == null)
                {
                    callback(null);
                    return;
                }

                prefabCache[path] = new CachedResource<GameObject> { asset = loadedAsset, refCount = 1 };
                callback(loadedAsset);
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

            LoadAsyncResource<AudioClip>(path, loadedAsset =>
            {
                if (loadedAsset == null)
                {
                    callback(null);
                    return;
                }

                audioCache[path] = new CachedResource<AudioClip> { asset = loadedAsset, refCount = 1 };
                callback(loadedAsset);
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

            LoadAsyncResource<TextAsset>(path, loadedAsset =>
            {
                if (loadedAsset == null)
                {
                    callback(null);
                    return;
                }

                textCache[path] = new CachedResource<TextAsset> { asset = loadedAsset, refCount = 1 };
                callback(loadedAsset);
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
            runner = ctx.Get<ICoroutineRunner>();
        }

        public void Shutdown()
        {
            prefabCache.Clear();
            audioCache.Clear();
            textCache.Clear();
            runner = null;
            GC.Collect();
        }

        private void LoadAsyncResource<T>(string path, Action<T> callback) where T : Object
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("Resource path is null or empty.");
                callback(null);
                return;
            }

            if (runner == null)
            {
                Debug.LogError("ResMgr runner is not configured.");
                callback(null);
                return;
            }

            runner.Run(LoadResourceCoroutine(path, callback));
        }

        private IEnumerator LoadResourceCoroutine<T>(string path, Action<T> callback) where T : Object
        {
            var request = Resources.LoadAsync<T>(path);
            yield return request;
            callback(request.asset as T);
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
