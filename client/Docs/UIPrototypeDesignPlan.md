# Unity UGUI 策划拼 UI 与策划案强绑定系统计划

## 1. 背景与目标

当前目标是在 Unity 内制作一个专门给策划使用的 UI 编辑器。策划可以通过该编辑器拼出 UGUI 界面原型，系统根据 UI 结构自动生成策划案模板，并附带 UI 截图。策划只需要补充交互、规则、数据来源、表现细节等内容。

后续如果策划或程序修改 UI，系统可以根据最新 UI 结构强制生成新的策划案条目，保证 UI 和策划案始终绑定。程序、美术和大模型都基于同一份策划案工作，从而减少口头沟通、遗漏和返工。

核心链路：

```text
策划拼 UI
  -> 生成 UGUI Prefab
  -> 根据 Prefab 语义生成 UI 结构
  -> 生成 UI 截图
  -> 生成策划案模板
  -> 策划补充细节（此处无需体现在编码里）
  -> 程序/美术按策划案执行（此处无需体现在编码）
  -> 大模型根据策划案直接辅助生成代码（此处无需体现在编码里）
```

## 2. 核心原则

1. UI 是策划案模板的来源。
2. UI 元素必须有稳定 ID，不能只依赖层级路径。
3. 策划案不能被重新生成时覆盖人工填写内容。
4. 新增 UI 元素必须生成新的待填写策划项。
5. 删除 UI 元素不能直接删除历史策划内容，标记为已删除。
6. 程序生成代码只覆盖 generated 文件，不覆盖人工逻辑。
7. 大模型直接读取策划案生成代码，UI 强绑定框架负责保证策划案条目与 UI 元素可追溯。

## 3. 总体架构

```text
Unity Editor UI Builder
  -> UGUI Prefab
  -> Prefab Semantic Scanner
  -> Lightweight Iteration JSON
  -> Screenshot
  -> Design Template Generator
  -> 策划案 Markdown 
  -> 后续暂时无需处理
```

## 4. 模块拆分

### 4.1 UI 拼装编辑器

实现一个 Unity EditorWindow，供策划创建和编辑 UI 页面。

主要能力：

- 新建 UI 页面。
- 基于预设组件添加 UGUI 控件。
- 支持基础布局调整。
- 支持组件语义填写。
- 支持实时预览。
- 支持保存为 Prefab。
- 支持从 Prefab 语义生成策划案结构。
- 支持生成截图。

首批支持组件：

- Button
- Text / TMP_Text
- Image
- Slider
- ScrollView
- InputField

每个组件优先从 Prefab 本身推导基础信息：

| 字段 | 说明 |
| --- | --- |
| ElementId | 优先使用 GameObject 名称或轻量标记 |
| DisplayName | 优先使用 GameObject 名称，可由策划注释补充 |
| ComponentType | 从 UGUI/TMP/自定义组件自动识别 |
| Path | 从 Prefab 层级自动计算 |
| NeedCode | 根据组件类型和是否导出为策划案推导 |
| NeedArt | 根据 Image、RawImage 等资源型组件推导，可人工补充 |
| Events | 从 Button、Slider 等组件推导 |

### 4.2 Prefab 语义与轻量迭代信息

策划案生成应优先只依赖 `.prefab` 文件。Prefab 是 UI 的真实结构来源，工具通过扫描 Prefab 层级、GameObject 名称、UGUI 组件类型和自定义 UI 组件，生成策划案层级结构。

JSON 不再承载完整 UI 结构，避免 Prefab 和 JSON 变成两份需要同步的数据。JSON 只保存 Prefab 无法自然表达、但策划案迭代需要的信息。

每个 UI 页面可选对应一个轻量迭代信息文件，例如：

```text
Assets/Game/UIPrototypes/Bag/BagView.plan.json
```

示例结构：

```json
{
  "viewId": "BagView",
  "viewName": "背包界面",
  "version": 3,
  "lastModifiedTime": "2026-05-05 20:51:00",
  "screenshot": "BagView.png",
  "elements": {
    "Root/Header/Btn_Close": {
      "plannerComment": "关闭当前背包界面",
      "exportToDesignDoc": true
    },
    "Root/Content/List_Item": {
      "plannerComment": "背包道具列表",
      "exportToDesignDoc": true
    }
  },
  "deletedElements": [
    {
      "id": "Btn_Close",
      "path": "Root/Header/Btn_Close_Old",
      "deletedAtVersion": 2
    }
  ]
}
```

关键设计：

- `.prefab` 是 UI 结构的唯一主来源。
- `.plan.json` 只保存策划注释、是否导出为策划案、版本号、修改时间、删除记录等迭代信息。
- 如果 `.plan.json` 缺少某个元素的信息，工具可以根据 Prefab 自动补充默认值。
- 不为每个元素额外设计复杂字段，避免增加策划维护成本。
- 元素层级结构由 Prefab 语义决定，策划案章节层级也由 Prefab 语义生成。

### 4.3 策划案模板生成器

策划点击“生成策划案”后，系统扫描 Prefab 并结合轻量迭代信息生成 Markdown 模板。

输出示例：

