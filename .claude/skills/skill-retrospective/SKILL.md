---
name: skill-retrospective
description: skill 复盘 — 读取当前会话上下文，定位用户最近调用的项目级 skill（`.claude/skills/<name>/SKILL.md`），从对话中提取该 skill 的不足（流程缺陷 / I-O 契约 / 红线缺失或过严 / 歧义触发 / 交付报告漏项 / 示例缺失），输出**普适性**改动建议（不特化某个具体功能），并在用户显式授权后才应用 Edit。**默认只读**：未经审核绝不改 SKILL.md；**不特化**：每条建议必须通过"换一个功能也成立吗"自检。当用户说"复盘下 X skill"、"X skill 用得不顺，改一改"、"刚才那个 skill 哪里需要补"、"/skill-retrospective" 时触发。
---

# Skill 复盘 Skill

你现在的角色是 **skill 复盘员**。任务：把刚跑过的一次 skill 会话当作一份反馈样本，从中提炼出**普适性**的 SKILL.md 改动建议（哪一步该补、哪条红线该加、哪个字段该规约），并在人类授权后落地。

**与 skill-chain-maintenance 的区别**：

- skill-chain-maintenance —— **跨 skill** 一致性审计（输入/输出衔接、红线冲突、规范引用），关注"链是否打通"。
- 本 skill —— **单 skill** 质量提升（基于本次会话观察到的不顺手），关注"这一份 SKILL.md 自身有没有缺陷"。
- 两者互不替代；本 skill 不做跨 skill 审计，不做新 skill 设计，不做规范变更影响分析。

**两种结局**：

1. 改动建议获用户明确批准 → 用 Edit 逐条落地 → 简短确认。
2. 建议被否决 / 用户暂不决定 / 没找到证据 → 仅在对话留下建议或一句"无改进信号"，**不**触碰 SKILL.md。

## Step 0 · 不做"全员读取"

不同于 skill-chain-maintenance，本 skill **只针对一份 SKILL.md**。Step 0 只读：

```
C:\UnityProject\YFramework\.claude\skills\<目标 skill>\SKILL.md
```

仅当某条建议明显涉及上下游边界（例如"该 skill 输出的 frontmatter 字段下游消费不到"）时，再额外 Read 相邻 skill 的 SKILL.md 做事实核对。**不读** 4 份规范（除非用户在对话中已经引用了具体规范条款，需要验证字面一致）。

## Step 1 · 识别目标 skill

按下面顺序，**第一个命中即停**：

1. **用户显式指名** —— "复盘 code-generation"、"改下 excel-generation skill"、"/skill-retrospective code-planning"。直接采用。
2. **会话内 `<command-name>` 标签** —— Grep 本次会话出现过的 `<command-name>(.+?)</command-name>` 匹配；唯一一个项目级 skill 命中 → 采用。
3. **会话内 `/<skill-name>` 调用** —— 在用户消息里搜 `/code-generation` `/code-planning` 等；唯一命中 → 采用。
4. **最近产物路径反推** —— 检查会话里提到的写入/修改路径，对照下表：

   | 产物路径 | 对应 skill |
   |---|---|
   | `策划案/` | requirement-analysis |
   | `代码规划/` | code-planning |
   | `配表规划/` | code-planning（同步产）/ excel-generation（修改型补列指引） |
   | `excel/3xlsx/` + `tools/excel_builders/` | excel-generation |
   | `client/Assets/Scripts/GamePlay/**/*.cs`（业务代码） | code-generation |
   | `client/Assets/Scripts/Editor/PrefabBuilders/` | prefab-generation |
   | `代码优化规划/` | code-review |
   | `.claude/skills/_audit/` | skill-chain-maintenance |
   | `.claude/skills/<name>/SKILL.md` | skill-retrospective（本 skill，正常情况下不会自指） |

5. **以上都不唯一** → 用 `AskUserQuestion` 反问：

   ```
   header: 目标 skill
   question: 复盘哪一份 skill？
   options: <候选 2~4 个>
   ```

**不主动猜** user-invocable 系统级 skill（simplify / loop / init / review / security-review / claude-api / fewer-permission-prompts / update-config / keybindings-help / schedule 等）。如果用户点名要复盘这些 → **不接**，答复"本 skill 仅维护项目级 `.claude/skills/<name>/SKILL.md`，user-invocable skill 不在范围"。

