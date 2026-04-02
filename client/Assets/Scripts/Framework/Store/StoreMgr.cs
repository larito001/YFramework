using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace YOTO
{
    #region Example

    [Serializable]
    public class TestPlayerData
    {
        public string playerName;
        public int coins;
    }

    public class TestPlayerDataContaner : DataContaner<TestPlayerData>
    {
        private TestPlayerData _data = new();
        public override string SaveKey => "player_save";

        public override TestPlayerData GetData() => _data;
        public override void __SetData(TestPlayerData data) => _data = data;
    }

    public class ExampleUsage : MonoBehaviour
    {
        void Start()
        {
            Debug.Log("Bind a StoreMgr to the data container before calling Save/Load.");
        }
    }

    #endregion

    public abstract class DataContaner<T> : IDataContainerBase where T : class, new()
    {
        private StoreMgr storeMgr;

        public abstract string SaveKey { get; }
        public abstract T GetData();
        public abstract void __SetData(T data);

        public void BindStore(StoreMgr store)
        {
            storeMgr = store;
        }

        public void Save(Action onComplete = null)
        {
            storeMgr?.Save(this, onComplete);
        }

        public void Load(Action onComplete = null)
        {
            storeMgr?.Load(this, onComplete);
        }
    }

    public interface IDataContainerBase
    {
        string SaveKey { get; }
        void BindStore(StoreMgr store);
        void Save(Action onComplete = null);
        void Load(Action onComplete = null);
    }

    public interface ISaveStrategy
    {
        string Serialize<T>(T data);
        T Deserialize<T>(string json);
    }

    public interface IStorageDriver
    {
        IEnumerator WriteCoroutine(string key, string content, Action onComplete = null);
        IEnumerator ReadCoroutine<T>(string key, ISaveStrategy strategy, Action<T> onComplete) where T : class;
        void Delete(string key);
    }

    public class FileStorageDriver : IStorageDriver
    {
        private string GetPath(string key)
        {
            return Path.Combine(Application.persistentDataPath, $"{key}.json");
        }

        public IEnumerator WriteCoroutine(string key, string content, Action onComplete = null)
        {
            string path = GetPath(key);
            yield return null;

            try
            {
                File.WriteAllText(path, content, Encoding.UTF8);
                onComplete?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save Error] {e.Message}");
            }
        }

        public IEnumerator ReadCoroutine<T>(string key, ISaveStrategy strategy, Action<T> onComplete) where T : class
        {
            string path = GetPath(key);
            yield return null;

            if (!File.Exists(path))
            {
                onComplete?.Invoke(null);
                yield break;
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                T data = strategy.Deserialize<T>(json);
                onComplete?.Invoke(data);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Load Error] {e.Message}");
                onComplete?.Invoke(null);
            }
        }

        public void Delete(string key)
        {
            string path = GetPath(key);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    public class JsonSaveStrategy : ISaveStrategy
    {
        public string Serialize<T>(T data)
        {
            return JsonUtility.ToJson(data, true);
        }

        public T Deserialize<T>(string json)
        {
            return JsonUtility.FromJson<T>(json);
        }
    }

    public class StoreMgr : IGameService
    {
        private ISaveStrategy _strategy;
        private IStorageDriver _storage;
        private ICoroutineRunner _coroutineRunner;

        public void Save<T>(DataContaner<T> dataContaner, Action onComplete = null) where T : class, new()
        {
            string json = _strategy.Serialize(dataContaner.GetData());
            _coroutineRunner.Run(_storage.WriteCoroutine(dataContaner.SaveKey, json, onComplete));
        }

        public void Load<T>(DataContaner<T> dataContaner, Action onComplete) where T : class, new()
        {
            _coroutineRunner.Run(_storage.ReadCoroutine<T>(dataContaner.SaveKey, _strategy, data =>
            {
                if (data == null) data = new T();
                dataContaner.__SetData(data);
                onComplete?.Invoke();
            }));
        }

        public void Delete(string key)
        {
            _storage.Delete(key);
        }

        public void Init(GameContext ctx)
        {
            _strategy = new JsonSaveStrategy();
            _storage = new FileStorageDriver();
            _coroutineRunner = ctx.Get<ICoroutineRunner>();
        }

        public void Shutdown()
        {
            _strategy = null;
            _storage = null;
            _coroutineRunner = null;
        }

    }
}