```text
Docs/UIPlans/Bag/BagView.design.md
```

文档结构：

```markdown
# 背包界面策划案

## 1. 基础信息

- 界面 ID：BagView
- 界面名称：背包界面
- UI 版本：3
- 生成时间：2026-05-05
- 负责人：待填写

## 2. UI 截图

![BagView](./BagView.png)

## 3. 界面整体说明

### 3.1 打开入口

<!--content-->
待填写。

### 3.2 关闭规则

<!--content-->
待填写。

### 3.3 权限/等级/条件

<!--content-->
待填写。

## 4. UI 元素说明

### Btn_Close：关闭按钮

- 类型：Button
- 路径：Root/Header/Btn_Close
- 是否需要程序：是
- 是否需要美术：否

#### 点击后行为

<!--content-->
待填写。

#### 是否有音效

<!--content-->
待填写。

#### 是否有关闭动画

<!--content-->
待填写。
```

生成器只需要保护策划填写内容：

- Prefab 结构区可以由工具刷新。
- 策划填写内容不能被工具覆盖。

推荐使用单一低学习成本锚点：

```markdown
<!--content-->
待填写。
```

工具只识别 `<!--content-->` 后面的策划填写内容。对于特殊行为、点击事件、列表刷新等内容，不需要额外锚点标记，仍然通过标题和 Prefab 语义定位，避免策划学习复杂规则。

### 4.4 UI 与策划案 Diff 同步

当 UI 被修改后，再次生成策划案时，不直接覆盖旧文档，而是执行同步。

变更类型：

| 变更 | 处理方式 |
| --- | --- |
| 新增 UI 元素 | 生成新策划项，标记为新增待填写 |
| 删除 UI 元素 | 保留旧内容，标记为已删除 |
| 修改元素名称 | 更新 Prefab 结构区，保留手动内容 |
| 修改元素路径 | 更新路径，不要求重填 |
| 修改元素类型 | 标记高风险，需要重新确认 |
| 新增事件 | 生成新的行为说明字段 |
| 删除事件 | 保留旧事件说明，标记为已删除 |
| 修改截图 | 更新文档截图引用 |

同步后在文档中生成变更摘要：

```markdown
## 本次 UI 变更

- 新增：Btn_Filter，筛选按钮
- 新增：Panel_Empty，空状态面板
- 修改：List_Item，从 ScrollView 改为 LoopList
- 删除：Btn_SellAll，批量出售按钮
```

## 5. 推荐落地阶段

### 阶段 1：MVP

目标：验证最小闭环。

范围：

- 在 Unity Editor 中选择一个已有或新建 UI Prefab。
- 从 Prefab 层级和组件语义生成策划案结构。
- 生成轻量 `plan.json`，仅保存版本、修改时间、策划注释、是否导出等信息。
- 生成 UI 截图。
- 生成 Markdown 策划案模板。
- 再次生成时支持新增元素 Diff。

MVP 验收标准：

- 策划能拼一个简单界面。
- 每个关键控件都有稳定 ID。
- 能生成带截图的策划案。
- UI 新增控件后，文档自动新增待填写项。
- 已填写内容不会被覆盖。

### 阶段 2：开发可用版本

范围：

- 支持完整组件预设。
- 支持元素命名规范检查。
- 支持 UI 元素完整度检查。
- 支持新增、删除、修改 Diff。
- 支持 Prefab 绑定代码生成。
- 支持美术资源清单生成。
- 支持列表、页签、弹窗等复杂 UI 模板。

验收标准：

- 程序可以直接使用 generated 绑定代码。
- 策划案能覆盖大部分 UI 需求描述。
- 美术资源需求能从 UI 和策划案中明确产出。
- UI 改动后能明确提示策划需要补充的内容。

### 阶段 3：团队流程化版本

范围：

- 策划案状态管理。
- UI 版本锁定。
- UI 变更审批。
- 程序开发状态。
- 美术资源状态。
- 任务系统对接。
- 文档平台对接。

验收标准：

- 一个 UI 页面从原型到开发、美术、验收有清晰状态。
- UI 变更能追踪责任人和版本。
- 策划案缺失项能被自动检查出来。

### 阶段 4：AI 辅助开发版本

范围：

- 大模型直接读取策划案 Markdown。
- 根据策划案中的 UI 条目、交互规则、数据说明和表现细节生成 Prompt。
- 生成 View 层代码。
- 自动跑 Unity 编译。
- 自动修复编译错误。
- 生成测试或调试用例。
- 人工 Review 后合入。

验收标准：

- 大模型可以基于策划案生成可编译的 View 逻辑代码。
- 程序主要做 Review 和复杂业务补充。
- 编译错误能自动反馈并修复一部分。

## 6. 数据目录建议

UI 原型和迭代信息：

```text
Assets/Game/UIPrototypes/{Module}/{ViewId}/{ViewId}.prefab
Assets/Game/UIPrototypes/{Module}/{ViewId}/{ViewId}.plan.json
Assets/Game/UIPrototypes/{Module}/{ViewId}/{ViewId}.png
```

策划案：