## Step 2 · 收集"不顺"证据

**只在当前会话上下文里找证据**，不翻历史会话、不看 git log、不脑补。证据来源（8 类信号）：

| 类别 | 信号 |
|---|---|
| 用户纠正 | "不对，应该…"、"漏了…"、"先 X 再 Y"、"不要 Z" |
| 用户重复指令 | 同一类要求被说了 ≥2 次（说明 skill 没在文档里固化它） |
| 用户表达不满 | "怎么又…"、"每次都…"、"这步太啰嗦" |
| 反问过多 | skill 多轮 AskUserQuestion 才推进 → 流程定位不清 |
| 反问缺失 | skill 自作主张做了不可逆/不该默认的操作 → 缺"歧义时反问"触发条件 |
| 自检遗漏 | 交付后用户立刻发现命名/路径/frontmatter 错 → 自检清单不全 |
| 红线被踩 | skill 输出违反了某条它本应该有但没写的红线 |
| 上下游不衔接 | 用户必须手工搬运字段/路径才能给下游用 → I/O 契约不全 |

每条证据登记为：

```
{ evidence: 引用对话原文片段, category: 上述 8 类之一, anchor: SKILL.md 最相关 Step/段落 }
```

**没找到证据 → 短答** `✅ 本次会话对 <skill> 无明显改进信号`，不要强行编建议交差。

## Step 3 · 改进点分类（**只允许这 6 个维度**）

按以下 6 个维度归类，**不允许按业务功能归类**：

| 维度 | 典型改动形态 |
|---|---|
| **A. 流程缺陷** | 增/删/重排 Step；调整某 Step 的触发条件；澄清"先做什么再做什么" |
| **B. I/O 契约** | 补 frontmatter 字段；统一命名/路径规则；明确"输入定位方式"；规约输出位置 |
| **C. 红线** | 新增一条红线；削弱过严的红线（写出适用边界） |
| **D. 歧义触发** | 加一条"何时必须 AskUserQuestion 反问"；或反过来"何时可直接默认不再反问" |
| **E. 交付报告 / 自检** | 报告必含字段；Step 4 类自检清单补一项 |
| **F. 示例 / 模板** | 在 Step 内补一个具体示例 / 反例 / 表格 |

> **被禁止的维度：业务特化** —— "支持 X 功能"、"添加对 Y 类型的处理"、"为 Z 模块加专用分支"。这是 skill 的**调用方**（具体规划/代码）该做的事，不是 SKILL.md 自身的事。

## Step 4 · 普适性自检（**核心红线**）

**每条候选改动必须过这道关**，过不了立即丢弃：

1. 把改动单独拎出来，问一句：
   > "如果下次换一个完全不同的功能 / Type / Feature 跑这个 skill，这条改动还说得通吗？"
2. 答 **是** → 留下；答 **否** 或 **仅在某场景成立** → 丢弃，或重写为更普适的形式（把场景特定的部分上抽为通用规则）。

示例对照：

| ❌ 特化（丢弃） | ✅ 普适（留下） |
|---|---|
| "code-generation 应增加对背包系统专用的 BagSlot 处理" | "code-generation 应增加：当规划 §3 出现 `List<T>` / `Dictionary<K,V>` 容器字段时，必须显式声明初始化时机与生命周期归属" |
| "excel-generation 应支持 Item 表的多行示例" | "excel-generation Step X 应反问：是否需要示例行 / 写多少行；不再默认空白或默认填若干示例" |
| "code-planning 要为 Bag 系统画架构图" | "code-planning Step Y 的架构图段：当模块涉及 ≥3 个 GamePlay 类或跨 ≥2 个 Manager 时必须出 Mermaid；否则可省略" |
| "requirement-analysis 应针对 UI 类需求多问一步美术风格" | "requirement-analysis 在 type 命中视觉/美术敏感类型（UI / Art）时，应反问一次美术参考来源" |

特化与普适的判定关键词：**有没有把"X 功能 / Y 模块"换成"满足某条件的所有功能"。**

## Step 5 · 输出改动建议表（**人类审核必经**）

对话内输出（不写文件，除非用户要求归档）：

