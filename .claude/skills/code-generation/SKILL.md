---
name: code-generation
description: 代码生成 — 接受一份 C:\UnityProject\YFramework\代码规划\ 下的代码规划文档，**严格按规划**在 client/Assets/Scripts/GamePlay/ 下落地代码（新增/修改 .cs 文件、修改 GameProjectBootstrapper.cs partial、修改 GameEventTypes/UIEnum/YSceneType 枚举），完成后执行静态自检与局部自测。**不写 Framework/ 代码**，**不超出规划范围发挥**。当用户说"按 GP-Xxx-Plan-v1 写代码"、"生成 XX 的代码"、"实现 XX 规划"、调用 /code-generation 时触发。
---

# 代码生成 Skill

你现在的角色是**严格执行规划的 Unity 客户端工程师**。任务：把一份已存在的代码规划文档（`代码规划/...md`）一字不差地翻译成可编译的 C# 代码，落到 `client/Assets/Scripts/GamePlay/` 下。

**核心原则**：规划是合同。规划写了的全部要做、规划没写的一律不做。发现规划缺失或矛盾 → 停下问用户，不擅自补全。

## Step 0 · 先读规范与 Bootstrapper 当前状态

进入本 skill 后，**第一步必须**确认（或读取）：

```
C:\UnityProject\YFramework\Docs\项目规范.md         (§1.1 分层、§3 资源约定)
C:\UnityProject\YFramework\Docs\模块规范.md         (各 Manager 的 API 签名)
C:\UnityProject\YFramework\Docs\代码规范.md         (§1 命名、§2 文件结构、§4 服务、§7 UI、§9 性能、§10 Unity)
C:\UnityProject\YFramework\Docs\需求规范.md         (本 skill 不直接用，但作背景)
C:\UnityProject\YFramework\client\Assets\Scripts\GamePlay\GameProjectBootstrapper.cs  (要插入注册行)
C:\UnityProject\YFramework\client\Assets\Scripts\GamePlay\Event\GameEventTypes.cs      (要补枚举值)
C:\UnityProject\YFramework\client\Assets\Scripts\GamePlay\UI\GameUIEnum.cs              (要补 UIEnum)
C:\UnityProject\YFramework\client\Assets\Scripts\GamePlay\Scene\GameSceneTypes.cs       (要补 YSceneType)
```

注意当前 `GameEventTypes.cs` 里枚举类型仍叫 `YOTOEventType`（不是 `GameEventTypes` —— 文件名已迁移但类名未改），写代码时引用 `YOTOEventType.Xxx`，不要凭规范命名编造 `GameEventTypes.Xxx`。

如果用户没指定规划文档（直接 `/code-generation` 无参），开放式问："要执行哪份代码规划？给 id（如 `GP-Combat-Plan-v1`）或目录名。"

## Step 1 · 读取并验证规划文档

### 1.1 定位规划文档

按用户给的 id / Feature / 路径在 `代码规划/` 下查找。多个匹配 → `AskUserQuestion` 让用户选。找不到 → 提示"先用 `/code-planning <策划案 id>` 出规划，再回来生成代码"，**停**。

### 1.2 读完整文档并验证

读取规划文档全文。对照 `code-planning` skill 的输出格式，逐项验证：

| 检查项 | 不通过时的动作 |
|---|---|
| frontmatter 有 `id` / `source` / `status` | 报错并停（规划元数据残缺） |
| `source` 指向的 `策划案/...md` 文件存在 | Read 验证；不存在 → 提示用户修复规划 |
| §3 文件清单存在且至少 1 项 | 缺失 → 停 |
| §6 资源/配表/事件/存档变更四节齐全 | 缺失 → 停 |
| §7 注册位置写明了 `GameProjectBootstrapper` 改动 | 缺失 → 仅在确实没有注册需求时才放过 |
| §9 验收对齐每条对应到具体类/方法 | 缺失 → 停（无法在代码里落地） |
| **§10 待框架扩展数量 == 0** | **>0 时停**：列出待扩展项，提示"先调 framework-extension skill 处理这些项再回来生成代码；否则现在生成的代码会缺底层支撑。" |
| **代码规划 `links.excel_plan` 指向的配表规划文档存在；且其 §3 列出的所有 `excel/3xlsx/<table>.xlsx` + 对应 `client/Assets/ScriptGenerated/Config/<Table>Config.cs` 实际存在** | 配表规划缺失 → 提示"先调 `/code-planning <策划案 id>` 出代码规划+配表规划"；xlsx 缺失 → 提示"先调 `/excel-generation <配表规划 id>` 生成 xlsx，再在仓库根 `.\发布配表.bat` 发布"；xlsx 在但 `*Config.cs` 缺失 → 提示"先在仓库根 `.\发布配表.bat`"，**停**。 |
| §11 风险与未决无阻塞项 | 有阻塞项 → 列出并问"用户是否同意先按降级方案/占位实现继续？" |

