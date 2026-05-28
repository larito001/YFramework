---
name: tps-feature
description: Use this skill when the user asks to add or extend a feature in TPS code (e.g. "tps 加一个 buff 组件", "tps-feature", "在 TPS 里实现 X"). Reads ARCHITECTURE.md first, classifies the request against existing principles, and stops to ask the user for a new principle if the request can't be cleanly mapped — then updates ARCHITECTURE.md and tps-review.md before writing any code.
---

在 `Assets/Scripts/GamePlay/TPS/` 加新功能。**先比对原则、不够就先补原则、再写代码**。规范见 `Assets/Scripts/GamePlay/TPS/ARCHITECTURE.md`。

# 流程

## 1. 读 ARCHITECTURE.md

必须先读全文。不读就写代码 = 跑偏起点。

## 2. 归类需求到五个层

按文档的"分层"小节把功能拆到具体载体上：

| 需求长这样 | 载体 |
|---|---|
| 角色 / 武器 / 子弹的新行为 | Component（加到对应 Actor 的组件链里） |
| 跨对象单实例能力 | Service |
| 一类 Actor 的列表 / Tick / Spawn 流程 | Manager |
| Unity 渲染 / 动画 / 反馈 | View |
| 新数据维度 | Actor 字段（写明状态 / 意图 / trigger） |

一个需求可能拆成多个层（"新 buff 系统" = BuffComponent + Service + Actor 字段）。拆完每一块都要能套进文档已有原则。

## 3. 判定能否套用既有原则

下列任一为"是"则**停下来问用户**，不要先动手：

- 需求**违反**三大不变量任一条（数据下游/状态上游、状态衍生反馈走事件、跨 actor 走 ID）
- 需求需要的载体行为**没有原则覆盖**（如"组件需要在 Tick 中加新组件" — 文档现在只说 Tick 中禁 Add）
- 需求引入**新的跨对象通信通道**（不属于"跨对象通信"表里任一行）
- 文档里的某条原则在该需求下**会不合理地阻碍正确实现**

不确定就算"是"——宁可多问一次。

## 4. 不在原则内 → 停下补原则

向用户提问，**用 AskUserQuestion**（不要长篇 markdown 让用户读）：

- 列出冲突的具体原则条文 + 行号
- 给 2~3 个候选方案（每个简短描述含义和代价）
- 让用户选 / Other 自定义

**等到用户答复再继续**。不要假设 / 不要"按最常见的做"。

## 5. 用户答完 → 更新两份文档

按这个顺序：

1. **`Assets/Scripts/GamePlay/TPS/ARCHITECTURE.md`** — 把新原则加到对应小节（分层 / Actor / Component / View / Manager / Service / 跨对象通信 / Tick 顺序），或新增一节
   - 如果是"放宽" 已有原则：在原条文下加例外
   - 如果是"新增"：在最合适的小节加条
   - 如果"颠覆"已有原则：先改旧条文，再加新条文，写明 why
   - 保持文档风格：✅/❌ 列表 + 一句话 why + 必要时给例子

2. **`.claude/skills/tps-review.md`** — 看 7 条检查里有没有应该加 / 改的
   - 新原则可被静态 grep 识别违反 → 加成第 8 条
   - 新原则改变了既有违规判定 → 改对应条
   - 新原则纯粹"放宽"已有限制 → 改对应条的例外列表
   - 没有可机械检查的方向 → 不动 review

3. **再写实现代码**

## 6. 在原则内 → 直接按原则实现

正常 Edit/Write。完成后简短陈述：归到哪个层、复用了哪条原则、有没有触碰跨层边界。

# 不要

- 不要在 ARCHITECTURE 模糊时自己脑补"应该是这个意思"
- 不要把不匹配的功能硬塞进"看起来差不多"的现有原则
- 不要先写代码再补原则（顺序反了 = 文档落后于代码 = 下次 review 抓不到）
- 不要往文档里堆"建议 / 最佳实践"——只放硬约束，软建议留给 review
- 不要因为"小改动"就跳过流程：单个新组件也走完整 5 步

# 何时不用这个 skill

- 改 bug / 重构 / 删代码 → 直接做，不需要原则比对
- 改 `Assets/Scripts/GamePlay/TPS/` 之外的代码 → 这个 skill 不覆盖
- 用户明确说"先随便写个 demo" → 跳过流程，但提交前提醒走一次 tps-review
