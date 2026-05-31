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
    /// 存档分类。开新游戏只清 <see cref="Progress"/>(背包/技能树等进度),
    /// 不动 <see cref="Settings"/>(音量/画质等玩家偏好)。
    /// </summary>
    public enum SaveCategory
    {
        Progress,
        Settings,
    }

    /// <summary>单个存档槽的元信息(展示用)。进度数据本身按 <c>slot{id}_{key}</c> 分文件存,不在这里。</summary>
    [Serializable]
    public class SaveSlotInfo
    {
        public int id;             // 槽唯一 id(也是 Progress 落盘键前缀)
        public string name;        // 玩家自定义名(留空则 UI 用"存档{序号}")
        public long createdUnix;   // 创建时间(Unix 秒)
        public long lastPlayedUnix; // 最后游玩/写档时间(Unix 秒)
    }

    /// <summary>存档槽清单。按 Settings 全局存盘(键 <c>__saveslots</c>),记录所有槽 + 下一个可用 id。</summary>
    [Serializable]
    public class SaveSlotManifest
    {
        public int nextId = 1;
        public List<SaveSlotInfo> slots = new List<SaveSlotInfo>();
    }

    /// <summary>
    /// 一份存档数据的句柄。<see cref="StoreMgr.Register{T}"/> 返回它,业务侧持有以按需读/写;
    /// 同时被 <see cref="StoreMgr.SaveAll"/> / <see cref="StoreMgr.LoadAll"/> 统一调度。
    /// </summary>
    public interface ISaveHandle
    {
        string Key { get; }
        SaveCategory Category { get; }
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
        bool Exists(string key);
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

        public bool Exists(string key) => File.Exists(GetPath(key));

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

        public bool Exists(string key) => Resolve(key).Exists(key);

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

        // ---- 多存档槽 ----
        // Progress 数据按"激活存档槽"隔离落盘(键加 slot{id}_ 前缀);Settings 永远全局不随槽变。
        // 槽清单(SaveSlotManifest)自身按 Settings 全局存盘,启动时异步读入缓存。
        private const string ManifestKey = "__saveslots";
        private SaveSlotManifest _manifest;          // 内存缓存(启动异步读入)
        private bool _manifestLoaded;
        private readonly List<Action> _pendingReady = new List<Action>(); // 清单就绪前排队的回调
        private int _activeSlot;                      // 0 = 尚未选择存档槽

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
        /// <param name="category">存档分类。进度数据用默认 <see cref="SaveCategory.Progress"/>(开新游戏会清);
        /// 玩家偏好(音量/画质)用 <see cref="SaveCategory.Settings"/>(开新游戏不动)。</param>
        public ISaveHandle Register<T>(string key, Func<T> capture, Action<T> restore,
            SaveCategory category = SaveCategory.Progress) where T : class, new()
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("存档 Key 不能为空", nameof(key));
            if (capture == null) throw new ArgumentNullException(nameof(capture));
            if (restore == null) throw new ArgumentNullException(nameof(restore));

            var handle = new LambdaHandle<T>(this, key, capture, restore, category);
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
            _storage?.Delete(key);
        }

        /// <summary>当前激活存档槽下,该分类是否存在任意一份已落盘存档。</summary>
        public bool HasSave(SaveCategory category = SaveCategory.Progress)
        {
            if (_storage == null) return false;
            foreach (var h in _handles)
            {
                if (h.Category == category && _storage.Exists(EffectiveKey(h.Key, category))) return true;
            }
            return false;
        }

        /// <summary>
        /// 删除当前激活存档槽下该分类的所有落盘文件(默认只清 <see cref="SaveCategory.Progress"/>,不动设置)。
        /// 只清文件;内存状态需配合 <see cref="LoadAll"/>(清后读到空档 → restore 收 new T() → 内存随之重置)。
        /// </summary>
        public void ClearSaves(SaveCategory category = SaveCategory.Progress)
        {
            if (_storage == null) return;
            foreach (var h in _handles)
            {
                if (h.Category == category) _storage.Delete(EffectiveKey(h.Key, category));
            }
        }

        // ---------------- 多存档槽 ----------------

        /// <summary>当前激活存档槽 id(0 = 尚未选择)。Progress 数据读写都落到此槽。</summary>
        public int ActiveSlot => _activeSlot;

        /// <summary>已有存档槽列表(按创建顺序)。清单未就绪时为空,先用 <see cref="WhenSlotsReady"/> 等待。</summary>
        public IReadOnlyList<SaveSlotInfo> Slots => _manifest != null ? _manifest.slots : Array.Empty<SaveSlotInfo>();

        /// <summary>存档槽清单是否已从磁盘读入。</summary>
        public bool SlotsReady => _manifestLoaded;

        /// <summary>存档槽增删时触发(新建/删除)。UI 据此刷新"读取存档"按钮等状态。</summary>
        public event Action SlotsChanged;

        /// <summary>清单就绪后回调(已就绪则立即回调)。读档界面在 OnShow 里用它再刷新列表。</summary>
        public void WhenSlotsReady(Action onReady)
        {
            if (_manifestLoaded) { onReady?.Invoke(); return; }
            if (onReady != null) _pendingReady.Add(onReady);
        }

        /// <summary>
        /// 新建一个空存档槽并设为激活(用于"新游戏")。返回槽信息;清单尚未读入则拒绝并返回 null。
        /// 必须在 <see cref="WhenSlotsReady"/> 之后调用——否则无法得知磁盘上已有哪些槽,贸然新建会与异步读回的清单互相覆盖。
        /// </summary>
        public SaveSlotInfo CreateSlot()
        {
            if (!RequireSlotsLoaded(nameof(CreateSlot))) return null;
            var info = new SaveSlotInfo { id = _manifest.nextId++, createdUnix = NowUnix(), lastPlayedUnix = NowUnix() };
            _manifest.slots.Add(info);
            _activeSlot = info.id;
            PersistManifest();
            SlotsChanged?.Invoke();
            return info;
        }

        /// <summary>切换激活存档槽(用于"读取某存档")。之后 <see cref="LoadAll"/> 会读该槽数据。</summary>
        public void SetActiveSlot(int slotId) => _activeSlot = slotId;

        /// <summary>删除存档槽:抹掉它的所有 Progress 落盘文件并从清单移除。清单未就绪则拒绝(见 <see cref="CreateSlot"/>)。</summary>
        public void DeleteSlot(int slotId)
        {
            if (!RequireSlotsLoaded(nameof(DeleteSlot))) return;
            if (_storage != null)
            {
                // 按当前已注册的进度键删盘;若历史曾有现已移除的进度系统,其旧文件不在此列(孤儿档),需要时单独清理。
                foreach (var h in _handles)
                {
                    if (h.Category == SaveCategory.Progress) _storage.Delete(SlotKey(slotId, h.Key));
                }
            }
            _manifest.slots.RemoveAll(s => s.id == slotId);
            if (_activeSlot == slotId) _activeSlot = 0;
            PersistManifest();
            SlotsChanged?.Invoke();
        }

        /// <summary>Progress 键按激活槽加前缀;Settings 永远全局。槽=0(未选择)时也走全局键。</summary>
        internal string EffectiveKey(string key, SaveCategory category)
        {
            return category == SaveCategory.Progress && _activeSlot > 0 ? SlotKey(_activeSlot, key) : key;
        }

        /// <summary>进度数据按槽隔离的落盘键格式。EffectiveKey 与 DeleteSlot 共用,避免两处格式漂移。</summary>
        private static string SlotKey(int slotId, string key) => $"slot{slotId}_{key}";

        /// <summary>存档槽变更类操作的前置校验:清单未读入时拒绝并告警(防与异步读回的清单互相覆盖导致丢档)。</summary>
        private bool RequireSlotsLoaded(string op)
        {
            if (_manifestLoaded) return true;
            Debug.LogError($"[StoreMgr] {op} 在存档槽清单就绪前被调用,已忽略。请放到 WhenSlotsReady 回调里。");
            return false;
        }

        /// <summary>某份进度存档刚写盘:更新激活槽的"最后游玩时间"(同一秒内多次只持久化一次)。</summary>
        internal void NotifyProgressSaved(SaveCategory category)
        {
            if (category != SaveCategory.Progress || _activeSlot <= 0 || _manifest == null) return;
            var info = FindSlot(_activeSlot);
            if (info == null) return;
            long now = NowUnix();
            if (info.lastPlayedUnix == now) return;
            info.lastPlayedUnix = now;
            PersistManifest();
        }

        private SaveSlotInfo FindSlot(int id)
        {
            var list = _manifest?.slots;
            if (list == null) return null;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].id == id) return list[i];
            }
            return null;
        }

        private void PersistManifest()
        {
            if (_manifest != null) WriteData(ManifestKey, _manifest, null); // Settings 全局键,不走 EffectiveKey
        }

        private static long NowUnix() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // ---------------- 兼容旧 DataContaner<T> 写法 ----------------

        public void Save<T>(DataContaner<T> container, Action onComplete = null) where T : class, new()
        {
            WriteData(EffectiveKey(container.SaveKey, container.Category), container.GetData(), onComplete);
        }

        public void Load<T>(DataContaner<T> container, Action onComplete = null) where T : class, new()
        {
            ReadData<T>(EffectiveKey(container.SaveKey, container.Category), data =>
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

            // 异步读入存档槽清单;就绪后冲刷等待中的回调(读档界面等)。
            _manifest = null;
            _manifestLoaded = false;
            _activeSlot = 0;
            ReadData<SaveSlotManifest>(ManifestKey, m =>
            {
                _manifest = m ?? new SaveSlotManifest();
                if (_manifest.slots == null) _manifest.slots = new List<SaveSlotInfo>();
                _manifestLoaded = true;
                var cbs = _pendingReady.ToArray();
                _pendingReady.Clear();
                foreach (var cb in cbs) cb?.Invoke();
            });
        }

        public void Shutdown()
        {
            _handles.Clear();
            _byKey.Clear();
            _pendingReady.Clear();
            SlotsChanged = null;
            _manifest = null;
            _manifestLoaded = false;
            _activeSlot = 0;
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
            public SaveCategory Category { get; }

            public LambdaHandle(StoreMgr store, string key, Func<T> capture, Action<T> restore, SaveCategory category)
            {
                this.store = store;
                this.capture = capture;
                this.restore = restore;
                Key = key;
                Category = category;
            }

            public void Save(Action onComplete = null)
            {
                store.NotifyProgressSaved(Category); // 进度存档则刷新激活槽的最后游玩时间
                store.WriteData(store.EffectiveKey(Key, Category), capture(), onComplete);
            }

            public void Load(Action onComplete = null)
            {
                store.ReadData<T>(store.EffectiveKey(Key, Category), data =>
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

        /// <summary>存档分类。旧容器默认按 <see cref="SaveCategory.Settings"/>(历史用法多为音量/画质等偏好,
        /// 开新游戏不应清掉);若某容器存的是进度,子类可覆盖为 <see cref="SaveCategory.Progress"/>。</summary>
        public virtual SaveCategory Category => SaveCategory.Settings;

        public void BindStore(StoreMgr store)
        {
            storeMgr = store;
            store?.AddHandle(new ContainerHandle(this, Category)); // 旧容器也纳入统一注册表
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
            public ContainerHandle(IDataContainerBase c, SaveCategory category) { container = c; Category = category; }
            public string Key => container.SaveKey;
            public SaveCategory Category { get; }
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