```text
Docs/UIPlans/{Module}/{ViewId}.design.md
```

生成代码：

```text
Assets/Game/Scripts/UI/{Module}/{ViewId}.generated.cs
Assets/Game/Scripts/UI/{Module}/{ViewId}.cs
```

美术清单：

```text
Docs/UIPlans/{Module}/{ViewId}.art.md
Assets/Game/Art/UI/{Module}/
```

## 7. 关键技术点

### 7.1 Prefab 语义绑定

每个 UI 元素优先从 Prefab 推导：

- GameObject 名称作为人类可读 ElementId。
- 当前 Prefab 层级 Path 作为定位依据。
- UGUI/TMP/自定义组件作为组件语义。
- 可选策划注释和是否导出配置来自 `plan.json`。

绑定优先级：

```text
Prefab 对象引用/文件 ID > ElementId > Path
```

如果 Path 变化但 ElementId 未变，策划案内容不应丢失。如果缺少补充信息，工具自动补默认值并提示策划确认。

### 7.2 文档保护

文档需要区分自动区和手写区。

文档中只保留一个低学习成本锚点：

```markdown
<!--content-->
待填写。
```

工具刷新文档时，只更新 Prefab 自动生成的标题、截图、路径、组件类型等结构信息；`<!--content-->` 后面的策划填写内容必须保留。

### 7.3 UI 截图

生成截图时需要支持：

- 指定分辨率。
- 指定 Canvas。
- 指定摄像机。
- 支持透明背景或默认背景。
- 支持横竖屏。
- 截图文件自动随策划案更新。

### 7.4 完整度检查

策划案生成后，工具应能检查：

- 是否有未命名元素。
- 是否有重复 ElementId。
- 是否有新增待填写项。
- 是否有高风险类型变更。
- 是否有 NeedCode 但未生成绑定字段的元素。
- 是否有 NeedArt 但未填写美术说明的元素。

### 7.5 代码生成边界

生成器可以生成：

- 字段绑定。
- 事件注册。
- 空方法 Stub。
- 列表 Item 绑定。
- UI 刷新入口。

生成器不应覆盖：

- 人工业务逻辑。
- 手写生命周期逻辑。
- 手写网络请求。
- 手写数据缓存。

## 8. 风险与解决方案

| 风险 | 解决方案 |
| --- | --- |
| 策划自由拼 UI 导致结构不可维护 | 使用预设组件、规范容器和命名检查 |
| UI ID 不稳定导致文档绑定失败 | 优先使用 Prefab 对象引用/文件 ID，辅以 ElementId 和 Path |
| 重新生成文档覆盖策划填写内容 | 使用统一 `<!--content-->` 锚点保护策划填写内容 |
| 策划案字段太多，策划不愿填 | 按组件类型生成字段，只问必要问题 |
| 大模型误解策划案 | 依赖 Prefab 语义生成的固定章节和 `<!--content-->` 内容，让模型直接读取策划案生成代码 |
| 程序改 UI 后策划不知道补什么 | Diff 同步并生成变更摘要 |
| 美术需求仍然不清楚 | NeedArt 元素自动生成资源清单 |

## 9. MVP 具体任务拆分

### 9.1 Editor 工具

- 创建 `UIPrototypeEditorWindow`。
- 支持选择或创建 UI Prefab。
- 支持扫描 Prefab 下的 UGUI 节点。
- 支持编辑策划注释和是否导出为策划案。
- 支持保存轻量迭代信息到 `plan.json`。

### 9.2 Prefab 扫描与迭代信息生成

- 定义 `UIViewPlanInfo`。
- 定义 `UIElementPlanInfo`。
- 实现 Prefab 扫描器。
- 实现轻量 `plan.json` 序列化。
- 实现 ElementId 重复检查。
- 实现缺失字段检查。

### 9.3 策划案生成

- 实现 Markdown 模板生成。
- 实现截图路径写入。
- 实现组件类型到填写字段的映射。
- 实现首次生成。
- 实现再次生成时保留手写内容。
- 实现新增元素自动追加。

### 9.4 截图生成

- 实现 Canvas 渲染截图。
- 支持固定分辨率。
- 输出 PNG。
- 自动复制或引用到策划案目录。

### 9.5 验收 Demo

用一个简单背包界面验证：

- `Btn_Close`
- `Txt_Title`
- `List_Item`
- `Item_Bag`
- `Panel_Empty`

验证动作：

1. 生成轻量 `plan.json`。
2. 生成截图。
3. 生成策划案。
4. 手动填写部分内容。
5. 新增 `Btn_Filter`。
6. 再次生成策划案。
7. 确认旧内容保留，新控件生成待填写项。

## 10. 后续扩展方向

- 支持 Word 文档生成。
- 支持飞书文档同步。
- 支持 Confluence 同步。
- 支持 UI 变更审批流。
- 支持策划案字段 Schema 配置。
- 支持项目自定义组件类型。
- 支持 UI 自动化测试生成。
- 支持大模型直接根据策划案生成代码。
- 支持大模型根据截图、策划案和 Prefab 发现遗漏字段。
- 支持开发完成后反向校验 UI 是否满足策划案。



