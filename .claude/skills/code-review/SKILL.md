---
name: code-review
description: 代码评审与优化分析 — 对指定范围的代码（文件/目录/未提交改动/commit/分支 diff/整个 GamePlay）做多维度问题扫描，覆盖**依赖方向、生命周期对称、违禁 API、性能/GC、架构合理性、命名/结构/注释**等。**只读不改**：发现问题 → 在 C:\UnityProject\YFramework\代码优化规划\ 下生成结构化优化分析报告（含严重度分级与修复方案）；**审核通过则直接肯定答复，不创建文档**。当用户说"review 一下 XX"、"看下 XX 有什么问题"、"优化下 XX 代码"、"code review"、调用 /code-review 时触发。
---

# 代码评审 Skill

你现在的角色是**资深 Unity 客户端 Tech Lead**，做的是 PR review / tech-debt audit 的工作。任务：对指定范围的代码做多维度扫描，识别问题，**不改代码**，只输出分析与修复方案。

**两种结局**：

1. **有问题** → 在 `代码优化规划/` 下生成结构化报告，按严重度分级、给具体修复方案、给验证方式。
2. **无问题** → **不创建任何文档**，直接简短肯定答复（"审核通过 ✅，<scope> 没有发现需要优化的问题。"）。不为了写文档而硬挑毛病。

## Step 0 · 始终先读规范

进入本 skill 后，**第一步必须**确认（或读取）以下四份规范——本 skill 的所有判断标准都源于此：

```
C:\UnityProject\YFramework\Docs\项目规范.md     (§1.1 分层与依赖方向)
C:\UnityProject\YFramework\Docs\模块规范.md     (各 Manager 的对外 API、约定)
C:\UnityProject\YFramework\Docs\代码规范.md     (§1 命名、§2 文件结构、§3 注释、§4 服务、§5 事件、§6 资源、§7 UI、§8 错误、§9 性能、§10 Unity)
C:\UnityProject\YFramework\Docs\需求规范.md     (背景，本 skill 不直接用)
```

规范没说的不要凭"通用最佳实践"挑刺（例如"this 项目不用 async/await"是规范明文规定的，"async/await 总是更好"在本仓库就是错的）。

## Step 1 · 确定评审范围

### 1.1 解析用户输入

| 用户输入 | 评审范围 | 命令 |
|---|---|---|
| `/code-review` 无参 | 用 `AskUserQuestion` 给四个选项让用户选 | — |
| 路径（文件 / 目录） | 该路径下所有 `.cs` | Glob + Read |
| 模块名（`Combat` / `Task`） | Glob 匹配 | `**/*Combat*.cs` |
| `--diff` / "未提交改动" | 当前 working tree | `git diff --name-only HEAD` |
| `--staged` | 已 stage | `git diff --cached --name-only` |
| commit hash / branch 名 | diff 范围 | `git diff <ref>..HEAD --name-only` |
| `--all-gameplay` / "整个 GamePlay" | `client/Assets/Scripts/GamePlay/**/*.cs` | Glob |
| 类名（`StartPanel`） | 该类所在文件 + 直接引用它的文件 | Grep + Read |

如果用户说得模糊（"看下代码"），用 `AskUserQuestion`：

```
header: 评审范围
options:
- 当前未提交改动（git diff）
- 指定文件/目录路径
- 整个 GamePlay/
- 特定模块/类名
```

### 1.2 限制范围

如果范围过大（>50 个文件），先告知用户："本次评审 N 个文件，预计输出会很长。建议拆分：A) 只看 Critical/Major；B) 按子目录分批；C) 全量但结果可能不便阅读。"用 `AskUserQuestion` 让 ta 选。**不要默认全量深扫——会把上下文塞满**。

### 1.3 列出范围确认给用户

把要评审的文件清单（绝对/相对路径）一段话回报，让用户确认。**用户不确认前不开扫**。

## Step 2 · 多维度扫描

按下面顺序对每个文件做检查。一边扫一边把发现的问题记到内部清单（`severity / file:line / category / 问题 / 建议 / 影响面`）。**不要边扫边写文档**，全部扫完再统一汇总。

### 2.1 [🔴 Critical] 依赖方向

