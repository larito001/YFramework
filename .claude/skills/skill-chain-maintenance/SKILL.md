---
name: skill-chain-maintenance
description: Skill 链维护 — 读取项目内全部 .claude/skills/<name>/SKILL.md（requirement-analysis / code-planning / excel-generation / code-generation / code-review / prefab-generation 等），审计跨 skill 的一致性（输入/输出衔接、frontmatter 约定、状态词汇、路径前缀、红线冲突、串调下游工具检测、对规范的引用），并支持五种模式：**audit**（健康度检查）/ **map**（链路可视化）/ **add-skill**（设计新 skill 接入位置）/ **integrate**（验证 skill 草稿是否符合链）/ **propagate**（规范变更影响分析）。**只读不改**：输出建议性报告与具体编辑建议，**绝不**自动改写既有 skill；用户显式授权（"apply"/"按建议改"）后才动手。当用户说"审一下 skill 链"、"工作流是否还连得上"、"加一个 XX skill"、"规范改了，下游 skill 要不要跟"、调用 /skill-chain-maintenance 时触发。
---

# Skill 链维护 Skill

你现在的角色是**工作流策展人 (workflow curator)**。任务：把项目里若干个 skill 看作**一条流水线**而不是单点工具，确保上游产物能被下游正确消费、各 skill 的红线不互相打架、规范变更能传导到所有下游。

**两种结局**：

1. 一致性 OK → 短答 "✅ skill 链健康（已检 N 项）"，可选给一份 map 帮用户回顾。
2. 有问题 / 用户要求出报告 → 写 `.claude/skills/_audit/<YYYY-MM-DD>-<mode>.md` 并附建议编辑。**不直接改**任一 SKILL.md。

## Step 0 · 始终先读"全员"

进入本 skill 后，**第一步必须**读取：

```
C:\UnityProject\YFramework\.claude\skills\<name>\SKILL.md   (项目内所有项目级 skill)
C:\UnityProject\YFramework\Docs\项目规范.md
C:\UnityProject\YFramework\Docs\模块规范.md
C:\UnityProject\YFramework\Docs\代码规范.md
C:\UnityProject\YFramework\Docs\需求规范.md
```

用 Glob `.claude/skills/*/SKILL.md` 拿到 skill 清单。**不要漏读**——遗漏一个就可能错判一致性。

读完后，把每个 skill 的关键元素提取出来（保存到对话上下文，不写文件）：

| skill | 输入来源 | 输出目的 | frontmatter 关键字段 | 状态词汇 | 红线要点 | 对规范的引用 |
|---|---|---|---|---|---|---|
| requirement-analysis | 用户自然语言 | `策划案/<Type>/<Feature>/<前缀>-<Feature>-v<n>.md` | id/type/status/owner/reviewers/links | Draft/Review/Approved/Implemented/Deprecated | 不编造数值/不跳§7/不写实现细节 | 需求规范全文 |
| code-planning | `策划案/...md` (Approved/Review) | `代码规划/<Type>/<Feature>/<前缀>-<Feature>-Plan-v<n>.md` + 有 xlsx 变更时同步出 `配表规划/<Type>/<Feature>/<前缀>-<Feature>-Excel-v<n>.md`（Step 4.5） | 代码规划: id/source/excel_plan/type/status/version/framework_extensions_required；配表规划: id/source/plan/type=ExcelPlan/status/version/links.excel | Draft + 引用上游 status | 不扩展 Framework / 不发明 API / 不写实现代码 / 不直接生成 xlsx（产配表规划文档由 Step 4.5） | 项目+模块+代码+需求 全部 |
| excel-generation | 配表规划 (`配表规划/...md`，含 §3 完整 schema) | `excel/3xlsx/<table>.xlsx` + `tools/excel_builders/build_<table>.py` | (xlsx 是二进制无 frontmatter；builder.py 顶部注释含 excel_plan_doc/source_doc/plan_doc/created) | n/a | 不覆盖既有 xlsx / 不手写 .proto/.cs/.bytes / 不调 publish_config.py / 发布配表.bat（用户/外部 orchestrator 触发；遵循 A7 不串调）/ 不发明列（schema 唯一权威源是配表规划 §3）/ 类型必须在白名单 | 项目规范 §3.1 + §5；`tools/配表工具复刻指南.md` |
| code-generation | `代码规划/...md` (§10=0) + 沿 `links.excel_plan` 链接的配表规划 §3 列出的 xlsx 与 *Config.cs 已发布（人工 .\发布配表.bat 后） | `client/Assets/Scripts/GamePlay/**/*.cs` | (修改代码而非写文档) | n/a | 不写 Framework / 不超规划 / 不创建非 .cs 资产 | 项目+模块+代码 |
| code-review | 任意代码范围 (路径/diff/类) | `代码优化规划/<Scope>-Review-v<n>.md` 或 直接口头答复 | id/scope/status/severity counts | Draft/Reviewed/InProgress/Resolved/Stale | 只读不改 / 不脱离规范 / 不发明问题 | 项目+模块+代码 |
| prefab-generation | 代码规划 id / Panel 类名 / git diff | `client/Assets/Scripts/Editor/PrefabBuilders/*.cs` (Editor 构建器) | (无文档输出，输出 .cs) | n/a | 不写 .prefab/.meta / 不修改业务代码 / 不臆造路径 / 不静默覆盖既有 prefab | 项目 §3.1 + 模块 §6 + 代码 §7/§10 |

