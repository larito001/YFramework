using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace YOTO
{
    // ============================================================================
    //  存档系统 StoreMgr
    //  设计目标:新增一份需要存档的数据,业务侧只需一行注册,不用再写容器类 / 绑定 / 快照管线。
    //
    //  分层(可各自替换,互不耦合):
    //    ISaveStrategy   —— 序列化方式(默认 JSON)
    //    IStorageDriver  —— 落盘方式(默认每个 Key 一个文件,异步读写)
    //    StoreMgr        —— 注册表 + 统一 Save/Load/SaveAll/LoadAll 调度
    //
    //  新增存档数据(推荐写法,一行):
    //      handle = store.Register("BagSave",
    //                  () => bag.ToSaveData(),                 // 采集:返回要存的快照对象
    //                  (GridBagSaveData d) => bag.Load(d));    // 还原:套用读到的数据(无存档时给 new T())
    //      handle.Load();   // 初次读档
    //      handle.Save();   // 需要落盘时(面板关闭 / 关卡结束 等)
    //
    //  每个 Key 独立成文件(persistentDataPath/<Key>.json),新增数据不会动到旧文件,天然向后兼容。
    // ============================================================================

    /// <summary>
    /// 一份存档数据的句柄。<see cref="StoreMgr.Register{T}"/> 返回它,业务侧持有以按需读/写;
    /// 同时被 <see cref="StoreMgr.SaveAll"/> / <see cref="StoreMgr.LoadAll"/> 统一调度。
    /// </summary>
    public interface ISaveHandle
    {
        string Key { get; }
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
                // 原子写入:先写临时文件,再替换正式文件。写到一半崩溃也只会留下 .tmp,旧存档在替换成功前始终完好。
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, content, Encoding.UTF8);
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save Error] {e.Message}");
            }

            // 无论成功失败都回调:签名里没有错误通道,onComplete 表示"已结束";漏调会让 SaveAll 屏障永久挂起。
            onComplete?.Invoke();
        }

        public IEnumerator ReadCoroutine<T>(string key, ISaveStrategy strategy, Action<T> onComplete) where T : class
        {
            string path = GetPath(key);
            yield return null;

            T data = null;
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path, Encoding.UTF8);
                    data = strategy.Deserialize<T>(json);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Load Error] {e.Message}");
                    data = null;
                }
            }

            // onComplete 放在 try 之外:回调自身(restore)抛异常时不会被这里的 catch 吞掉并二次触发。
            onComplete?.Invoke(data);
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

    /// <summary>
    /// 按 Key 分流的落盘驱动:把不同存档路由到不同子驱动
    /// (典型:进度类 → Steam Cloud,画质/音量设置 → 本地文件)。
    /// 未显式路由的 Key 一律走默认驱动。本身也是 <see cref="IStorageDriver"/>,可继续被嵌套。
    ///
    /// 用法(在 GameBootstrapper 里组装后注入 StoreMgr):
    ///   var routing = new RoutingStorageDriver(new FileStorageDriver())   // 默认本地
    ///                     .Route(new SteamCloudStorageDriver(), "BagSave", "SkillTree"); // 这些走云
    ///   ctx.Register(new StoreMgr(routing));
    /// </summary>
    public class RoutingStorageDriver : IStorageDriver
    {
        private readonly IStorageDriver _default;
        private readonly Dictionary<string, IStorageDriver> _routes = new Dictionary<string, IStorageDriver>();

        public RoutingStorageDriver(IStorageDriver defaultDriver)
        {
            _default = defaultDriver ?? throw new ArgumentNullException(nameof(defaultDriver));
        }

        /// <summary>把若干 Key 路由到指定子驱动(后注册覆盖先注册)。返回 this 便于链式配置。</summary>
        public RoutingStorageDriver Route(IStorageDriver driver, params string[] keys)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            if (keys != null)
            {
                foreach (var k in keys)
                {
                    if (!string.IsNullOrEmpty(k)) _routes[k] = driver;
                }
            }
            return this;
        }

        private IStorageDriver Resolve(string key)
        {
            return key != null && _routes.TryGetValue(key, out var d) ? d : _default;
        }

        public IEnumerator WriteCoroutine(string key, string content, Action onComplete = null)
            => Resolve(key).WriteCoroutine(key, content, onComplete);

        public IEnumerator ReadCoroutine<T>(string key, ISaveStrategy strategy, Action<T> onComplete) where T : class
            => Resolve(key).ReadCoroutine(key, strategy, onComplete);

        public void Delete(string key) => Resolve(key).Delete(key);
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

        // 注入的落盘/序列化实现(可空 = 用默认)。改接 Steam Cloud 只需 new StoreMgr(new SteamCloudStorageDriver()),框架零改动。
        private readonly IStorageDriver _storageOverride;
        private readonly ISaveStrategy _strategyOverride;

        // 注册表:Key -> 句柄。List 保留注册顺序供 SaveAll/LoadAll 遍历,Dictionary 供查重/按 Key 取用。
        private readonly List<ISaveHandle> _handles = new List<ISaveHandle>();
        private readonly Dictionary<string, ISaveHandle> _byKey = new Dictionary<string, ISaveHandle>();

        /// <summary>默认实现:本地文件 + JSON。</summary>
        public StoreMgr() { }

        /// <summary>
        /// 注入落盘/序列化实现。改接 Steam Cloud / 加密 / 二进制等只走这里,不动框架与业务。
        /// </summary>
        /// <param name="storage">落盘驱动(传 null 用默认 <see cref="FileStorageDriver"/>)。</param>
        /// <param name="strategy">序列化策略(传 null 用默认 <see cref="JsonSaveStrategy"/>)。</param>
        public StoreMgr(IStorageDriver storage, ISaveStrategy strategy = null)
        {
            _storageOverride = storage;
            _strategyOverride = strategy;
        }

        // ---------------- 注册(推荐入口) ----------------

        /// <summary>
        /// 注册一份存档数据,返回其句柄。新增需要存档的数据时,业务侧只需调一次此方法。
        /// </summary>
        /// <param name="key">存档键(同时是文件名),全局唯一。</param>
        /// <param name="capture">采集快照:返回当前要存的对象(每次 Save 时调用)。</param>
        /// <param name="restore">还原:套用读到的数据;无存档时收到 <c>new T()</c>,绝不为 null。</param>
        public ISaveHandle Register<T>(string key, Func<T> capture, Action<T> restore) where T : class, new()
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("存档 Key 不能为空", nameof(key));
            if (capture == null) throw new ArgumentNullException(nameof(capture));
            if (restore == null) throw new ArgumentNullException(nameof(restore));

            var handle = new LambdaHandle<T>(this, key, capture, restore);
            AddHandle(handle);
            return handle;
        }

        /// <summary>按 Key 取回已注册句柄(未注册返回 null)。</summary>
        public ISaveHandle GetHandle(string key)
        {
            return key != null && _byKey.TryGetValue(key, out var h) ? h : null;
        }

        /// <summary>注销某 Key 的存档(不删除已落盘文件,如需删盘另调 <see cref="Delete"/>)。</summary>
        public void Unregister(string key)
        {
            if (key == null || !_byKey.TryGetValue(key, out var h)) return;
            _byKey.Remove(key);
            _handles.Remove(h);
        }

        // ---------------- 批量调度 ----------------

        /// <summary>保存所有已注册的存档。全部异步写盘完成后回调(无注册项立即回调)。</summary>
        public void SaveAll(Action onComplete = null) => RunBatch((h, cb) => h.Save(cb), onComplete);

        /// <summary>读取所有已注册的存档并各自还原。全部完成后回调。</summary>
        public void LoadAll(Action onComplete = null) => RunBatch((h, cb) => h.Load(cb), onComplete);

        public void Delete(string key)
        {
            _storage.Delete(key);
        }

        // ---------------- 兼容旧 DataContaner<T> 写法 ----------------

        public void Save<T>(DataContaner<T> container, Action onComplete = null) where T : class, new()
        {
            WriteData(container.SaveKey, container.GetData(), onComplete);
        }

        public void Load<T>(DataContaner<T> container, Action onComplete = null) where T : class, new()
        {
            ReadData<T>(container.SaveKey, data =>
            {
                container.__SetData(data ?? new T());
                onComplete?.Invoke();
            });
        }

        // ---------------- IO 原语(句柄与兼容层共用) ----------------

        internal void WriteData<T>(string key, T data, Action onComplete) where T : class
        {
            if (_storage == null || _coroutineRunner == null)
            {
                Debug.LogWarning($"[StoreMgr] 未初始化或已关闭,忽略保存:{key}");
                onComplete?.Invoke(); // 仍回调,避免 SaveAll 屏障挂起
                return;
            }
            string json = _strategy.Serialize(data);
            _coroutineRunner.Run(_storage.WriteCoroutine(key, json, onComplete));
        }

        internal void ReadData<T>(string key, Action<T> onLoaded) where T : class
        {
            if (_storage == null || _coroutineRunner == null)
            {
                Debug.LogWarning($"[StoreMgr] 未初始化或已关闭,忽略读取:{key}");
                onLoaded?.Invoke(null); // 仍回调,避免 LoadAll 屏障挂起
                return;
            }
            _coroutineRunner.Run(_storage.ReadCoroutine<T>(key, _strategy, onLoaded));
        }

        // ---------------- 生命周期 ----------------

        public void Init(GameContext ctx)
        {
            _strategy = _strategyOverride ?? new JsonSaveStrategy();
            _storage = _storageOverride ?? new FileStorageDriver();
            _coroutineRunner = ctx.Get<ICoroutineRunner>();
        }

        public void Shutdown()
        {
            _handles.Clear();
            _byKey.Clear();
            _strategy = null;
            _storage = null;
            _coroutineRunner = null;
        }

        // ---------------- 内部 ----------------

        /// <summary>登记句柄;同 Key 重复注册时覆盖并告警(便于热重载/重注册场景)。</summary>
        internal void AddHandle(ISaveHandle handle)
        {
            if (_byKey.TryGetValue(handle.Key, out var existing))
            {
                Debug.LogWarning($"[StoreMgr] 存档 Key 重复注册,已覆盖:{handle.Key}");
                _handles.Remove(existing);
            }
            _byKey[handle.Key] = handle;
            _handles.Add(handle);
        }

        /// <summary>对所有句柄执行同一操作,以计数器做屏障,全部完成后回调一次。</summary>
        private void RunBatch(Action<ISaveHandle, Action> op, Action onComplete)
        {
            if (_handles.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            var snapshot = _handles.ToArray(); // 防回调中增删注册表
            int remaining = snapshot.Length;
            foreach (var h in snapshot)
            {
                bool counted = false; // 每个句柄只计一次:即便某驱动违约重复回调,也不会把计数器减过头导致提前完成
                Action done = () =>
                {
                    if (counted) return;
                    counted = true;
                    if (--remaining == 0) onComplete?.Invoke();
                };
                op(h, done);
            }
        }

        /// <summary>Lambda 注册产生的句柄:Save 走 capture→序列化,Load 走读盘→restore。</summary>
        private sealed class LambdaHandle<T> : ISaveHandle where T : class, new()
        {
            private readonly StoreMgr store;
            private readonly Func<T> capture;
            private readonly Action<T> restore;

            public string Key { get; }

            public LambdaHandle(StoreMgr store, string key, Func<T> capture, Action<T> restore)
            {
                this.store = store;
                this.capture = capture;
                this.restore = restore;
                Key = key;
            }

            public void Save(Action onComplete = null) => store.WriteData(Key, capture(), onComplete);

            public void Load(Action onComplete = null)
            {
                store.ReadData<T>(Key, data =>
                {
                    restore(data ?? new T()); // 无存档时给空对象,restore 永远不必判 null
                    onComplete?.Invoke();
                });
            }
        }
    }

    // ============================================================================
    //  兼容层:旧的容器式写法(新代码建议直接用 StoreMgr.Register)
    //  BindStore 后会自动登记进 StoreMgr 注册表,因此也受 SaveAll/LoadAll 调度。
    // ============================================================================

    public abstract class DataContaner<T> : IDataContainerBase where T : class, new()
    {
        private StoreMgr storeMgr;

        public abstract string SaveKey { get; }
        public abstract T GetData();
        public abstract void __SetData(T data);

        public void BindStore(StoreMgr store)
        {
            storeMgr = store;
            store?.AddHandle(new ContainerHandle(this)); // 旧容器也纳入统一注册表
        }

        public void Save(Action onComplete = null)
        {
            storeMgr?.Save(this, onComplete);
        }

        public void Load(Action onComplete = null)
        {
            storeMgr?.Load(this, onComplete);
        }

        /// <summary>把 <see cref="IDataContainerBase"/> 适配成统一句柄,转调其自身 Save/Load。</summary>
        private sealed class ContainerHandle : ISaveHandle
        {
            private readonly IDataContainerBase container;
            public ContainerHandle(IDataContainerBase c) { container = c; }
            public string Key => container.SaveKey;
            public void Save(Action onComplete = null) => container.Save(onComplete);
            public void Load(Action onComplete = null) => container.Load(onComplete);
        }
    }

    public interface IDataContainerBase
    {
        string SaveKey { get; }
        void BindStore(StoreMgr store);
        void Save(Action onComplete = null);
        void Load(Action onComplete = null);
    }
}