- `Framework/` 下的文件 **不能** `using GamePlay.*` 或引用 `GamePlay/` 任何类型（项目规范 §1.1）。
- 检查方法：对每个 `Framework/**/*.cs`，Grep 文件内容是否含有任何 `GamePlay/` 下声明的类型名（`StartPanel` / `GameMainScene` / `TaskManager` 等）。

### 2.2 [🔴 Critical] 生命周期对称（**最常见 bug 来源**）

对每个类，列内部对称表，逐对核验：

| 配对 | 出现位置 | 反向必须出现位置 |
|---|---|---|
| `EventMgr.Add` / `eventMgr.Add` | `Init` / `OnShow` | 配对的 `Shutdown` / `OnHide` 里有 `Remove`（**同一委托引用**，不是 lambda） |
| `Button.onClick.AddListener` | `OnLoad` | `OnHide` / 销毁前有 `RemoveListener` 或 `RemoveAllListeners`（除非整 Panel 随 prefab 销毁；规划写明的可豁免） |
| `ResMgr.LoadHandle*` / `Load*` | 任意 | 配对的 `Release` / `handle.Dispose()` |
| `runner.Run(co)` | 任意 | 必要时 `runner.Stop(co)`（短协程豁免） |
| `IGameService.Init` 字段赋值 | `Init` | `Shutdown` 里置 `null` |
| `Timers.inst.Add(_, _, cb)` | 任意 | 不再需要时 `Remove(cb)`（一次性 `Add(0.5f, cb)` 默认 `repeat=1` 可豁免） |

匿名 lambda 订阅事件 / Timers → **直接 Critical**（无法准确反订阅）。

### 2.3 [🟠 Major] 违禁 API（业务侧）

对每个 `GamePlay/**/*.cs` 与新增的非框架代码 Grep：

| 违禁模式 | 应改为 | 严重度 |
|---|---|---|
| `\bResources\.Load\b` | `ctx.Get<ResMgr>().LoadHandleAsync<T>` | 🟠 Major |
| `\bResources\.UnloadAsset\b` | 让 `ResMgr.OnChangeScene` 处理 | 🟠 Major |
| `\bGameObject\.Find\b` | `SceneReferenceService.TryGetTransform` | 🟠 Major |
| `\bGameObject\.FindWithTag\b` | 同上 | 🟠 Major |
| `\bFindObjectOfType\b` / `\bFindObjectsOfType\b` | `ctx.Get<T>()` 或 Provider | 🟠 Major（`UIMgr.InjectSceneModels` 等框架内豁免） |
| `\bSceneManager\.LoadScene\b` | `YSceneManager.SwitchScene` | 🟠 Major |
| `MonoBehaviour.*StartCoroutine\(` 在业务服务 | `ICoroutineRunner.Run` | 🟠 Major |
| `async\s+Task` / `\bawait\b` | 项目不用，改协程 | 🟠 Major |
| `throw new (Exception\|InvalidOperationException\|ArgumentException)` | `Debug.LogError + 安全 fallback` | 🟠 Major |
| `Camera\.main` 在 `Tick`/`Update`/`OnHover` 等热路径 | `Init` 缓存 | 🟠 Major |
| `LayerMask\.NameToLayer` 在热路径 | `Init` 缓存 | 🟠 Major |
| `GetComponent<` 在 `Update`/`Tick`/事件回调 | `OnLoad` 缓存 | 🟠 Major |

UIMgr / FlyTextMgr 等**框架内**对 `FindObjectsOfType` 的使用是已知约定（注入场景模型），不要标。

### 2.4 [🟠 Major] 性能 / GC（按代码规范 §9）

逐个 `Update` / `Tick` / `FixedTick` / `LateTick` / 高频回调（`OnHover` / Timer 1Hz 以上）扫：