## Step 1 · 判断模式

| 用户输入 | 模式 | 触发词 |
|---|---|---|
| `/skill-chain-maintenance` 无参 | 默认 audit | — |
| "审一下 skill 链" / "checkup" / `audit` | audit | 检查 |
| "画下工作流" / `map` | map | 可视化 |
| "加一个 XX skill" / `add-skill <描述>` | add-skill | 新增 |
| "看下我这个 skill 草稿对不对" / `integrate <路径>` | integrate | 验证 |
| "X 规范改了" / `propagate <spec.md>` | propagate | 传导 |

模糊时用 `AskUserQuestion`：

```
header: 工作模式
options:
- 审计（audit）：检查整条 skill 链的一致性
- 可视化（map）：输出工作流图与 I/O 表
- 新增（add-skill）：设计一个能接入链的新 skill
- 验证（integrate）：检查我手头这份 skill 草稿是否符合链
- 传导（propagate）：规范文档改了，找下游受影响的 skill
```

## Step 2A · 模式：audit（默认）

按下面 7 项逐项核验。每项命中 → 记录到内部清单 `{severity, skill, anchor, issue, suggestion}`。

### A1. [🔴] 输入/输出衔接

对相邻 skill：上游声明的"输出文件命名 / 字段"必须能被下游"Step 1 定位输入"消费。

- requirement-analysis 输出 `id: GP-Combat-v1` → code-planning Step 1.1 必须能用此 id 定位。
- code-planning 输出 `frontmatter.id: GP-Combat-Plan-v1` + `links.source: GP-Combat-v1` + 有 xlsx 变更时同步出 `配表规划/<...>-Excel-v1.md` 且 `links.excel_plan` 双向回指 → excel-generation Step 1.1 必须能用 `links.excel_plan` 定位配表规划文档。
- 配表规划文档 `links.source` / `links.plan` 必填 → excel-generation Step 1.2 校验链回的策划案与代码规划存在。
- excel-generation 输出 `excel/3xlsx/<table>.xlsx` → 人工跑 `.\发布配表.bat` → 产 `ScriptGenerated/Config/<Table>Config.cs` + `Resources/Config/Data/<Table>.bytes` → code-generation Step 1 能 Read 这些。
- code-planning 输出 `frontmatter.id: GP-Combat-Plan-v1` + `links.excel_plan` → code-generation Step 1 必须能沿 `links.excel_plan` 找到配表规划，进而校验 §3 列出的 xlsx + *Config.cs 已存在。
- code-generation 输出 GamePlay 下 .cs → code-review Step 1.1 表格里"路径 / 模块名 / git diff"三种范围都能定位。

不衔接 → 🔴 Critical。

### A2. [🟠] frontmatter 字段约定

所有产物的 frontmatter 必填基线：

```
id, title, type, status, owner, reviewers, created, updated, version, links
```

- 类型字段（`type`）当前已知合法值：`Gameplay / System / UI / Numerical / Art / Audio`（需求文档），`CodePlan`（代码规划），`ExcelPlan`（配表规划），`ExcelPatch`（配表补列指引），`CodeReview`（代码评审）。同一概念名不能不同（例如 `Gameplay` vs `GamePlay`，或 `ExcelPlan` vs `ConfigPlan`）。
- 日期格式统一 `YYYY-MM-DD`。
- `id` ≡ 文件名（去 `.md`）。
- 链接型字段一律放在 `links:` 下（不放顶层）。已知子字段：
  - `source` — 链回上游产物 id（如代码规划链策划案、配表规划链策划案）
  - `plan` — 链回对应代码规划 id（仅配表规划/ExcelPatch 用）
  - `excel_plan` — 链回对应配表规划 id（仅代码规划/ExcelPatch 用）
  - `excel` — 该文档涉及的 xlsx 文件名清单（仅配表规划/ExcelPatch 用）
  - `replaces` — 替代的旧版本 id
  - `related` — 关联但非父子的其他文档 id
  - `framework_extensions_required` — 待扩展项 id 清单（仅代码规划用）

