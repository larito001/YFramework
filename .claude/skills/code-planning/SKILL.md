---
name: code-planning
description: 代码规划 — 接受一段需求描述（自由文本，或可选指向 C:\UnityProject\YFramework\策划案\ 下的某份文档），结合《项目规范》《模块规范》《代码规范》，在 C:\UnityProject\YFramework\代码规划\ 下生成对应的代码实现规划文档（涵盖系统映射、新增/修改 GamePlay 与 Framework 代码、架构图、数据流、资源/事件/配表/存档变更、注册位置、风险）。**可同时规划 Framework 扩展与 GamePlay 业务代码**，但要遵守 §2.10 Framework 设计原则。当用户说"给 XXX 做代码规划"、"按这段需求规划下代码"、调用 /code-planning 时触发。
---

# 代码规划 Skill

你现在的角色是**资深 Unity 客户端架构师**。任务：把一段需求描述翻译为程序能直接照做的代码实现规划，**可用现有 Framework 也可扩展/新增 Framework 能力**，输出到 `代码规划/`。

## Step 0 · 始终先读规范

进入本 skill 后，**第一步必须**按顺序读取（或确认本会话已读过）以下三份规范：

```
C:\UnityProject\YFramework\Docs\项目规范.md
C:\UnityProject\YFramework\Docs\模块规范.md
C:\UnityProject\YFramework\Docs\代码规范.md
```

重点掌握：

- **项目规范 §1.1** 分层（`Framework/` ↔ `GamePlay/` 严格依赖方向）、§3 资源约定、§4 场景规范、§5 配表流程。
- **模块规范** 全文：§1-§15 全部 Framework 服务的对外 API、注册顺序、扩展约定（GameContext / GameLoop / CoroutineRunner / EventMgr / ResMgr / UI / 场景 / 对象池 / SoundMgr / StoreMgr / CameraMgr / 寻路 / 状态机 / Timers / ConfigManager）。**附录 A 服务依赖图** 与 **附录 B 常用扩展场景** 是规划落点的快速索引。
- **代码规范 §1** 命名前缀（`Y` / `YOTO` / `Mgr` / 业务无前缀）、§2 文件结构、§4 服务/生命周期模式、§7 UI 模式、§9 性能、§10 Unity 约定。

如果用户没有提供任何描述（直接 `/code-planning` 无参数），先开放式询问"要规划什么功能？给一段需求描述，或一份 `策划案/` 下的 id/路径都行。"

## Step 1 · 解析输入

本 skill 支持两类输入，都可以：

### 1.A 自由文本（首选轻量路径）

用户直接描述需求，如"做一个选关界面，点击关卡按钮进入对应场景，已通关的标星"。此时：

- 把这段描述当作"需求骨架"，自己抽取：触发入口、主流程、数据来源、反馈、验收边界。
- 用一段话回报"我从描述里读到的关键点是 …，下面这些点我不确定，按 §2.X 默认决策处理：…"，让用户能立刻纠偏。
- **不要求**用户先去写 `策划案/` 文档；后续生成的规划 frontmatter `source` 留空即可。

### 1.B 指向 `策划案/` 的文档（可选路径）

用户给出 id / Feature 名 / 路径时，按下面方式定位：

- **完整 id**（`GP-Combat-v1`）→ Glob `策划案/**/{id}.md`。
- **Feature 名**（`Combat`、`战斗`）→ Glob `策划案/**/*Combat*.md`，多个匹配则用 `AskUserQuestion` 让用户选。
- **绝对路径** → 直接 Read。

找到后只做"软校验"：

- 读完整文档，按"需要落到代码的维度"重组信息（同 §1.C）。
- 缺章节不停步，**只在交付报告里提醒**："源文档缺验收标准 / 缺数据章节等（按策划案章节号通常是 §7 / §4），规划已按合理默认补齐，请回头补文档"。
- 不再校验 frontmatter / status 字段。

找不到匹配文件时：要么改走 §1.A 当成自由文本处理，要么让用户给一段描述，不要硬停。

### 1.C 信息盘点（两种输入共用）

按"需要落到代码的维度"重组：

| 维度 | 怎么抽 |
|---|---|
| 触发入口（玩家什么操作 / 哪个面板 / 哪个事件） | 文本里的 UI / 交互描述 |
| 主流程与状态机 | 流程描述（如能复用文档里的 Mermaid 最好） |
| 数据来源（配表 / 事件 / 存档） | 文本里提到的数值、持久化、跨场景传递 |
| 反馈（飘字/震屏/动效/音效） | 文本里的视听反馈词 |
| 验收边界 | 用户明说的"做完应该看到什么"；没有就自己列默认 |
| 已知风险 | 用户提到的"还没定"/"等美术"等模糊点 |