### 1.3 把规划"展开"为可执行清单

从规划里抽出五张内部清单（保存到对话上下文，不写入文件）：

1. **新增 `.cs` 文件**：路径 + 类名 + 父类/接口 + 一句用途。
2. **修改 `.cs` 文件**：路径 + 改什么（加枚举值 / 加注册行 / 改字段）。
3. **资源依赖**：prefab / sound / 配表 path（这些 **本 skill 不创建**，只检查规划是否标注了交付方）。
4. **事件 / 存档新增**：`YOTOEventType` 加值、`UIEnum` 加值、`YSceneType` 加值、新 `DataContaner<T>` 类。
5. **生命周期合约**：每个新增类的 `Init/Shutdown/OnLoad/OnShow/OnHide/AfterIntoObjectPool/BeforeRecover` 各做什么（来自规划 §7.2）。

把展开后的清单用一段话回报用户，让 ta 确认无误后再开工。**用户不确认前不要写任何代码**。

## Step 2 · 执行前自检

写代码之前再过一遍以下硬规则：

### 2.1 路径与命名

- 所有新增文件落在 `client/Assets/Scripts/GamePlay/<子目录>/<ClassName>.cs`。**禁止**写到 `Framework/` 任何子目录。
- 文件名 == 主类名（含大小写）。一个 `.cs` 一个 public 类型；紧密耦合的小类型可同文件（参考 `SoundTypes.cs`）。
- 类名前缀按代码规范 §1.2：业务类 **无前缀**（`CombatManager`、`CombatEntity`、`CombatPanel`），不要给业务类加 `Y` / `YOTO` / `Got`。
- 枚举值：`PascalCase`；常量：`PascalCase`；私有字段：`camelCase` 或 `_camelCase`，**同一文件保持一致**（看相邻文件）。

### 2.2 必须遵守的 Framework 调用模式

| 模式 | 正确写法 | 反例（禁止） |
|---|---|---|
| 服务获取（IGameService 内）| `_eventMgr = ctx.Get<EventMgr>();`（在 `Init`）后续直接用字段 | 每次 `GameLoop.Instance.Ctx.Get<>()` |
| 服务获取（UIPageBase 内）| `var ev = GetService<EventMgr>();` | `Object.FindObjectOfType<EventMgr>()` |
| 资源加载 | `ctx.Get<ResMgr>().LoadHandleAsync<GameObject>(path, h => ...);` | `Resources.Load(...)` |
| 协程 | `runner.Run(MyRoutine());` 其中 `runner = ctx.Get<ICoroutineRunner>();` | 自挂 MonoBehaviour 起 `StartCoroutine` |
| 定时 | `Timers.inst.Add(0.5f, callback);` | 自写 Update 累计 dt |
| 全局事件 | `_eventMgr.Add(YOTOEventType.X, OnX);` 必须在 `OnHide`/`Shutdown` 配对 `Remove` | 匿名 lambda 订阅 |
| 飘字 | `ctx.Get<FlyTextMgr>().AddText("hit", pos, FlyTextType.PlayerHurt);` | 自己实例化 prefab |
| 震屏 | `ctx.Get<CameraMgr>().ShakeCamera(0.15f, 0.1f);` | — |
| 配表 | `ctx.Get<ConfigManager>().heroConfig.Get(id);` | 自己 `Resources.Load("Config/Data/...")` |
| 切场景 | `ctx.Get<YSceneManager>().SwitchScene(YSceneType.Home, args);` | `SceneManager.LoadScene` |
| 场景对象池 | `MyEntity : ObjectBase, PoolItem<MyData>`；`SetPrefabBundlePath` + `InstanceGObj` | 直接 `Instantiate` 循环 |
| 数据对象池 | `static DataObjPool<MyEntity, MyData> pool = new(...)` | 自维护 `List<MyEntity>` |
| 场景引用 | `ctx.Get<SceneReferenceService>().TryGetTransform("PlayerSpawn", out var t);` | `GameObject.Find("PlayerSpawn")` |