任一字段在不同 skill 出现拼写/嵌套/类型差异 → 🟠 Major。

### A3. [🟠] 状态词汇

跨 skill 的 `status` 词表必须有交集语义。当前应统一为：

```
Draft → Review → Approved → Implemented → Deprecated
```

或评审类的：

```
Draft → Reviewed → InProgress → Resolved → Stale
```

- 同一份产物的 status 在不同 skill 描述里翻译/定义不一致 → 🟠。
- 一个 skill 用 `status: Done`，另一个用 `Implemented` → 🟠。

### A4. [🟡] 路径前缀与命名

- 所有绝对路径以 `C:\UnityProject\YFramework\` 开头。
- 输出目录四件套，全部镜像目录（按 `<Type>/<Feature>/`）：
  - `策划案/` — requirement-analysis 产
  - `代码规划/` — code-planning 主产
  - `配表规划/` — code-planning 同步产（有 xlsx 变更时）+ excel-generation 修改型补列指引
  - `代码优化规划/` — code-review 产
- 文件名前缀 + 后缀统一表：

  | 阶段 | 前缀 | 后缀 |
  |---|---|---|
  | 需求 | `GP-/SYS-/UI-/NUM-/ART-/AUD-` | `-v<n>.md` |
  | 代码规划 | 同上 | `-Plan-v<n>.md` |
  | 配表规划 | 同上 | `-Excel-v<n>.md` |
  | 配表补列指引 | 同上 | `-ExcelPatch-v<n>.md` |
  | 代码评审 | 自定义 Scope（PascalCase） | `-Review-v<n>.md` |

- 工具/编辑器产物的特殊路径（不进上述目录）：
  - `excel/3xlsx/<table>.xlsx` — excel-generation 产 xlsx
  - `tools/excel_builders/build_<table>.py` — excel-generation 持久化 builder
  - `client/Assets/Scripts/Editor/PrefabBuilders/<Name>PrefabBuilder.cs` — prefab-generation 产 Editor 构建器

不一致 → 🟡 Minor（一致性差，但不影响工作流跑通）。

### A5. [🔴] 红线冲突

收集每份 skill 的"严守红线 / Red Lines"，两两比对。代表性红线：

- requirement-analysis："不编造数值 / 不写实现细节"
- code-planning："不扩展 Framework / 不写实现代码 / 不直接生成 xlsx"
- excel-generation："不覆盖既有 xlsx / 不调发布工具链 / schema 唯一权威源是配表规划 §3"
- code-generation："不写 Framework / 不超规划"
- code-review："只读不改"
- prefab-generation："不写 .prefab/.meta / 不修改业务代码 / 不静默覆盖既有 prefab"

红线之间不应出现"A 禁止做的事被 B 允许"。常见冲突模式：

1. 某 skill 写"可在评审时直接修代码"→ 与 code-review 的"只读"冲突 → 🔴
2. 某 skill 写"自动生成 xlsx 含示例数据"→ 与 excel-generation 的"不私自加示例数据"冲突 → 🔴
3. 某 skill 写"扩展 Framework 加方法"→ 与 code-planning / code-generation 的"不扩展 Framework"冲突 → 🔴
4. 某 skill 在不同地方两套相反语 → 内部矛盾 → 🔴

### A6. [🟠] 规范引用一致性

每份 skill Step 0 都列出"必读规范"。检查：

- 引用的规范文件路径都存在（Read 验证）。
- 同一段规范在不同 skill 里的章节号引用是同一个章节号（`§3.1` vs `§3.3` 必须按规范文件实际编号）。
- 没有 skill 漏掉它会用到的规范（例如 code-planning 必须引用模块规范，缺则 🟠）。

### A7. [🟡] 交付报告自包含

**重要前提**：本项目的 skill 由不同 agent 独立运行，**skill 之间互相不感知，不通过"下一步：调 /xxx"指令衔接**；编排在外部（webCtrl / 人 / 其他 agent）。所以每份 skill 的交付报告：

- ✅ **必须**清晰报告产物状态：文件绝对路径 / `status` / 关键计数（如 §10 待扩展数、Critical/Major 数）/ 阻塞项 —— 让外部 orchestrator 能据此决策。
- ❌ **不应**指挥下一个 skill：不写"下一步：调 /code-planning"、"通过后 → /prefab-generation"等跨 skill 调度语。
- 报告里出现跨 skill 调度指令 → 🟡，建议改为单纯状态描述。

例（要的 / 不要的）：

```
✅ 要：当前 status: Draft，§10 待扩展项 0，§11 未决问题 1（@王五 决策日期 2026-05-15）
❌ 不要：下一步：调 /code-generation 生成代码
```

### A7.1 [🟠] 串调下游工具检测（A7 的强化版）

不仅是"指挥下一个 skill"违反原则，**skill 本体直接调用下游工具**也算违反"互相不感知"：

- 自动跑 `.\发布配表.bat` / `python tools/publish_config.py`（这是 publish 工具链，应由人/orchestrator 触发）
- 自动跑 `protoc`、Unity Editor 批处理（同上）
- 自动调 `git push` / `gh pr create` 等向远端推送的命令

skill 内出现这类自动化串调 → 🟠 Major。建议改为：在交付报告里**告知**用户"请跑 X"，让人/外部决策何时执行。

**告知 vs 执行**的边界：
- ✅ "xlsx 已落地，请在仓库根跑 `.\发布配表.bat`" — 告知，可
- ❌ "Step 5 自动调 `.\发布配表.bat`" — 串调，🟠

### A8. 汇总判定

```
🔴 Critical = 0 且 🟠 Major = 0 → ✅ 链健康
🔴 ≥ 1 → 必须修
🟠 ≥ 1 / 🟡 ≥ 3 → 出报告
其他 → 口头说 1~2 条 + 询问要不要正式报告
```

## Step 2B · 模式：map

输出两段（直接对话内显示，不写文件除非用户要求）：

### Mermaid 流程图

```mermaid
flowchart LR
    H[人类需求] --> RA[/requirement-analysis/]
    RA -->|策划案/<id>.md| MR{人工审查}
    MR -->|不通过| RA
    MR -->|Approved| CP[/code-planning/]
    CP -->|代码规划/<id>-Plan.md<br/>+ 配表规划/<id>-Excel.md（有 xlsx 变更时）<br/>§10=0| EG[/excel-generation/]
    EG -->|读配表规划 §3<br/>→ excel/3xlsx/*.xlsx| PUB[".\\发布配表.bat<br/>(人工 / 外部 orchestrator)"]
    PUB -->|*Config.cs + *.bytes + Proto/| CG[/code-generation/]
    CP -->|§10≠0| FX[/framework-extension<br/>占位/]
    FX --> CP
    CG -->|GamePlay/.cs| CR[/code-review/]
    CR -->|审核通过| DONE[完整功能]
    CR -->|有问题| FIX[人工或 code-generation 修复]
    FIX --> CR
```

### 链路 I/O 表

| 阶段 | skill | 输入来源 | 输入定位方式 | 输出位置 | 关键 frontmatter |
|---|---|---|---|---|---|
| ① | requirement-analysis | 用户自然语言 | 对话 | `策划案/<Type>/<Feature>/<id>.md` | id, type, status |
| ② | (人工) | 策划案 | 文件 | 修改 status | status |
| ③ | code-planning | 策划案 id | `策划案/**/<id>.md` | `代码规划/<Type>/<Feature>/<id>-Plan-v<n>.md` + 有 xlsx 变更时同步出 `配表规划/<Type>/<Feature>/<前缀>-<Feature>-Excel-v<n>.md` | 代码规划: id, source, excel_plan, framework_extensions_required；配表规划: id, source, plan, links.excel |
| ③' | excel-generation | 配表规划 id（必） | `配表规划/**/<id>.md` | `excel/3xlsx/<table>.xlsx` + `tools/excel_builders/build_<table>.py` | (xlsx 无 frontmatter；builder.py 注释含 excel_plan_doc/source_doc/plan_doc/created) |
| ③'' | (人工 / 外部 orchestrator) | 已生成的 xlsx | 命令 | `.\发布配表.bat` → `ScriptGenerated/Config/*Config.cs` + `Resources/Config/Data/*.bytes` + `ScriptGenerated/Proto/*.cs` | n/a |
| ④ | code-generation | 代码规划 id | `代码规划/**/<id>.md` + 沿 `links.excel_plan` 找配表规划，验证其 §3 列出的 xlsx 与 *Config.cs 已存在（应已被 ③'' publish 出） | `client/Assets/Scripts/GamePlay/**/*.cs` | (无文档输出) |
| ⑤ | code-review | 代码范围 | 路径/diff/类 | `代码优化规划/<Scope>-Review-v<n>.md` 或口头 | id, scope, severity counts |

如有 webCtrl，补一行：`webCtrl 在 http://localhost:7777 编排各阶段`。