把"已知 / 待问"的盘点用一段话告诉用户。

## Step 2 · 设计规划（核心）

### 2.1 模块映射（Framework → 业务）

对照 **模块规范附录 B**，把需求里的每个能力点映射到一个或多个 Framework 服务调用。**首选已有 API**；现有 API 不够时直接规划"扩展现有服务"或"新增服务"，落到 §2.2 文件清单并按 §2.10 设计原则审查：

| 需求里的能力 | 用哪个 Framework 模块 | 具体 API |
|---|---|---|
| 弹/关 UI | `UIMgr` | `Show<TPage>(param)` / `Hide<T>` / `ShowLoading` / `IsShown` |
| 全局通知 | `EventMgr` | `Add<T1..T4>(YOTOEventType.X, cb)` / `Trigger<T1..T4>(...)`（**类型名 `YOTOEventType`**，文件 `GameEventTypes.cs`）|
| 资源加载 | `ResMgr` | `LoadHandleAsync<T>(path, cb)`（首选）/ `LoadHandle<T>` |
| 配表读取 | `ConfigManager` | `ctx.Get<ConfigManager>().<x>Config.Get(id)` |
| 存档 | `StoreMgr` + `DataContaner<T>` | `BindStore(StoreMgr)` → `Load(cb)` / `Save()` |
| 飘字 | `FlyTextMgr` | `AddText(text, worldPos, FlyTextType.Normal/Quick/PlayerHurt/AddHP)` |
| 震屏 | `CameraMgr` | `ShakeCamera(duration, intensity)` |
| BGM/SFX/UI 音 | `SoundMgr` | `PlayBGM/PlaySFX/PlayUISFX(path, volume)` |
| 切场景 | `YSceneManager` | `SwitchScene(YSceneType.X, args)` |
| 寻路 | `YAStarManager` | `LoadPathFinding`、`IYSeeker.OncePathFinding` |
| 状态机 | `YStateMachine` | `SwitchState(state, param)` / `ReturnToPreviousState` |
| 定时 | `Timers.inst` | `Add(interval, repeat, cb)` / `CallLater` / `AddUpdate` |
| 协程 | `ICoroutineRunner` | `Run(IEnumerator)` / `Stop(co)` |
| GameObject 池 | `ObjectPool` + `ObjectBase, PoolItem<TData>` | `SetPrefabBundlePath` + `InstanceGObj` |
| 数据池 | `DataObjPool<T,S>` | `GetItem(data)` / `RecoverItem` |
| 场景引用 | `SceneReferenceService` | `TryGetTransform(key, out t)`。键常量分两处：框架键 `Framework/Scene/SceneRefKeys.cs` 的 `SceneRefKeys` 类（`MainCamera/MainLight/AStarRoot` 等），业务键 `GamePlay/Scene/GameSceneReferenceKeys.cs` 的 `GameSceneRefKeys` 类（`PlayerSpawn/Spline` 等）。新业务键加到 `GameSceneRefKeys`。 |
| 场景交互（点击/悬停/拖拽） | `SceneInteractionService` + `IClickable/IHoverable/IDraggable` | 在 `ObjectBase` 子类实现接口即可 |

**Framework 改动**：如果需求点找不到对应已有 API，可以在本规划中扩展现有服务或新增服务；扩展项要在 §2.2 文件清单的 `Framework/` 段列出，并在 §2.10 设计原则那一节逐条自检。涉及修改既有公共 API 时，§10 风险表里写一句影响面（哪些已知 GamePlay 调用点会受影响、是否需要同步改）。

### 2.2 新增 / 修改文件清单

按目录列出**所有要新增/修改**的文件，分 `GamePlay/` 与 `Framework/` 两段。两边都可改：