如果规划要求的实现方式与上表冲突 → **以表为准**，并在规划文档的 §11 加一条"规划层调整"说明（用 Edit 工具修改规划 v 进位）。

### 2.3 生命周期对称（**最常见 bug 来源**）

为每个新增类列一张内部对称表（不输出给用户，自己核对）：

```
CombatManager (IGameService):
  Init    : 缓存 _eventMgr / _camMgr / _flyMgr 字段；订阅 YOTOEventType.SkillCast
  Shutdown: Remove 订阅；字段置 null

CombatPanel (UIPageBase):
  OnLoad  : Button.onClick.AddListener(OnFireClick) — 一次性
  OnShow  : eventMgr.Add(YOTOEventType.HpChanged, OnHpChanged); 刷新 UI
  OnHide  : eventMgr.Remove(YOTOEventType.HpChanged, OnHpChanged); Tween 停

CombatEntity (ObjectBase, PoolItem<CombatEntityData>):
  AfterIntoObjectPool: Timers.inst.Remove(callback); 清状态
  SetData            : SetPrefabBundlePath(path); InstanceGObj()
  AfterInstanceGObj  : 应用 transform / 播动画
  BeforeRecover      : Timers 移除、handle 释放、事件反订阅
```

每个 `Add` 必须有 `Remove`、每个 `AddListener` 必须有 `RemoveAllListeners` 或 `RemoveListener`、每个 `LoadHandle` 必须有 `Release`/`Dispose`。

## Step 3 · 生成代码

按下面顺序写。**严格按顺序**：先扩枚举（被新代码引用），再写新类，最后改 Bootstrapper（被 Init 链调用）。

### 3.1 枚举扩展（如果规划要求）

| 枚举 | 文件 | 操作 |
|---|---|---|
| `UIEnum` | `client/Assets/Scripts/GamePlay/UI/GameUIEnum.cs` | Edit 末尾追加值（保持 `,` 结尾） |
| `YSceneType` | `client/Assets/Scripts/GamePlay/Scene/GameSceneTypes.cs` | Edit 在 `None` 之前插入新值（`None` 必须最后） |
| `YOTOEventType` | `client/Assets/Scripts/GamePlay/Event/GameEventTypes.cs` | Edit 末尾追加；**注意类型名仍是 `YOTOEventType`**，不要在新代码里写 `GameEventTypes.X` |

枚举值不要给显式数值（除 `None = 0`），保持序号自然递增以减少冲突。

### 3.2 新增数据 / 配置类（先于使用它们的 Manager / Panel）

- `[Serializable]` 标在所有要进 `JsonUtility` / 池化数据 struct 上。
- 字典不可序列化 → 改用 `List<Entry>` + `OnBeforeSerialize/OnAfterDeserialize`（参考 `TaskInstance`）。
- 池化 struct 形如 `ParticleEntityData { string path; Vector3 pos; float scale; }`。

### 3.3 新增 Manager / Service（IGameService）

骨架（按代码规范 §2 文件结构）：

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using YOTO;

public class CombatManager : IGameService          // 按需加 ITickable
{
    private EventMgr _eventMgr;
    private CameraMgr _camMgr;
    private FlyTextMgr _flyMgr;
    // 业务字段...

    public void Init(GameContext ctx)
    {
        _eventMgr = ctx.Get<EventMgr>();
        _camMgr = ctx.Get<CameraMgr>();
        _flyMgr = ctx.Get<FlyTextMgr>();
        _eventMgr.Add<int, int>(YOTOEventType.SkillCast, OnSkillCast);
    }

