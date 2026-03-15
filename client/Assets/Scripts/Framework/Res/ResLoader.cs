using System;
using System.Collections;
using UnityEngine;

namespace YOTO
{
    public class ResLoader<T> : PoolItem<Vector3> where T : UnityEngine.Object
    {
        public static DataObjPool<ResLoader<T>, Vector3> pool = new DataObjPool<ResLoader<T>, Vector3>("ResLoader", 3);
        private static ICoroutineRunner runner;

        public static void Configure(ICoroutineRunner coroutineRunner)
        {
            runner = coroutineRunner;
        }

        public long ID { get; private set; }
        private static long index = 0;

        private string currentPath;
        private Action<T> currentCallback;
        private bool canceled;

        public ResLoader()
        {
            ID = index++;
        }

        public void LoadAsync(string path, Action<T> callback)
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("ResLoader LoadAsync path is null or empty");
                return;
            }

            Cancel();
            currentPath = path;
            currentCallback = callback;
            canceled = false;
            runner?.Run(LoadResourceAsync(path));
        }

        private IEnumerator LoadResourceAsync(string path)
        {
            ResourceRequest request = Resources.LoadAsync<T>(path);
            yield return request;

            if (canceled)
            {
                yield break;
            }

            T loadedAsset = request.asset as T;
            currentCallback?.Invoke(loadedAsset);
            currentPath = null;
            currentCallback = null;
        }

        public void Cancel()
        {
            canceled = true;
            currentCallback = null;
            currentPath = null;
        }

        public void AfterIntoObjectPool()
        {
            ID = -1;
            Cancel();
        }

        public void SetData(Vector3 serverData)
        {
        }
    }
}