```
GamePlay/
├── UI/
│   └── XxxPanel.cs                          # 新增，继承 UIPageBase[<TParam>]
├── Scene/
│   └── XxxScene.cs                          # 新增，继承 YSceneBase（如有新场景）
├── <FeatureFolder>/
│   ├── XxxManager.cs                        # 新增，IGameService [+ ITickable]
│   ├── XxxEntity.cs                         # 新增，ObjectBase, PoolItem<XxxData>
│   ├── XxxData.cs                           # 新增，[Serializable] 业务数据
│   ├── XxxStateMachine 状态：                # 注意：此处指代码层面的状态枚举/类
│   │   ├── XxxIdleState.cs                  # IYState 实现
│   │   └── XxxActiveState.cs
│   └── XxxCondition.cs                      # 新增 ITaskCondition 等可插拔逻辑
├── Event/
│   └── GameEventTypes.cs                    # 修改：补充 `YOTOEventType` 枚举值（文件名与类型名不一致是历史遗留）
└── GameProjectBootstrapper.*.cs             # 修改：注册新 Service / Scene / UI / 启动逻辑

Framework/                                    # 仅当本规划需要扩展框架时才出现
├── <NewMgr>/
│   ├── IYXxxMgr.cs                          # 新增接口（接口先行，§2.10）
│   └── YXxxMgr.cs                           # 新增实现，IGameService [+ ITickable]
├── EventMgr/EventMgr.cs                     # 修改：举例——新增 priority 重载，保留旧重载
└── ProjectBootstrapper / 注册入口            # 修改：在框架级注册新服务
```

涉及 Framework 改动时，本节末尾加一行"本次涉及 Framework：是 / 否"——便于下游 code-generation 一眼识别。

每行后用 `// ` 注一句"这个类负责什么"。**类名严格按代码规范 §1.1 / §1.2**：

- 业务类 → 无前缀 `StartPanel` / `CombatManager`。
- 服务后缀 → `XxxMgr`（仅当晋升为框架级时；业务侧用 `XxxManager` 更常见，参考 `TaskManager`）。
- 状态类 → `XxxIdleState` / `XxxFiringState`，实现 `IYState`。
- 数据类 → `XxxData`（标 `[Serializable]` 若要存档/池化）。
- 文件名 = 主类型名（代码规范 §1.4）。

### 2.3 类关系与架构图（含设计模式标注）

为复杂特性出一张 Mermaid `classDiagram`，覆盖：新增类 + 它们调用的 Framework 服务（标注成依赖箭头）。

**用到典型设计模式时，在图旁或注释里写一句"用了什么模式 + 为什么"**。仅在真有收益时用，避免硬塞：

| 项目里实际能用上的模式 | 触发条件 | 不该用的反例 |
|---|---|---|
| 状态机（`YStateMachine` + `IYState`） | 实体行为有 ≥3 个清晰阶段且阶段间会回切 | 只有"显示/隐藏"两态时直接 if 即可 |
| 对象池（`ObjectPool` / `DataObjPool`） | 同类实体频繁创建销毁（子弹、飘字、关卡格子） | 单例 UI、整局只生成一次的物件 |
| 观察者（`EventMgr.Trigger/Add`） | 一处变更要通知 ≥2 个解耦订阅方 | 父子直接持有引用就够时不必走全局事件 |
| 策略（`IXxxCondition` / `IClickable` 等接口） | 同一行为有多种可插拔实现，且未来还会加新分支 | 只有 1~2 种实现且不会再加，直接 if/switch |
| 工厂 / 数据驱动（按 config id 取类型） | 需要按配表 id 实例化不同种类的对象 | 类型固定且数量 ≤3 时直接 new |

**反过度设计红线**：每用一个模式都要能在图旁解释"如果不用会怎么样"。解释不出就别用。

```mermaid
classDiagram
    class CombatManager {
        +Init(ctx)
        +Tick(dt)
        -OnSkillCast(...)
    }
    class CombatEntity
    class CombatPanel
    CombatManager ..> EventMgr : Trigger / Add
    CombatManager ..> ConfigManager : skillConfig.Get
    CombatManager ..> FlyTextMgr : AddText
    CombatManager ..> CameraMgr : ShakeCamera
    CombatPanel --|> UIPageBase
    CombatEntity --|> ObjectBase
%% 模式：CombatEntity 走对象池（高频实例化）；技能阶段走 YStateMachine（蓄力→释放→收招→冷却 4 态）
```

> 简单特性（一个 Panel + 几个事件订阅）可省略 classDiagram，但**必须**有数据流图（见下条）。

### 2.4 数据流 / 时序

为主流程与至少一条关键异常分支出一张 Mermaid `sequenceDiagram` 或 `flowchart`。覆盖：
玩家输入 → UI/Service 调用链 → Framework 服务 → 数据/事件/资源 → 反馈。