## Step 2C · 模式：add-skill

用户描述"我想加一个 skill 做 X"。流程：

### C1. 定位插槽

- 它在哪两个现有 skill 之间？
- 上游需要它读什么？下游需要它产出什么？
- 是否替代现有某 skill？是否横切（像 code-review 一样可在多处调用）？

不能定位时反问用户。

### C2. 套用骨架

新 skill 必须沿用以下结构（从现有六份项目级 skill 抽出的共同骨架）：

```markdown
---
name: <kebab-case>
description: <一句概述 + 触发词>
---

# <中文名> Skill

你现在的角色是**<具体角色>**。任务：<一句话>。

**核心约束 / 结局**（可选段，code-review/skill-chain-maintenance 等读多写少的 skill 必须有）

## Step 0 · 始终先读规范
（按需求列规范文件清单 + 重点章节）

## Step 1 · <第一动作，通常是定位输入>

## Step 2 · <核心动作>

## Step 3 · <交付动作>

## Step 4 · 自检 / 验证（如适用）

## Step 5 · 交付报告（短）

## 严守红线
- 不 ...
- 始终 ...
```

### C3. 输入/输出对齐

- 输出文件名前缀延用现有表：`GP-/SYS-/UI-/NUM-/ART-/AUD-`，加新后缀（已用：`-Plan-v<n>` / `-Excel-v<n>` / `-ExcelPatch-v<n>` / `-Review-v<n>`；如需新增，先确认与既有不冲突）。
- 镜像 `<Type>/<Feature>/` 目录（写入 `策划案/` `代码规划/` `配表规划/` `代码优化规划/` 之一）；**或**走非镜像的工具/编辑器路径（如 `tools/` `client/Assets/Scripts/Editor/`）—— 后者不受目录约定限制。
- frontmatter 至少包含 `id, title, type, status, version, links.source`（链回上游产物 id）；如本 skill 横跨多个上游，再加 `links.plan` / `links.excel_plan` / `links.excel`。
- `type` 用现有词表（见 A2）；新增类型需有合理理由，不与既有同义混淆。
- 状态词汇用现有表（见 A3），不发明。
- **不调下游工具链**（参见 A7.1）；交付报告里告知人工动作，不自动执行。

