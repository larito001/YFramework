# StoreManager 存档系统使用说明

本文说明业务层如何使用存档系统（`StoreMgr`），以及如何把落盘方式换成 Steam Cloud / 加密等。
只讲用法，内部实现见 `Assets/Scripts/Framework/Store/StoreMgr.cs`。

## 设计要点

- **新增一份存档 = 一行 `Register`**，不用写容器类、不用绑定、不用管快照。
- 每个存档 **独立成一个文件**：`persistentDataPath/<Key>.json`。新增数据不会动到旧文件，天然向后兼容。
- 落盘方式（`IStorageDriver`）与序列化方式（`ISaveStrategy`）都可整体替换且互不耦合：
  换 Steam Cloud / 加密 / 二进制只动这一层，框架与业务零改动。

## 入口服务

```csharp
var ctx = GameLoop.Instance.Ctx;
var store = ctx.Get<StoreMgr>();
```

`StoreMgr` 由 `GameBootstrapper` 注册，全局唯一，生命周期与游戏一致。

## 新增一份存档（推荐写法）

在拥有该数据的系统（通常是某个 `IGameService`）的 `Init` 里注册一次即可。
以「技能树进度」为例：

```csharp
public class SkillTreeSystem : IGameService
{
    private ISaveHandle saveHandle;
    private SkillTreeData data = new SkillTreeData();

    public void Init(GameContext ctx)
    {
        var store = ctx.Get<StoreMgr>();

        // 一行接入存档：
        //   capture —— 返回当前要存的快照对象
        //   restore —— 套用读到的数据（无存档时收到 new T()，绝不为 null）
        saveHandle = store.Register("SkillTree",
            () => data,
            (SkillTreeData d) => data = d);

        saveHandle.Load();   // 初次读档
    }

    public void Unlock(int nodeId)
    {
        data.unlockedNodes.Add(nodeId);
        saveHandle.Save();   // 有变化时落盘
    }

    public void Shutdown() => saveHandle?.Save();
}
```

存档结构就是一个普通可序列化类（与 `JsonUtility` 兼容）：

```csharp
[System.Serializable]
public class SkillTreeData
{
    public int points;
    public System.Collections.Generic.List<int> unlockedNodes = new();
}
```

就这些。新增数据 **不需要改 `StoreMgr`、不需要改 `GameBootstrapper`**。

### 约定

- `Key` 全局唯一，同时是落盘文件名。上线后不要随意改（改了等于丢旧档）。
- 存档结构类要 `[Serializable]`，字段用 `JsonUtility` 支持的类型（基础类型 / `List` / 嵌套 `[Serializable]` 类，**不支持 `Dictionary`**）。
- `restore` 回调里 **不用判 null**：没有存档时框架会传入 `new T()`。
- `capture` 在每次 `Save` 时调用，请返回「当前最新」状态。

## 句柄 API（`ISaveHandle`）

`Register` 返回的句柄就是这份存档的操作入口：

```csharp
saveHandle.Save();            // 异步写盘
saveHandle.Save(() => {...}); // 写完回调
saveHandle.Load();            // 异步读盘并 restore
saveHandle.Load(() => {...}); // 读完回调
string key = saveHandle.Key;
```

读写都是异步的（走协程，不卡帧）。可靠的存档时机是「面板关闭 / 关卡结束 / 主动存档」；
`Shutdown` 时也可调，但退出过程协程可能来不及跑完，仅尽力而为。

## 批量读写（存档槽 / 退出统一保存）

```csharp
store.SaveAll();            // 保存所有已注册存档
store.SaveAll(() => {...}); // 全部写完后回调一次
store.LoadAll();            // 读取所有已注册存档并各自 restore
store.LoadAll(() => {...});
```

适合「点继续游戏 → `LoadAll` → 进场景」「退出前 `SaveAll`」这类整体操作。

## 存档分类与「新游戏 / 读取存档」

每份存档有个分类 `SaveCategory`：

- `Progress`（默认）—— 进度数据（背包、技能树……），**开新游戏要清掉**。
- `Settings` —— 玩家偏好（音量、画质……），**开新游戏要保留**。

注册时指定（不传即 `Progress`）：

```csharp
store.Register("SkillTree", () => data, (SkillTreeData d) => data = d);                       // 进度(默认)
store.Register("Graphics",  () => gfx,  (GraphicsData d) => gfx = d, SaveCategory.Settings);  // 设置
```