    public void Shutdown()
    {
        _eventMgr?.Remove<int, int>(YOTOEventType.SkillCast, OnSkillCast);
        _eventMgr = null;
        _camMgr = null;
        _flyMgr = null;
    }

    private void OnSkillCast(int skillId, int casterId)
    {
        // 规划 §9 对应行为
    }
}
```

要点：

- `IGameService` 在 `Framework/GameContext/IGameService.cs`，**根命名空间无 namespace**，不需要 `using YOTO`（已经全局可见）。但 `EventMgr`、`StoreMgr` 在 `YOTO` 命名空间，需要 `using YOTO`。
- `ConfigManager` 在 `YFramework.Config` —— 用到时 `using YFramework.Config;`。
- 不要在业务 Manager 里 throw；按代码规范 §8，错误用 `Debug.LogError("[ModuleName] ...")` + 安全 fallback。

### 3.4 新增 UI Panel

骨架：

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YOTO;

public class CombatPanel : UIPageBase             // 或 UIPageBase<CombatPanelParam> 强类型参数
{
    public Button btn_fire;                        // public 字段供 Inspector 拖拽 —— 仅限确实在 Inspector 配置的引用
    public TextMeshProUGUI txt_hp;

    public override void OnLoad()
    {
        btn_fire.onClick.AddListener(OnFireClick);
    }

    public override void OnShow()
    {
        var ev = GetService<EventMgr>();
        ev.Add(YOTOEventType.RefreshTrainHP, OnRefreshHp);
        OnRefreshHp();
    }

    public override void OnHide()
    {
        var ev = GetService<EventMgr>();
        ev.Remove(YOTOEventType.RefreshTrainHP, OnRefreshHp);
    }

    public override void OnResize()
    {
    }

    private void OnFireClick()
    {
        // 规划行为
    }

    private void OnRefreshHp()
    {
        // 刷新 UI
    }
}
```

要点：

- `UIPageBase` 已 `[RequireComponent(CanvasGroup, YOTOUIShow)]`，**预制体必须挂这两个组件** —— 在交付报告里强提醒。
- `OnLoad/OnShow/OnHide/OnResize` 全部 `public override`（基类是 `abstract`）。即使 `OnResize` 没事做也要留空实现。
- 内部跳转用 `Show<XxxPanel>()` / `CloseSelf()`（基类的 protected 方法）。
- 服务获取一律 `GetService<T>()`，不要走 `Context.Get`（虽然技术上可以，但规范建议封装）。
- `public` 字段仅限 Inspector 引用（参考 `StartPanel.btn_new`）；纯内部状态用 `private`。

### 3.5 新增对象池实体（ObjectBase）

参考 `ParticleEntity` 的格式：

```csharp
using UnityEngine;
using YOTO;

public struct CombatEntityData
{
    public string path;
    public Vector3 pos;
    // 其他池化数据
}

public class CombatEntity : ObjectBase, PoolItem<CombatEntityData>
{
    public static DataObjPool<CombatEntity, CombatEntityData> pool =
        new DataObjPool<CombatEntity, CombatEntityData>("CombatEntity", 4);

    private CombatEntityData _data;

    public void AfterIntoObjectPool()
    {
        // 重置状态、停定时器、SetInVision(false)、RecoverObject()
    }

    public void SetData(CombatEntityData data)
    {
        _data = data;
        Location = data.pos;
        SetInVision(true);
        SetPrefabBundlePath(data.path);
        InstanceGObj();
    }

    protected override void AfterInstanceGObj()
    {
        // prefab 实例化完成（可能异步）
    }

    protected override void BeforeRecover(bool isDelete)
    {
        // 清理
    }
}
```

不要把 `pool` 字段做成 `private` —— 按现有约定它是 `public static`，从外部 `CombatEntity.pool.GetItem(data)` 调。

### 3.6 新增场景（YSceneBase）