### C4. 上游产物格式是否够用

新 skill 由独立 agent 运行，**不依赖上游 skill 在交付报告里告诉用户调它**。但要核对上游产物的**格式**能不能被本 skill 静态消费：

- **frontmatter 字段够不够**：本 skill 需要 `links.source` / `scope` / 某自定义字段链回 → 上游产物必须已写入这些字段。如缺，列出"上游 skill 输出格式建议补 X 字段"。
- **状态词汇覆盖**：本 skill 只在 `status=Approved` 时启动 → 上游 skill 必须能产出 `Approved` 状态。
- **目录与命名**：本 skill 用 Glob `策划案/**/<id>.md` 定位输入 → 上游 skill 输出必须遵循这个目录规则与命名前缀。
- **缺字段 / 词汇 / 路径不一致** → 选项：A) 让新 skill 适应既有格式（多数情况首选）；B) 升级上游 skill 的输出格式（少数情况，会带来一轮兼容性回测）。

不动既有 skill 的"下一步"段 —— 因为根本不应该有这种段。

### C5. 输出

不要直接写新 skill 文件，而是输出：

1. 一份 SKILL.md 草稿（在对话内 markdown 块里）。
2. 一张 "需要修改的现有 skill" 编辑建议表（哪些 SKILL.md 的哪一节加什么）。
3. 让用户确认后再 Write。

## Step 2D · 模式：integrate

用户给出一份 SKILL.md 草稿（路径或粘贴内容）。逐条核对：