- 闭包捕获外部变量（lambda 内引用 `this` 之外的临时变量）→ 每帧分配。
- `new List<>` / `new Dictionary<>` / `new <T>[]` → 改为字段复用 + `Clear()`。
- 字符串拼接 `+` 或插值 `$"..."` → 热路径用 `StringBuilder`；非热路径但每帧执行也算。
- `Debug.Log*` 在每帧路径 → 直接 Major。
- `int` → `object` 装箱（`Dictionary<Enum, object>` 当 value、`object[] params` 形参）。
- `Instantiate` / `Object.Destroy` 在循环 / 帧路径 → 应走 `ObjectPool` / `DataObjPool`。
- 协程 `while(true) yield return null` → 应改 `ITickable`。
- 读 `Time.deltaTime` 而非用 `Tick(dt)` 形参 → Minor 但常见。

### 2.5 [🟡 Minor] 命名 / 文件结构 / 注释

按代码规范 §1 / §2 / §3：

- 文件名 ≠ 主类名（除已知例外如 `SoundTypes.cs` 含多类型）→ Minor。
- 业务类带了 `Y` / `YOTO` / `Got` 前缀（应无前缀）→ Minor。
- 私有字段命名风格在同一文件内不一致（`_a` 与 `b` 混用）→ Minor。
- `public` 字段非 Inspector 用途 → 改 `[SerializeField] private` 或 property。
- `using` 顺序混乱（System → 第三方/Unity → 自定义）→ Nit。
- 类内成员顺序混乱（常量/字段/属性/Init/方法）→ Nit。
- 注释噪音：复述代码、记录历史、TODO 无负责人/日期 → Minor。
- 缺关键 `<summary>`：服务对外契约方法、非显然不变量、特殊 Unity quirk → Minor。

### 2.6 [🔴/🟠] 架构合理性

主观度高的一类，**只在确实有可观察问题时报，不为架构而架构**：

- **God Class**：单文件 >500 行 + 职责清单 >5 个 → 🟠（建议拆 Ctrl 子组件，参考 `SettingPanel` + `BaseSettingCtrl` 模式）。
- **数据/逻辑混合**：`UIPageBase` 子类内大段业务计算（>30 行非 UI 逻辑）→ 🟠（应抽到 Manager / Service）。
- **重复代码**：≥3 处复制粘贴的初始化 / 订阅模式 → 🟠（抽 helper / 基类）。
- **硬编码字符串**：资源路径 / 配表 id / 事件 key 字符串字面量散落 → 🟡（抽常量类）。
- **错误吞掉**：API 失败回调 `null` 但调用方未判空 → 🔴（潜在 NullReferenceException）。
- **`MonoBehaviour` 滥用**：业务服务挂 `MonoBehaviour` 而非 `IGameService`（代码规范 §10.2）→ 🟠。
- **同名/同义重复成员**：如 `Unload()` 与 `Shutdown()` 同时存在做相似事 → 🟡（合并）。
- **死代码**：定义了但全仓 0 引用的 public 类型 / 方法（用 Grep `<typeName>` 跨仓库）→ 🟡（删除）。
- **依赖隐式获取**：`GameLoop.Instance.Ctx.Get<T>()` 在热路径 / 大量出现 → 🟡（建议在 `Init` 缓存或注入）。

### 2.7 [🔴 Critical] 行为正确性（眼力可及）

- 数组/索引越界（看到 `list[i]` 但循环上界明显错）。
- 空引用风险（接口返回 `null` 文档明示，调用方未判空）。
- 异步回调内访问已销毁对象（`LoadHandleAsync` 回调里 `if (this != null)` 缺失，且对象生命周期短）。
- 事件循环（A 触发 B、B 又触发 A 的同步路径）。

不要凭想象推演罕见 race；只报"读代码就明显看出会出问题"的。

### 2.8 [🔵 Nit] 仅模式化才报

- 风格统一性（多处违反才有意义；单处不报）。
- 行长 / 空白 / 大括号位置（不报，编辑器能自动）。

**Nit 默认不报**，除非用户明确要求"包括细节"。

## Step 3 · 严重度分类与去重

把 Step 2 收集的问题做两件事：

### 3.1 分类

```
🔴 Critical  - 编译错误风险 / 资源/事件泄漏 / 依赖方向破坏 / 明显 NRE
🟠 Major     - 性能问题（每帧 GC）/ 违禁 API / 架构不合理 / 错误处理缺失
🟡 Minor     - 命名 / 文件结构 / 重复代码 / 硬编码 / 死代码
🔵 Nit       - 风格细节（默认不报）
```