```csharp
using UnityEngine;
using YOTO;

public class CombatScene : YSceneBase
{
    public override YSceneType SceneType => YSceneType.GamePlay;     // 按规划填具体值
    public override string SceneName => "CombatScene";                // 与 Unity 场景文件名一致

    protected override void OnEnterScene()
    {
        // 必须最终调 EnterSceneComplete()
        EnterSceneComplete();
    }

    protected override void OnLoadingEnd()
    {
        base.OnLoadingEnd();
        UI.Show<CombatPanel>();
        UI.Hide<StartPanel>();
    }

    protected override void OnLeaveScene()
    {
        // 必须最终调 LeaveSceneComplete()
        LeaveSceneComplete();
    }
}
```

### 3.7 新增存档（DataContaner）

```csharp
using System;
using YOTO;

[Serializable]
public class CombatData
{
    public int lastSkillId;
    public int kills;
}

public class CombatDataContainer : DataContaner<CombatData>
{
    private CombatData _data = new CombatData();
    public override string SaveKey => "combat_data_v1";
    public override CombatData GetData() => _data;
    public override void __SetData(CombatData v) => _data = v ?? new CombatData();
}
```

`SaveKey` 后缀 `_v1` 便于将来版本迁移。Bind 与 Load 在使用方（通常是 Manager.Init）：`var c = new CombatDataContainer(); c.BindStore(ctx.Get<StoreMgr>()); c.Load(() => {...});`

### 3.8 修改 GameProjectBootstrapper.cs

`GameProjectBootstrapper.cs` 是 `static partial class GameBootstrapper`。**不要新建文件**，直接 Edit 现有文件。在对应 partial 方法里追加注册行：

```csharp
// RegisterProjectServices(ctx)：业务 IGameService
ctx.Register(new CombatManager());

// ConfigureProjectScenes(sceneManager)：新场景
sceneManager.RegisterScene<CombatScene>();

// ConfigureProjectUi(uiConfig)：新 Panel
uiConfig.Register<CombatPanel>(UIEnum.CombatPanel, UILayerEnum.Normal, "UI/CombatPanel");

// RunProjectStartup(ctx)：通常不动；规划要求改首屏才动
```

注意：

- 多个新增按规划的"依赖顺序"插入。Manager 之间依赖时，被依赖的先注册。
- `RegisterScene` 与 `Register<TPanel>` 的 path 字符串严格按规划填写，路径错一个字符就加载失败。

### 3.9 关于 .meta 文件、prefab、配表、音频资源

**本 skill 不创建以下产物**：

- `.cs.meta`：Unity 在 Editor 打开工程时自动生成。**交付报告里提醒用户：合并前需在 Unity Editor 里打开一次工程，让其生成 `.meta`，再 git add**（项目规范 §1.4 强制）。
- `.prefab` / `.unity` 场景文件：由策划/美术在 Editor 中拼。代码生成完后，**在交付报告里列出"需要 Unity Editor 拼装"的资产清单**（含路径、必备组件如 CanvasGroup+YOTOUIShow）。
- `excel/3xlsx/*.xlsx`：策划编辑的 Excel。代码生成完后，**列出"需要重新发布配表"的清单**（建议命令 `.\发布配表.bat`）。
- 音频 / 美术资源：由对应 owner 提供，提醒交付路径。

## Step 4 · 静态自检

写完代码后，逐项执行以下检查。**每条不通过就修，修不动就退回 Step 3**：

### 4.1 文件层面

- [ ] 每个新增 `.cs` 文件的 **类名 == 文件名**（去 `.cs`）。
- [ ] 每个新增文件都在 `Assets/Scripts/GamePlay/` 下；**未写入 `Framework/`**（用 Glob 验证：`Framework/**/<新增类名>.cs` 应无结果）。
- [ ] 每个新增 Manager 实现 `IGameService` 且有 `Init` 与 `Shutdown`。
- [ ] 每个新增 Panel 继承 `UIPageBase` 并 override 全部四个 abstract 方法（`OnLoad/OnShow/OnHide/OnResize`）。

### 4.2 调用模式（用 Grep 实际扫描新增文件）

对每个新增文件 Grep 确认**没有违禁调用**：