> 旧 `DataContaner<T>` 默认按 `Settings`（历史用法多为设置，如 `SoundSettingsContainer`）；
> 若某容器存的是进度，覆盖 `Category` 返回 `Progress` 即可。

## 多存档槽（新游戏 / 读取存档）

进度数据（`Progress`）按**存档槽**隔离落盘：键自动加 `slot{id}_` 前缀，每个槽是一套独立进度。
设置数据（`Settings`，含槽清单本身）全局共享，不随槽变。

槽清单（`SaveSlotManifest`）启动时**异步**读入缓存，所以查询前先用 `WhenSlotsReady` 等就绪：

```csharp
store.WhenSlotsReady(() =>
{
    bool hasAny = store.Slots.Count > 0;   // 有没有存档(决定"读取存档"按钮)
    foreach (var s in store.Slots) { /* s.id / s.name / s.lastPlayedUnix 列表展示 */ }
});
```

开始界面的接法（`StartPanel` / `SaveSlotPanel` 已按此实现）：

```csharp
// 新游戏:开一个新空槽(即激活)→ LoadAll(空槽无文件 → 进度系统 restore 收 new T() 重置)→ 进场景
store.CreateSlot();
store.LoadAll(() => sceneManager.SwitchScene(YSceneType.Home));

// 读取某存档:激活该槽 → LoadAll 读该槽进度 → 进场景
store.SetActiveSlot(slotId);
store.LoadAll(() => sceneManager.SwitchScene(YSceneType.Home));

// 删除某存档:抹掉该槽所有进度文件并从清单移除
store.DeleteSlot(slotId);
```

游戏内保存照常 `handle.Save()` / `store.SaveAll()`：自动落到**当前激活槽**，并刷新该槽"最后游玩时间"。

> **扩展性**:新增进度系统只要用默认 `Progress` 注册（`store.Register("SkillTree", …)`),
> 就自动按槽隔离、被新游戏重置、随读档加载、跟着 `DeleteSlot` 一起删——**存档/读档界面一行都不用改**。

### 分类速记

- `Progress`（默认）—— 进度,按槽隔离,新游戏开新槽。
- `Settings` —— 偏好(音量/画质),全局,永不随槽变。注册时传 `SaveCategory.Settings`;
  旧 `DataContaner<T>` 默认即 `Settings`(如 `SoundSettingsContainer`),存进度的容器覆盖 `Category` 为 `Progress`。

## 其它 API

```csharp
// 存档槽
store.WhenSlotsReady(cb);          // 槽清单就绪后回调(已就绪立即)
IReadOnlyList<SaveSlotInfo> s = store.Slots; // 所有槽(就绪后才有内容)
store.CreateSlot();                // 新建空槽并激活(新游戏)
store.SetActiveSlot(id);           // 切换激活槽(读档)
store.DeleteSlot(id);              // 删除槽及其进度文件
int active = store.ActiveSlot;     // 当前激活槽(0=未选)

// 单 Key
ISaveHandle h = store.GetHandle("SkillTree");      // 按 Key 取句柄，未注册返回 null
bool has = store.HasSave(SaveCategory.Progress);   // 当前激活槽该分类是否有存档
store.ClearSaves(SaveCategory.Progress);           // 清当前激活槽该分类的文件
store.Unregister("SkillTree");                      // 注销（不删盘）
store.Delete("SkillTree");                          // 删除单个已落盘文件
```

## 存档位置

- 路径：`Application.persistentDataPath/<Key>.json`
  - 进度按槽隔离 → 文件名为 `slot{槽id}_<Key>.json`；设置为全局 `<Key>.json`；槽清单为 `__saveslots.json`。
  - Windows：`%userprofile%\AppData\LocalLow\<公司名>\<产品名>\`
- 格式：JSON（默认 `JsonSaveStrategy`，带缩进，便于调试）。

## 接 Steam Cloud / 加密 / 二进制

落盘层是 `IStorageDriver`（按 key 读 / 写 / 删），Steam 的 `ISteamRemoteStorage.FileWrite/FileRead`
也是「按文件名 key-value」，二者天然对应。接 Steam Cloud 只需两步，**框架与业务代码都不动**：

### 1. 实现一个驱动

```csharp
public class SteamCloudStorageDriver : IStorageDriver
{
    public IEnumerator WriteCoroutine(string key, string content, Action onComplete = null)
    {
        yield return null;
        // 例：Facepunch.Steamworks
        // SteamRemoteStorage.FileWrite($"{key}.json", Encoding.UTF8.GetBytes(content));
        onComplete?.Invoke();
    }