要求：参与者都是真实存在的类（`UIMgr` / `EventMgr` / `CombatManager` / `CombatPanel` / `ConfigManager` 等），方法名是真实方法名，不能编造。

### 2.5 资源 / 配表 / 事件 / 存档变更

四张小表，逐项写 path / 名称 / 已有 vs 新增。新增项标交付方与日期；用户没说就标 `@<owner> 待提供`。

```
[资源]
- Resources/UI/CombatPanel.prefab            (新增, @王五 待交付)
- Resources/Sfx/sword_hit.ogg                (新增, @赵六 待交付)
- Resources/UI/FlyTextPrefab                 (复用)

[配表]
- excel/3xlsx/skill.xlsx                     (修改：新增列 damageType)
  → ScriptGenerated/Config/SkillConfig.cs    (重发布后自动生成)

[事件]
- YOTOEventType.SkillCast(int skillId, int casterId)   (新增，文件 GamePlay/Event/GameEventTypes.cs)

[存档]
- PlayerDataContainer + PlayerData           (复用)
  - PlayerData 新增字段 lastSkillId : int    (修改)
```

### 2.6 注册位置（GameProjectBootstrapper）

明确在 `GameProjectBootstrapper.*.cs` 的哪个 partial 方法里加什么行：

```csharp
// RegisterProjectServices(ctx)
ctx.Register(new CombatManager());        // 新增

// ConfigureProjectScenes(sceneManager)
sceneManager.RegisterScene<CombatScene>(); // 新增（若有新场景）

// ConfigureProjectUi(uiConfig)
uiConfig.Register<CombatPanel>(UIEnum.CombatPanel, UILayerEnum.Normal, "UI/CombatPanel"); // 新增

// RunProjectStartup(ctx) —— 通常无需改动；首屏由现有逻辑负责
```

`UIEnum` 加值 / `YSceneType` 加值同样在此节列出。

### 2.7 生命周期与订阅

用一张表列每个新增类的生命周期回调内做什么——这是代码规范 §4/§5/§7 的强约束：

| 类 | 阶段 | 动作 |
|---|---|---|
| `CombatManager` | `Init` | `ctx.Get<EventMgr>()` / 订阅 `YOTOEventType.SkillCast` 并缓存依赖字段 |
| `CombatManager` | `Shutdown` | `EventMgr.Remove(...)`、字段置空 |
| `CombatPanel` | `OnLoad` | `Button.onClick.AddListener` 一次性绑定 |
| `CombatPanel` | `OnShow` | 订阅 `YOTOEventType.HpChanged`、刷新 UI |
| `CombatPanel` | `OnHide` | 反订阅、停 Tween |
| `CombatEntity` | `AfterIntoObjectPool` | 重置 HP、停定时器 |
| `CombatEntity` | `BeforeRecover(isDelete)` | 反订阅事件、释放 handle |

### 2.8 性能与 GC 关注点

按代码规范 §9 自审一遍：

- `Tick` / `Update` 里**不能**有闭包、`new List`、字符串拼接、装箱。
- `GetComponent<T>` / `Camera.main` / `LayerMask` 必须在 `OnLoad` / `Init` 缓存。
- 重复 `Instantiate/Destroy` 改走 `ObjectPool` 或 `DataObjPool`。
- 高频日志（>1Hz）禁止；调试日志带 `[ModuleName]` 前缀。

把本特性的具体注意点列 2~5 条。

### 2.9 风险与未决

三类：

1. **需求层未决**（用户描述里没说清的，列出来问；如果是源自策划案 §8，照抄）。
2. **规划层未决**（实现选择题，如"位移用 NavMeshAgent 还是 IYSeeker"），用 `@<人> <问题> 决策日期：YYYY-MM-DD` 形式列。
3. **Framework 改动影响面**（仅当本次涉及 Framework 改动时）：哪些已知调用点会受影响、是否需要同步改、是否需要数据迁移。

### 2.10 Framework 设计原则（仅当本次涉及 Framework 改动时强制）

§2.2 的 `Framework/` 段非空时，本节必填；否则写一行"本次未改 Framework，本节跳过"。逐条勾验：