| Grep 模式 | 预期结果 | 如发现 |
|---|---|---|
| `\bResources\.Load\b` | 0 | 改 `ResMgr.LoadHandleAsync` |
| `\bGameObject\.Find\b` | 0 | 改 `SceneReferenceService.TryGetTransform` |
| `\bFindObjectOfType\b` / `\bFindObjectsOfType\b` | 0 | 改 `ctx.Get<T>()` 或 `Provider` |
| `\bSceneManager\.LoadScene\b` | 0 | 改 `YSceneManager.SwitchScene` |
| `\.StartCoroutine\(` | 0（业务侧不该有） | 改 `ICoroutineRunner.Run` |
| `\bnew Exception\(` / `throw new` | 0 | 改 `Debug.LogError` + 安全返回 |
| `async\s+Task` / `await ` | 0 | 项目不用 async/await，改协程 |
| `_eventMgr\.Add` 但没有匹配的 `_eventMgr\.Remove` | 0 | 补 Shutdown / OnHide 反订阅 |
| `onClick\.AddListener` 但没有 `RemoveListener` 或 `RemoveAllListeners` | 0（除非 Panel 整体随 prefab 销毁） | 视情况补 |

### 4.3 生命周期对称

逐个检查 Step 2.3 列出的"生命周期对称表"，每个 `Add`/`AddListener`/`LoadHandle` 都能在反向钩子里找到配对。

### 4.4 命名空间引用

新增类用到的类型，确认 `using` 已加：

- `UIPageBase`、`YOTOUIShow`、`UIEnum`、`UILayerEnum`：无 namespace（全局），不需 using，但用到 `EventMgr` 等需要 `using YOTO`。
- `ConfigManager`：`using YFramework.Config`。
- `DOTween`：`using DG.Tweening`。
- `TextMeshPro`：`using TMPro`。

### 4.5 Bootstrapper 一致性

Read 回 `GameProjectBootstrapper.cs`，确认：

- 新增的 `ctx.Register(new XxxManager())` 行在 `RegisterProjectServices` 内（不是其他 partial 方法）。
- 新增的 `RegisterScene<T>` 在 `ConfigureProjectScenes` 内。
- 新增的 `uiConfig.Register<TPanel>` 在 `ConfigureProjectUi` 内，path 与规划 §6 资源清单一致。
- 现有的注册行**未被误删或重排**（用 git diff 校验：`git diff -- client/Assets/Scripts/GamePlay/GameProjectBootstrapper.cs`）。

### 4.6 依赖方向

- Grep 新增 / 修改的 `Framework/` 文件——应当 **零修改**：
  ```
  git diff --name-only -- client/Assets/Scripts/Framework/
  ```
  非空 → 立即回滚那些文件，提示用户"框架修改不在本 skill 职责，需要 framework-extension"。

## Step 5 · 局部自测（在不打开 Unity 的前提下）

**Unity 项目无法在 CLI 跑 PlayMode 测试**。本 skill 的自测范围限定在静态可验证的：

### 5.1 编译可行性预判（手动 lint）

逐个新增文件 Read 一遍，自问：

- [ ] 引用的 Framework 类型/方法的签名与模块规范一致？（不确定的字段 / 方法在框架源码里 Grep 验证一次）
- [ ] 泛型参数数量与 `EventMgr.Add<...>` / `Trigger<...>` 重载（0~4 参）匹配？
- [ ] `Init(GameContext ctx)` 签名严格匹配（不能写 `Init()`、不能写 `IGameService` 之外的接口除非规划要求）。
- [ ] Override 的方法签名严格匹配父类（`public override` / `protected override` / 返回类型 / 参数）。
- [ ] 所有用到的 enum 值都已经在对应 enum 里加了（Read `YOTOEventType` / `UIEnum` / `YSceneType` 实际文件确认）。

如果发现签名不匹配 / 枚举缺失 → 回到 Step 3 修，再回 Step 4 重检。

### 5.2 与规划 §9 验收对齐核对

打开规划文档 §9 验收对齐表，逐条问自己：

- 这条验收标准要求 `<动作> → <结果>`，结果由哪个类的哪个方法兑现？
- 该方法是否真的写了？是否真的会被那条调用链触达？

举例：规划写 `[ ] 玩家按 F 触发挥剑 → CombatManager.CastSkill(MeleeAttack)`。则确认：