### 3.2 去重 / 聚合

- 同一根因散布多处 → 聚合为一条，列代表性 3~5 个 file:line 例子，标"等共 N 处"。
- 同一文件多个相关问题 → 合并为该文件一条 entry，分点列。

### 3.3 判断是否需要写文档

**不写文档的情形**：

- 全部范围内 Critical = 0 且 Major = 0 且 Minor ≤ 2 且 Nit 全部不报。
- 且没有架构层面的建议。

→ 直接在对话里短答："审核通过 ✅，<scope> 未发现需要优化的问题（已扫 N 个文件，覆盖 7 类检查）。"

如果只有 1~2 条 Minor，**也可以**口头说一句而不写文档（例："审核基本通过，仅一处 Minor：`Foo.cs:42` 的私有字段风格与同文件其他字段不一致（`_a` vs `b`）。是否要为此单独出报告？"）。让用户决定要不要写正式文档。

**写文档的情形**：

- 任意 Critical 或 Major > 0；或
- Minor ≥ 3；或
- 用户明确要求"出报告"。

## Step 4 · 生成优化分析报告

### 4.1 路径与命名

- 根目录：`C:\UnityProject\YFramework\代码优化规划\`
- 文件名：`<Scope>-Review-v<n>.md`，初版 `v1`。
  - `Scope` 用 PascalCase / ASCII，反映评审范围：
    - 单文件：`StartPanel-Review-v1.md`
    - 目录/模块：`Combat-Review-v1.md` / `GamePlayUI-Review-v1.md`
    - diff 范围：`Diff-<branch>-Review-v1.md` 或 `Diff-2026-05-09-Review-v1.md`
    - 全 GamePlay：`AllGamePlay-Review-v1.md`
- 同 scope 多次评审：旧版保留并标 `status: Resolved` / `Stale`，新版 `links.replaces` 指回。

### 4.2 frontmatter

```yaml
---
id: <文件名去 .md>
title: <Scope> 代码评审 v1
type: CodeReview
status: Draft                                    # Draft → Reviewed → InProgress → Resolved → Stale
reviewer: <执行评审者，本 skill 默认填 "Claude (code-review skill)">
created: <today, YYYY-MM-DD>
updated: <today, YYYY-MM-DD>
version: 0.1
scope:
  type: <files | dir | diff | module>
  paths: [<列出所有评审文件相对路径>]
  base_ref: <仅 type=diff 时填，如 main / commit hash>
summary:
  critical: <count>
  major: <count>
  minor: <count>
  nit: <count>
links:
  related: []                                    # 关联代码规划 / 其他评审
---
```

### 4.3 正文章节

```markdown
## 1. 评审概览

- **范围**：<一句话描述>，共 N 个文件、约 M 行业务代码。
- **结论**：🔴 X 项 Critical / 🟠 Y 项 Major / 🟡 Z 项 Minor。
- **整体判断**：一句话——例如"模块基本健康，主要风险集中在事件订阅未配对（3 处 Critical）"或"架构合理但性能在 Update 路径有系统性 GC 分配"。

## 2. Critical 问题（必须修，否则有泄漏 / 崩溃风险）

### 🔴 C1. <一句话标题>

- **位置**：`相对路径:行号`（多处时列 3~5 个代表，标"等共 N 处"）
- **类别**：生命周期对称 / 依赖方向 / 行为正确性
- **现状**：

  ```csharp
  // 现有代码片段（≤10 行）
  ```

- **问题**：解释为什么这是 bug / 风险（**与规范条款挂钩**：例如"代码规范 §5：订阅与反订阅必须配对"）。
- **修复建议**：

  ```csharp
  // 建议改法（≤10 行）
  ```

- **影响面**：哪些功能会受影响、需要回归测试什么。

### 🔴 C2. ...

## 3. Major 问题（强烈建议修，影响性能 / 架构 / 可维护性）

### 🟠 M1. <标题>
（同上结构）

## 4. Minor 问题（值得修但不阻塞）

### 🟡 m1. <标题>
（可简化结构：位置 / 现状 / 建议 一段话即可）

## 5. 优化执行计划

