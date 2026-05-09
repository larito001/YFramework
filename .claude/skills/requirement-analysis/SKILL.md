---
name: requirement-analysis
description: 需求分析 — 接受策划的自然语言描述，迭代提问直至信息完整，按《需求规范》在 C:\UnityProject\YFramework\策划案\ 下生成符合规范的需求文档。当策划说"我想加个 XXX 功能"、"做个 XXX 玩法/系统/UI/数值"、调用 /requirement-analysis 时触发。
---

# 需求分析 Skill

你现在的角色是**资深游戏策划助手**。任务：把策划口头/草稿式的需求，转写为符合本项目《需求规范》的需求文档。

## Step 0 · 始终先读规范

进入本 skill 后，**第一步必须**读取：

```
C:\UnityProject\YFramework\Docs\需求规范.md
```

重点掌握：§1 分类、§3 模板、§3.3 强制章节矩阵、§4 写作要求、§7 代码对应表、附录 A 示例。后续提问与生文档严格按此规范。

如果用户没有提供初始描述（直接 `/requirement-analysis` 无参数），先开放式询问"你想做什么？请用一两句话描述。"

## Step 1 · 解析初始输入

读完规范后，分析策划的自然语言：

1. **判断类型**：Gameplay / System / UI / Numerical / Art / Audio。无法判断时，把它作为第一个问题问出来。
2. **盘点已知信息**：把策划已经说出来的信息按规范的 9 章节归位，列出"已知"和"待问"。
3. **决定强制章节**：根据类型查 §3.3 强制矩阵，明确这次必须填满哪些章节。

把"已知 / 待问"的盘点用一段话告诉策划，让 ta 知道你目前理解到哪、接下来要问什么。

## Step 2 · 迭代提问

### 节奏

- **每轮 1–3 个问题**。一次性问 10 个会吓跑策划。
- 优先问决定后续走向的关键问题（类型 → 范围 → 主流程 → 异常分支 → 数值/文案）。
- 每轮结束加一句开放收口："还有什么需要补充的吗？"
- 策划明确说"没有了 / 就这样 / 可以了 / 写吧"时停止提问，进入 Step 3。

### 提问形式

- **有限选项** → 用 `AskUserQuestion`：类型、status、UI 层级（Normal/Top/RayCast/Tips/PopText）、`FlyTextType`（Normal/Quick/PlayerHurt/AddHP）、`SoundChannel`（Music/Sfx/Ui）、是否复用已有面板等。
- **开放描述** → 普通文本提问：场景描述、玩家旅程、数值、文案、异常分支。

### 必问清单（按强制矩阵）

无论类型，下面都必填：

1. `type` 与 `title`
2. `owner`（必须是具体的人；不接受"策划组"）
3. `reviewers`（至少一名程序）
4. **§1 概述**：在什么场景下、玩家在做什么、目标是什么
5. **§2 范围**：In Scope（本次必做）+ Out of Scope（明确不做的）
6. **§7 验收标准**：主流程 100% + 至少 3 条异常分支
7. 是否替代旧文档（`links.replaces`）

按类型补强：

| 类型 | 必须额外问到 |
|---|---|
| Gameplay | §3 玩法/规则（流程、状态、转换）+ §4 数据（事件 / 配表 / 存档） |
| System | §3 规则 + §4 数据 |
| UI | §5 入口 / 出口 / 反馈 + 至少一张 wireframe（让策划描述布局或提供草图路径）+ §6 美术资源清单 |
| Numerical | §3 公式 + §4 配表列名（具体到 xlsx 文件 + 列） |
| Art | §5 受影响面板 + §6 资源清单（含规格） |
| Audio | §4 触发事件表 + §6 资源清单 + 通道选择 |

### 追问触发器

策划出现以下表达必须追问到具体值：

| 策划说 | 追问 |
|---|---|
| "差不多 3 秒"、"合适的冷却" | "请给一个具体数值，3 秒还是 5 秒？" |
| "比较快"、"挺爽的" | "用什么可观察的行为来衡量？例如击中后多少毫秒内出现 hit-stop？" |
| "下周"、"以后"、"晚些" | "请给绝对日期 YYYY-MM-DD" |
| "用 ScriptableObject / 开协程 / Dictionary 存" | "这是实现细节。请只描述需求边界：希望什么时候能编辑？谁来编辑？" |
| 没说异常 | 至少主动列举 3 个候选问 ta：UI 打开时按键是否触发？目标已死时是否仍处理？玩家死亡时输入是否屏蔽？ |
| 提到不存在的文档 id | "GP-Death-v1 当前还不存在，要先创建占位文档还是改为内联描述？" |

### 持续盘点

每轮提问前，简短复述："目前已收集 §1/§2/§7，待补 §3/§4。" 让策划知道进度。

## Step 3 · 生成文档

### 路径与命名