1. `CombatManager.CastSkill` 真的存在；
2. 有某处（通常 `CombatPanel.OnFireClick` 或某事件订阅）会调到它；
3. `CastSkill` 内部走到 §9 描述的具体反馈（飘字 / 震屏 / 伤害事件 Trigger）。

每条验收都应能在代码里追到一条调用链。**追不到 = 没实现**，回 Step 3。

### 5.3 与 §11 风险闭合

规划 §11 列出的"规划层未决"如果在 Step 1.3 已经让用户拍板，写代码时按拍板方案；自测时确认拍板方向落到代码里，未拍板的不留 TODO 散落，集中放交付报告。

## Step 6 · 交付报告

简短结构化输出，不要长篇：

```
## 代码生成完成 — <Plan id>

### 改动统计
- 新增文件 N 个：
  - GamePlay/Combat/CombatManager.cs
  - GamePlay/Combat/CombatEntity.cs
  - GamePlay/UI/CombatPanel.cs
  - ...
- 修改文件 M 个：
  - GamePlay/GameProjectBootstrapper.cs        (+3 行注册)
  - GamePlay/Event/GameEventTypes.cs           (+2 枚举值: SkillCast, HpChanged)
  - GamePlay/UI/GameUIEnum.cs                  (+1: CombatPanel)
  - GamePlay/Scene/GameSceneTypes.cs           (+1: Combat)

### 本 skill 不能完成、需要人工的工序
- [ ] 在 Unity Editor 打开工程一次，让其生成 .meta（项目规范 §1.4）
- [ ] 制作以下 prefab（含必需组件 CanvasGroup + YOTOUIShow）：
      - Resources/UI/CombatPanel.prefab
- [ ] 制作 / 提供以下美术资源（@王五，截止 2026-05-15）：
      - Resources/Sfx/sword_hit.ogg
      - Resources/Particle/HitFx.prefab
- [ ] 重新发布配表（如规划改了 xlsx）：执行 `.\发布配表.bat`

### 自检结果
- 文件层面 ............ ✅
- 调用模式扫描 ........ ✅（无 Resources.Load / GameObject.Find / async/await）
- 生命周期对称 ........ ✅
- 命名空间引用 ........ ✅
- Bootstrapper 一致性 . ✅
- 依赖方向 ............ ✅（未修改 Framework/）

### §9 验收对齐核对
- 主流程 5 条 → 全部追到调用链
- 异常分支 3 条 → 全部追到调用链 / 守卫

### 仍需人工 / 外部完成
1. 用户在 Unity Editor 打开工程，让 .meta 生成（项目规范 §1.4）；
2. 完成上面"需要人工的工序"清单；
3. 通过 GameStart.unity 跑一次（StartPanel → 进入 GameMain → 触发新功能 → 退回），按规划 §9 逐条勾验收；
4. 失败的验收项回报，本 skill 进入修复迭代（视改动量决定走 patch 还是规划升版）。
```

## 严守红线

- **不写 `Framework/`**。哪怕一行注释、一处 typo 修复都不行。框架问题 → 用户调 framework-extension skill。
- **不超规划范围**。规划没列的类不写、规划没列的方法不加、规划没列的事件不抛。**多写 = 错**。
- **不发明 API**。所有 Framework 调用从模块规范或源码可查；不确定 → Read 框架源码一次再用。
- **不跳过自检**。Step 4 与 Step 5 完整执行；任何一项不通过 → 修；修不动 → 退回 Step 3 或停下问用户。
- **不擅自修复规划缺陷**。规划矛盾、缺章节、§10 非空 → 停，让用户回 `/code-planning` 修订（升版到 v2）。
- **不创建 prefab / 场景 / xlsx / 音频文件**。只生成 `.cs`。其他资产在交付报告里清晰列出"需要谁来提供"。
- **不省略 OnHide 反订阅 / Shutdown 字段清空 / BeforeRecover 清理**。每一处 `Add` 都必须有 `Remove`。
- **始终中文回报**，但代码中的标识符、注释、日志按代码规范用 PascalCase / camelCase（中文只在 Debug.LogError 描述、TODO 备注里出现）。
- **始终用绝对日期** `YYYY-MM-DD`。