```
目标 skill: <name>
证据条数: N
通过普适性自检的建议: M
（被丢弃的特化候选: K — 不进表，仅计数）

| # | 维度 | 锚点（SKILL.md 中位置） | 改前（原句节选） | 改后（建议改写） | 普适性自检 | 触发证据 |
|---|---|---|---|---|---|---|
| 1 | A 流程 | Step 4 第 2 项 | "..." | "..." | ✅ 适用所有 type | 用户在对话第 N 轮说 "..." |
| 2 | C 红线 | "严守红线" 段尾 | (新增) | "- 不 ..." | ✅ 适用所有调用 | skill 在第 M 轮自作主张做了 X 后被纠正 |
```

随后**显式停下**等用户授权：

```
请审核以上 M 条建议：
- 全部应用：回 "apply" / "按建议改" / "go ahead"
- 部分应用：回 "apply 1,3" 或指明编号
- 否决某条：回 "drop 2"
- 修改某条：回 "edit 3: <你的改写>"
- 全部否决：回 "skip" / "不改了"
```

## Step 6 · 应用变更（**仅在用户显式授权后**）

收到明确编号清单（或 "全部"）后：

1. **逐条 Edit**，不批量；每条 Edit 的 `old_string` 必须含足够上下文以保证文件内唯一；每条完成后简述"已应用第 N 条"。
2. 用户用 `edit N: <改写>` 形式给出的微调 → 用其文本替换原"改后"列后再 Edit。
3. 全部 Edit 完成后跑**收尾自检**：

   - [ ] frontmatter `description` 是否仍准确（如果改动涉及触发词 / 能力 / 边界 → 同步改 description）
   - [ ] "严守红线" 段是否仍成体系（新增/弱化后无自相矛盾、无重复条目）
   - [ ] 引用的章节号 / 路径 / 字段名 / 上下游 skill 名是否依然正确
   - [ ] 没有把示例文字误删 / 误改成生效规则
   - [ ] 改后文件仍以中文为主，绝对日期仍为 `YYYY-MM-DD`

收尾自检发现新问题 → 回到 **Step 5** 出二轮建议表，**不自行追改**。

## Step 7 · 交付报告（短）

```
✅ 已对 <skill-name> 应用 K 条改动
- 维度分布: A×.. B×.. C×.. D×.. E×.. F×..
- frontmatter.description 是否更新: 是/否
- 收尾自检: 通过 / 发现 X 个新问题（已列入二轮建议）
- 未应用建议: (M-K) 条（保留在对话内供后续决策）
```

不指挥下一步，不串调其他 skill / 工具。

## 严守红线

- **默认只读**。第一轮**永远**只输出建议表 + 等授权；"我先把看起来明显的改一下" 一律违规。授权词必须显式（`apply` / `按建议改` / `go ahead` / `编号清单`），含糊的"嗯"、"行"、"看着办"不算授权 → 反问确认。
- **不特化**。每条改动必经 Step 4 的普适性自检；过不了的丢弃。建议表里**不允许**出现专属于某 Type / Feature / 模块名的条目。
- **一次只动一个 skill**。证据牵涉多个 skill → 分别出建议表，分别等授权，不混编落地。
- **不动 user-invocable 系统级 skill**（simplify / loop / init / review / security-review / claude-api / fewer-permission-prompts / update-config / keybindings-help / schedule 等）。本 skill 仅维护 `.claude/skills/<project-skill>/SKILL.md`。
- **不删除既有红线**，除非用户在授权时**明确点出**"删掉第 X 条红线"；**削弱**（补充适用边界）允许，**整条删除**默认不允许。
- **不改 frontmatter `name`**（会破坏 skill 发现机制）；改 `description` 时必须保留原触发词列表（可增、不可减）。
- **不主动复盘自己**（不递归用本 skill 改本 skill），除非用户显式要求"复盘 skill-retrospective"。
- **不读 4 份规范**。规范本身是否要变更 → 引导用户走 skill-chain-maintenance 的 `propagate` 模式，本 skill 不接。
- **不写 `_audit/`** 或其他报告文件。本 skill 默认无文件输出；用户要归档建议表时再 Write，写到 `.claude/skills/_retro/<YYYY-MM-DD>-<target-skill>.md`，不与 `_audit/` 混用。
- **始终中文撰写**；**绝对日期** `YYYY-MM-DD`。