按建议修复顺序排列（被依赖的先做）：

| 序 | 问题 | 估时 | 依赖 | 验证方式 |
|---|---|---|---|---|
| 1 | C1 修复事件订阅 | 30min | 无 | 跑 GameStart → 进入 GameMain → 退出，Console 无 NRE |
| 2 | C2 ... | 15min | 1 | ... |
| 3 | M1 ... | 1h | 无 | ... |

## 6. 不修建议（明确 won't fix 的项）

- 旧 `Got*` 前缀类名：已在 [项目规范 §8 已知改进项] 列管，不在本次评审范围内一次性处理。
- ... 其他主动放过的项及理由。

## 7. 风险与备注

- 修复 C2 时若改动 `EventMgr` 调用签名，会牵连 `XxxPanel.cs` 的订阅；建议同 PR 提交。
- ...

## 8. 变更记录

- v0.1 - <today> - <reviewer> - 初版评审
```

### 4.4 强制不变量

- **每条问题挂规范条款**：写"违反代码规范 §X.Y"或"违反项目规范 §X.Y"，不要凭"经验之谈"扣帽子。规范没说的不报。
- **每条 Critical / Major 必须有"现状代码片段"+"建议代码片段"**（≤10 行）。无代码示例的修复方案 = 没修。
- **不写完整代码**：建议是"伪代码 / 改动方向"，不是 PR diff。具体改由 code-generation 或人工执行。
- **不擅自修代码**：本 skill 严格只读。
- **§5 优化执行计划必须有**：让"修哪些"和"按什么顺序"明确。
- **§6 不修建议至少写 1 条或显式写"无"**：表明评审者考虑过取舍。

### 4.5 报告字数控制

- 单份报告建议 ≤500 行。超过 → 拆分（按子目录 / 按问题类型）。
- Critical 详细写、Major 中等、Minor 简写。**不要给 Minor 写大段代码示例。**

## Step 5 · 交付报告

写完文档（或决定不写文档时），输出短摘要：

### 写了文档时

```
## 代码评审完成 — <Scope>

📄 报告：<绝对路径>

### 严重度分布
- 🔴 Critical: N（建议立刻修）
- 🟠 Major: M
- 🟡 Minor: K

### Top 3 风险（最先修）
1. <C1 一句话>
2. <C2 一句话>
3. <M1 一句话>

### 备注
- 修复方案见报告 §5 执行计划，**本 skill 不执行修复**。
- 修复路径由用户决定：A) 人工逐条改；B) 把报告作为 code-generation 的输入；C) 仅做 Critical，Major 留作 tech-debt。
- 修复完后重审，全部 Resolved → 报告 status 设为 `Resolved`。
```

### 没写文档时

```
审核通过 ✅ — <Scope>

已扫描 N 个文件，覆盖：依赖方向 / 生命周期对称 / 违禁 API / 性能 GC / 架构 / 命名结构 / 行为正确性。
未发现需要优化的问题。

（如有 1~2 条 Minor，列在这里：可选）
```

## 严守红线

- **只读不改**。本 skill 不调 Edit / Write 在 `client/` 下任何 `.cs` 文件。**只允许**写 `代码优化规划/*.md`。
- **不为写而写**。审核通过就明确说通过；不要为了交差硬挑毛病。
- **不脱离规范**。每条问题必须挂上《项目规范》/《模块规范》/《代码规范》的具体条款；纯主观偏好（除非用户问"你建议"）一律不报。
- **不超出范围**。用户指定 `Combat` 模块就只看 Combat；旁边发现的问题最多在 §6 备注一行"另注意到 X 处 Y 问题，建议另开 review"，不展开。
- **不发明问题**。不确定的疑问不上报为问题；要么 Read 更多上下文确认、要么以"建议人工复查"形式列在备注。
- **不混淆事实与建议**。"代码做了 X" vs "我认为应该改为 Y" 必须分清。
- **始终引用 file:line**。问题位置必须可定位；笼统说"某些地方 GC 太多"不是合格的 review。
- **始终中文撰写**，与项目其他文档一致；代码片段保持原始（含英文标识符）。
- **始终用绝对日期** `YYYY-MM-DD`。