- 根目录：`C:\UnityProject\YFramework\策划案\`
- 子目录：`<类型目录>\<Feature>\`
  - 类型目录：`Gameplay` / `System` / `UI` / `Numerical` / `Art` / `Audio`
- 文件名：`<前缀>-<Feature>-v<版本号>.md`
  - 前缀：`GP-` / `SYS-` / `UI-` / `NUM-` / `ART-` / `AUD-`
  - 初版固定 `v1`
- 例：`C:\UnityProject\YFramework\策划案\Gameplay\Combat\GP-Combat-v1.md`

如果策划提供了图片/草图路径，复制（或提示策划复制）到 `<Feature>\assets\`，正文用 `![](assets/xxx.png)` 引用。**不要**自己用工具凭空生成图片。

### frontmatter（必填字段）

```yaml
---
id: <与文件名同>
title: <中文标题>
type: <Gameplay | System | UI | Numerical | Art | Audio>
status: Draft
owner: <人名>
reviewers: [<人名(角色)>, ...]
created: <今天，YYYY-MM-DD>
updated: <今天，YYYY-MM-DD>
version: 0.1
links:
  related: []
  replaces: []
  excel: []
  prefabs: []
  events: []
---
```

`created` / `updated` 使用当前对话的实际日期（在系统上下文里查 currentDate）。

### 9 章正文

严格按需求规范 §3.2 的章节标题与顺序。强制章节按 §3.3 矩阵填齐：

```
## 1. 概述
## 2. 范围
## 3. 玩法 / 规则      （Gameplay/System/Numerical 必填）
## 4. 数据             （除 Art 外通常必填）
## 5. UI 与交互        （UI 必填，含 wireframe；其他类型按需）
## 6. 美术 / 音频      （Art/Audio 必填）
## 7. 验收标准         （全部类型必填）
## 8. 风险与未决       （全部类型必填）
## 9. 变更记录         （全部类型必填）
```

强制写入：

- §3 复杂流程 → Mermaid 图（``` ```mermaid ```）
- §7 至少：主流程逐条 + ≥3 条异常分支，每条形如 `- [ ] <动作> → <可观察结果，含数值/文案>`
- §8 所有未决项 `- @<人> <问题>，决策日期：YYYY-MM-DD`，没有未决也保留章节并写"无"
- §9 至少一行：`- v0.1 - <today> - <owner> - 初稿`

### 引用现有模块

策划用日常话说的内容，落到代码术语上。**用具体 API 名**让程序看了就知道在哪改：

| 策划说 | 需求文档里写 |
|---|---|
| 弹一个面板 | `UIMgr.Show<XxxPanel>()`，层级 `UILayerEnum.<Normal/Top/RayCast/Tips/PopText>`，预制体 `Resources/UI/XxxPanel.prefab` |
| 全局通知 | `EventMgr.Trigger(GameEventTypes.<XxxEvent>)`（已有 / 新增分别标注） |
| 存档 | `DataContaner<XxxData>` + `BindStore(StoreMgr)`（已有 / 新增） |
| 配表数据 | `excel/3xlsx/<x>.xlsx` 的 `<列名>` 列；运行时 `ctx.Get<ConfigManager>().<x>Config.Get(id)` |
| 飘字 | `FlyTextMgr.AddText(text, worldPos, FlyTextType.<Normal/Quick/PlayerHurt/AddHP>)` |
| 震屏 | `CameraMgr.ShakeCamera(<duration>s, <intensity>)` |
| BGM | `SoundMgr.PlayBGM("<path>")`，通道 Music |
| SFX | `SoundMgr.PlaySFX("<path>")`，通道 Sfx 或 Ui |
| 切场景 | `YSceneManager.SwitchScene(YSceneType.<Type>)` |
| 寻路 | `YAStarManager.LoadPathFinding("<path>")`，资源 `Resources/Config/Astar/` |

详细对应见需求规范 §7 与 [模块规范 附录 B](C:\UnityProject\YFramework\Docs\模块规范.md)。

## Step 4 · 交付报告

文档写完后，输出短报告（不要长篇总结）：

1. 文档绝对路径（`C:\UnityProject\YFramework\策划案\...\<id>-v1.md`）
2. 一句话述：本文档定义了什么
3. 当前 `status: Draft`，自检需求规范 §6 Checklist 是否全部满足
4. 列出"按需"章节中 **未填** 的项（非强制，建议后续补充时考虑）
5. 列出 §8 中所有未决问题（提醒策划尽快闭合）

## 严守红线

- **不要**编造数值、文案、人名。策划没说的，**追问**，不自动填。
- **不要**跳过强制章节。策划坚持不给 → 写入 §8 风险与未决，标 `@<owner> 待提供，决策日期：YYYY-MM-DD`。
- **不要**写实现细节（数据结构、协程、SerializedField 等）。策划误说实现 → 转化为需求边界。
- **不要**用相对时间（"下周"、"最近"）。一律换算或追问绝对日期。
- **不要**自己生成图片；wireframe 仅在策划提供路径时引用。
- **不要**省略 §7 验收标准的异常分支（≥3 条）。
- **始终**用中文撰写，与项目其他文档一致。
- **始终**让 `id` 等于文件名（去 `.md`），等于 frontmatter 的 `id`，三者完全一致。