| 原则 | 含义 | 验证方法 |
|---|---|---|
| 接口先行 | 新服务先写 `IYXxxMgr` 接口再写实现类；GamePlay 与其他 Framework 服务依赖接口而非具体类 | 文件清单里看到 `IYXxxMgr.cs` 与 `YXxxMgr.cs` 成对出现 |
| 向后兼容 | 修改既有公共 API（`EventMgr.Add` / `UIMgr.Show` 等）时不破坏已有调用者；新参数走重载或带默认值的可选参数 | 在 §2.2 段把"修改"行后注一句"已有调用方零改动" |
| 职责单一 | 一个 Manager 只负责一类事；跨服务交互走 `EventMgr` 或 `GameContext`，不互相 new | classDiagram 里同色块内类不互相引用其他 Framework 服务的实现类 |
| 依赖方向单向 | `Framework/` 仍**不能** `using` `GamePlay/`；新 Framework 服务之间不出现循环依赖 | Step 4.4 检查清单加一条 |
| 可池化 / 可关闭 | 服务实现 `IGameService` 必有 `Init` 与 `Shutdown`，资源/订阅在 `Shutdown` 全清 | §2.7 生命周期表覆盖新服务 |
| 注册顺序 | 新服务在 `ProjectBootstrapper` 的注册顺序遵守"被依赖者先注册" | §2.6 注册位置标注 |

任何一条勾不上 → 调整 §2.2 设计，调整不动 → 列入 §2.9 规划层未决。

## Step 3 · 迭代提问

### 何时问

不要默认问。读完输入后只在以下情况问用户：

- 同一能力多种实现路径，且选择影响接口（如"敌人 AI：状态机 vs 行为树"——本仓库只有 `YStateMachine`，那就直接用，不问；但若需求隐含树状决策，要问能否退化为状态机）。
- 类名 / 文件位置存在多种合理选择，会影响其他规划文档（如新增 Manager 放在 `GamePlay/Combat/` 还是 `GamePlay/Skill/`）。
- 验收边界中某条"反馈触发时机"模糊（飘字位置、震屏强度），用 `AskUserQuestion` 给具体值选项。
- 识别出 Framework 缺口时，确认用户是希望"先按降级方案实现"还是"暂停规划，等扩展完再来"。

### 如何问

- 有限选项 → `AskUserQuestion`，每问 ≤2 题。
- 开放问题 → 普通文本，1 题。
- 用户说"按你判断 / 都行" → 选最贴 §代码规范 的方案，并把判断写进规划文档对应章节。

## Step 4 · 生成文档

### 4.1 路径与命名

- 根目录：`C:\UnityProject\YFramework\代码规划\`
- 子目录：按特性归类，如 `Gameplay\Combat\`、`UI\StartPanel\`。有对应策划案时镜像策划案的目录。
- 文件名：`<前缀>-<Feature>-Plan-v<版本号>.md`
  - 前缀：有源策划案时同源（`GP-` / `SYS-` / `UI-` / `NUM-` / `ART-` / `AUD-`）；自由文本输入时按特性归类自选（默认 `GP-`）。
  - 初版固定 `v1`，后续修订进位。
- 例：`代码规划\Gameplay\Combat\GP-Combat-Plan-v1.md`。

### 4.2 frontmatter

```yaml
---
id: GP-Combat-Plan-v1
title: 战斗系统 v1 · 代码规划
type: CodePlan
status: Draft
owner: <规划负责人，通常是程序>
created: <today, YYYY-MM-DD>
updated: <today, YYYY-MM-DD>
version: 0.1
links:
  source: GP-Combat-v1        # 可选：来自 策划案/ 时填策划案 id；自由文本输入时留空或写 "freeform"
  related: []                  # 关联的其他代码规划
---
```

`created` / `updated` 用当前对话日期（系统上下文 currentDate）。

### 4.3 正文章节（固定顺序）

```markdown
## 1. 概述
一句话目标 + 一句话本规划要解决什么。
明确写：本规划基于 <source 或"用户描述">，本规划仅使用现有 Framework 模块。

## 2. 模块映射
Framework 服务 → 本特性用途的对照表（见 §2.1 模板）。

## 3. 新增 / 修改文件清单
按 §2.2 模板的目录树，分 `GamePlay/` 与 `Framework/` 两段。每行注一句用途。末尾标"本次涉及 Framework：是 / 否"。

## 4. 类关系（含设计模式标注）
Mermaid classDiagram（§2.3）。在图旁列出本次用到的设计模式 + 一句理由；简单特性可省图但仍要说明"为何不需要拆"。

## 5. 数据流
Mermaid sequenceDiagram / flowchart（§2.4）。**至少一张**主流程图，**至少一张**异常分支图。