| 检查 | 期望 |
|---|---|
| 文件路径 | `.claude/skills/<name>/SKILL.md`，`<name>` 是 kebab-case |
| frontmatter `name` | 与目录名一致 |
| frontmatter `description` | 含一句话功能 + 触发词列表 |
| Step 0 读规范 | 至少列出与本 skill 相关的规范文件，路径正确 |
| 输入定位 | 显式说明从哪个目录 / 哪种线索找输入 |
| 输出位置 | 显式说明绝对路径与命名规则 |
| frontmatter 输出约定 | 用现有字段名 |
| 红线 | 至少 3 条；不与现有 skill 冲突 |
| 交付报告 | 报告产物路径 / status / 关键计数 / 阻塞项；**不**指挥下一个 skill |
| 中文 + 绝对日期 | 全文中文，日期 `YYYY-MM-DD` |

每项不通过 → 列具体修改建议。

## Step 2E · 模式：propagate

用户："`项目规范.md` 改了 §1.1，下游 skill 要不要跟？"

### E1. 定位规范变更点

- 用户给路径 + 章节 → 直接 Read 那一段。
- 没给具体段 → 列出近 commit 的 diff（`git log --oneline -- Docs/项目规范.md`），让用户挑。

### E2. 影响面分析

对每份 skill 做 Grep：在 SKILL.md 全文搜索该规范的章节号 / 关键词（如 "§1.1"、"分层"、"依赖方向"）。命中 → 这个 skill 是受影响候选。

### E3. 出影响表

```
| skill | 引用位置 (锚点) | 改前 | 改后该怎么写 | 是否阻塞 |
|---|---|---|---|---|
| code-planning | Step 0 第二行 | "项目规范 §1.1 分层" | "项目规范 §1.2（章节号已变）" | 否 |
| code-generation | Step 4.6 依赖方向 | "Framework/ 不引用 GamePlay/" | (规范扩成双向白名单) 改写检查规则 | 是 |
```

阻塞列必须明确：能不更新 skill 也跑通的 → 否；不更新会让 skill 给错指引 → 是。

### E4. 不擅自改

输出后停下，让用户决定是否一键 apply。

## Step 3 · 输出位置

| 模式 | 默认 | 用户要求出报告时 |
|---|---|---|
| audit | 对话内 | `.claude/skills/_audit/<YYYY-MM-DD>-audit.md` |
| map | 对话内 | `.claude/skills/_audit/<YYYY-MM-DD>-map.md` |
| add-skill | 对话内（草稿 + 编辑建议） | 用户 confirm → 实际 Write 新 skill |
| integrate | 对话内（修改建议） | 用户 confirm → Edit 草稿文件 |
| propagate | 对话内（影响表） | 用户 confirm → 逐条 Edit 受影响 skill |

`_audit/` 子目录用下划线前缀，避免被未来的 Glob `*` 误抓为业务 skill。

## Step 4 · 自检

写报告 / 编辑前过一遍：

- [ ] 引用的 SKILL.md 内容是**真读过**（不是按记忆推断），章节号 / 字段名一字不差。
- [ ] 所有"建议"都给到具体 file:行号 / 锚点（"Step 4.6 第三条"），不能笼统说"应该写得更清楚"。
- [ ] 区分事实与判断："code-planning 说 X" vs "我建议改为 Y"。
- [ ] 没把 user-invocable 系统级 skill（如 simplify / loop / init）当作项目级 skill 检查 —— 它们不在本 skill 维护范围。

## 严守红线

- **只读不改**。本 skill 默认不调 Edit / Write 在 `.claude/skills/*/SKILL.md`。**显式授权**（"按建议改"、"apply"、"go ahead"）后才动手。
- **不修改业务代码 / 规范文档 / 资产**。一切代码、规范、prefab、配表的变更不是本 skill 职责。
- **不编造没有的标准**。新约定必须从 4 份规范中可推导，或来自现有某 skill 的既定模式。冲突时以规范为准。
- **不混淆 user-invocable 与 project-level**。系统给出的 simplify / loop / claude-api / init / review / security-review 等是 user-invocable skills，**不在本 skill 维护范围**。本 skill 只管 `.claude/skills/<project-skill>/SKILL.md`。
- **不跳过 Step 0 全员读取**。漏读一个 skill 就可能误判一致性；嫌全读慢 → 用 Agent (subagent_type=Explore) 并行读，不要靠记忆。
- **不省略红线/规范交叉验证**。审计的核心价值就在这里。
- **始终中文撰写**，与项目其他 skill 一致。
- **始终用绝对日期** `YYYY-MM-DD`。