    public IEnumerator ReadCoroutine<T>(string key, ISaveStrategy strategy, Action<T> onComplete) where T : class
    {
        yield return null;
        // 务必在“所有”分支都回调一次 onComplete(无档传 null),否则 Load/LoadAll 会永久挂起。
        T data = null;
        // if (SteamRemoteStorage.FileExists($"{key}.json"))
        // {
        //     var bytes = SteamRemoteStorage.FileRead($"{key}.json");
        //     var json = Encoding.UTF8.GetString(bytes);
        //     data = strategy.Deserialize<T>(json);
        // }
        onComplete?.Invoke(data);
    }

    public bool Exists(string key)
    {
        // return SteamRemoteStorage.FileExists($"{key}.json");
        return false;
    }

    public void Delete(string key)
    {
        // SteamRemoteStorage.FileDelete($"{key}.json");
    }
}
```

> 加密 / 二进制同理：要么换 `IStorageDriver`（改落盘字节），要么换 `ISaveStrategy`（改序列化格式）。

### 2. 注入驱动

`StoreMgr` 支持构造时注入驱动与序列化策略（都可空，空则用默认本地文件 + JSON）。
在 `GameBootstrapper.BuildContext` 里把：

```csharp
ctx.Register(new StoreMgr());
```

按平台改成：

```csharp
#if STEAM_BUILD
ctx.Register(new StoreMgr(new SteamCloudStorageDriver()));
#else
ctx.Register(new StoreMgr());
#endif
```

业务侧所有 `Register / Save / Load` 一律不变。

## 按 Key 分流（进度存云端、设置存本地）

`RoutingStorageDriver` 是一个组合驱动：把不同 `Key` 路由到不同子驱动，未显式路由的 `Key` 走默认驱动。
它本身也是 `IStorageDriver`，照常注入 `StoreMgr` 即可。

在 `GameBootstrapper.BuildContext` 里组装：

```csharp
var routing = new RoutingStorageDriver(new FileStorageDriver())        // 默认：本地文件
    .Route(new SteamCloudStorageDriver(), "BagSave", "SkillTree");     // 这几个走 Steam Cloud

ctx.Register(new StoreMgr(routing));
```

- `new RoutingStorageDriver(默认驱动)`：所有未列出的 `Key`（如 `sound_settings`）都用它。
- `.Route(驱动, key1, key2, ...)`：把指定 `Key` 路由到该驱动，可链式多次调用；后注册覆盖先注册。
- 想反过来「默认上云、个别存本地」，把默认设成 Cloud、`Route` 里列要落本地的 `Key` 即可。

业务侧依旧只管 `store.Register("SkillTree", ...)`，**完全不知道也不关心**这份存档最终落在云端还是本地。

## 旧写法（`DataContaner<T>`，兼容保留）

历史代码里的容器式写法仍然可用（如 `SoundMgr` 的 `SoundSettingsContainer`），
且 `BindStore` 后也会自动纳入 `SaveAll/LoadAll`。**新代码请直接用 `Register`**，不必再写容器类。

```csharp
public sealed class SoundSettingsContainer : DataContaner<SoundSettingsData>
{
    private SoundSettingsData data = new SoundSettingsData();
    public override string SaveKey => "sound_settings";
    public override SoundSettingsData GetData() => data;
    public override void __SetData(SoundSettingsData v) => data = v ?? new SoundSettingsData();
}

// 使用
container.BindStore(store);
container.Load(() => { /* container.GetData() 已是读到的数据 */ });
container.Save();
```

## 速查

| 操作 | 代码 |
| --- | --- |
| 接入新存档 | `h = store.Register("Key", () => data, (T d) => data = d);` |
| 读档 | `h.Load();` |
| 存档 | `h.Save();` |
| 全部存 / 读 | `store.SaveAll();` / `store.LoadAll();` |
| 新游戏 | `store.CreateSlot();` 然后 `store.LoadAll(...)` |
| 读取某存档 | `store.SetActiveSlot(id);` 然后 `store.LoadAll(...)` |
| 列出/删除存档 | `store.Slots` / `store.DeleteSlot(id)`(先 `WhenSlotsReady`) |
| 清单个档 | `store.Delete("Key");` |
| 换 Steam/加密 | 实现 `IStorageDriver` / `ISaveStrategy`，`new StoreMgr(driver, strategy)` 注入 |
| 按 Key 分流 | `new RoutingStorageDriver(local).Route(cloud, "BagSave", "SkillTree")` 注入 |