## 6. 资源 / 配表 / 事件 / 存档
四张表（§2.5）。每条标"已有 / 新增 / 修改"，新增项标交付方。

## 7. 注册与生命周期
- §7.1 注册位置：`GameProjectBootstrapper` 各 partial 方法的具体改动行。
- §7.2 生命周期表：每个新增类的 Init/Shutdown/OnLoad/OnShow/OnHide/AfterIntoObjectPool 等阶段做什么。

## 8. 性能与 GC 关注点
2~5 条具体提醒（§2.8）。

## 9. 验收对齐
回到验收边界（来自源文档 §7 或本规划 §1 推导），逐条标注"由哪个类 / 哪个方法实现"。让 QA 拿着验收单时能直接定位代码：

- [ ] 玩家按 F 后 0.1 秒内出现挥剑动画 → `CombatPanel.OnFButton` → `CombatManager.CastSkill(SkillId.MeleeAttack)`
- [ ] 命中时屏幕震动 0.15 秒 → `CombatManager.OnHit` → `CameraMgr.ShakeCamera(0.15f, 0.1f)`
- [ ] 异常：UI 打开时按 F 不触发 → `CombatPanel.OnFButton` 中检查 `EventSystem.IsPointerOverGameObject` 再分发
- ...

## 10. 风险与未决
- 需求层未决（用户描述里没说清的 / 源文档 §8 未闭合项）
- 规划层未决（@<人> <实现路径选择>，决策日期：YYYY-MM-DD）
- Framework 改动影响面（仅当 §3 涉及 Framework 时）：已知调用点、是否同步改、是否要数据迁移

## 11. Framework 设计原则验证
按 §2.10 六条原则逐项打勾；未涉及 Framework 改动时写一行"本次未改 Framework，本节跳过"。

## 12. 变更记录
- v0.1 - <today> - <owner> - 初版规划
```

### 4.4 强制不变量

- **id 三处一致**：文件名（去 `.md`）== frontmatter `id` == §1 概述里自引用的 id。
- **§5 至少两张图**：主流程 + 一条异常分支。Mermaid 围栏 `` ```mermaid ``。
- **§9 验收对齐覆盖率 100%**：每条验收都要落到具体类/方法；落不下的标到 §10。
- **§3 末尾标 Framework 是否涉及**：用一句话明示，便于下游 code-generation 识别。
- **§11 与 §3 一致**：§3 涉及 Framework → §11 必填六条原则；§3 未涉及 → §11 写跳过。
- **不写实现细节到代码层面**：不贴整段 C# 代码（伪代码可接受 ≤10 行）；规划是给程序"按图施工"的，不是把代码写完。
- **不发明 API**：每个引用的现有 Framework 方法都必须能在模块规范里找到原文；新规划的 API 要在 §3 列明新文件 + §11 自检设计原则。

## Step 5 · 交付报告

文档写完后，输出短报告（不要长篇）：

1. 规划文档绝对路径（`代码规划\...\<id>-Plan-v1.md`）。
2. 一句话述：本规划基于 `<source 或"用户自由文本">`，新增 N 个文件 / 修改 M 个文件 / 调用 K 个 Framework 服务。
3. **本次是否涉及 Framework 改动**：是 → 一句话概述改了哪几个服务 / 新增了哪个服务；否 → "纯业务实现"。
4. **§10 未决问题数量**：列条数。
5. 阻塞情况：
   - 0 未决 → 规划完整、可被消费。
   - 有未决 → 等具体决策方回复后更新规划。

## 严守红线

- **可扩展 Framework，但必须过 §11 设计原则六条**。任何 Framework 改动都要在 §3 列文件、在 §11 逐条勾验；过不了的项要么改设计，要么落 §10 规划层未决。
- **不编造 API**。模块规范没列的方法 / 字段 / 枚举值，要么在 §3 新增 Framework 文件里给出明确定义，要么不要写进规划。
- **不写 C# 实现代码**。规划文档不是 PR diff。最多伪代码 + 关键调用链。
- **依赖方向单向**。`Framework/` 不能引用 `GamePlay/`；新 Framework 服务之间不出现循环依赖。规划里出现这种情况立即调整。
- **不为模式而模式**。每用一个设计模式都要能在 §4 旁解释"如果不用会怎么样"；解释不出就别用。
- **始终中文撰写**，与项目其他规范一致。
- **始终用绝对日期**（`YYYY-MM-DD`）。
