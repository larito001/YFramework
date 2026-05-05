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

实现一个 Unity EditorWindow，供策划创建和编辑 UI 页面。本质是对Ugui编辑进行扩展（注意不是模仿，是基于UGui的基础上扩展）。

主要能力：

- 新建 UI 页面，ui界面保存为预制体
- 基于预设组件添加 UGUI 控件。
- 预设组件是特定文件夹下的Prefab文件，在EditorWindow的里的列表里可以拖拽进编辑器
- 支持基础布局调整（Ugui原生）。
- 支持组件语义填写。
- 支持从 Prefab 语义生成策划案结构。
- 支持生成截图。

首批支持组件：

- Button【挂YButton脚本】
- Text / TMP_Text【挂YText脚本】
- Image【挂YImage脚本】
- Slider【挂YSlider脚本】
- ScrollView【挂YScrollView脚本】
- InputField【挂YInput脚本】

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

## 5. Unity UI 编辑器详细设计

### 5.1 设计定位

该工具不是重新实现一个 UI 编辑器，也不是在 Unity 外做一套类似 Figma 的系统，而是在 Unity 原生 UGUI 编辑能力上做策划友好的扩展。

核心定位：

- 布局、锚点、RectTransform、层级拖拽仍然使用 Unity 原生能力。
- 策划通过 EditorWindow 添加项目预设 UI 组件。
- 每个可进入策划案的组件都挂载项目语义脚本，例如 `YButton`、`YText`、`YImage`。
- 策划案层级由 Prefab 层级和语义脚本共同决定。
- `plan.json` 只保存策划注释、是否导出、版本、修改时间、删除记录等迭代信息，不保存完整 UI 结构。

### 5.2 编辑器入口

菜单入口：

```text
Tools/YFramework/UI Prototype Editor
```

主窗口：

```csharp
public class UIPrototypeEditorWindow : EditorWindow
{
    private GameObject currentPrefab;
    private UIViewPlanInfo currentPlanInfo;
    private Vector2 componentListScroll;
    private Vector2 hierarchyScroll;
    private Vector2 inspectorScroll;
}
```

窗口分为四个区域：

| 区域 | 用途 |
| --- | --- |
| 顶部工具栏 | 新建、打开、保存、生成截图、生成策划案、校验 |
| 左侧组件库 | 展示指定目录下的 UI 组件 Prefab |
| 中间当前 UI 结构 | 展示当前 Prefab 中可导出元素的树 |
| 右侧语义面板 | 编辑选中元素的策划注释、是否导出等信息 |

### 5.3 顶部工具栏设计

顶部工具栏按钮：

| 按钮 | 行为 |
| --- | --- |
| New UI | 创建新的 UI Prefab |
| Open Prefab | 选择已有 UI Prefab |
| Save | 保存 Prefab 和 `plan.json` |
| Scan | 重新扫描 Prefab 结构 |
| Validate | 检查命名、重复、缺失信息 |
| Screenshot | 生成当前 UI 截图 |
| Generate Doc | 生成或同步策划案 Markdown |

推荐工作流：

```text
New/Open Prefab
  -> 从左侧组件库拖入控件
  -> 使用 Unity 原生 Scene 视图调整布局
  -> 在右侧填写策划注释和导出选项
  -> Validate
  -> Screenshot
  -> Generate Doc
```

### 5.4 组件库设计

组件库不硬编码按钮、文本、图片等创建逻辑，而是扫描一个固定目录下的 Prefab。

推荐目录：

```text
Assets/Game/UIPrototypeComponents/
  Button.prefab
  Text.prefab
  Image.prefab
  Slider.prefab
  ScrollView.prefab
  InputField.prefab
```

每个组件 Prefab 需要满足：

- 根节点挂载对应语义脚本（没有就创建，默认继承Ugui的）。
- 组件命名符合默认命名规范。
- 内部可以包含完整 UGUI 结构。
- 可以由程序或美术维护样式。

示例：

| 组件 Prefab | 根节点脚本 | 说明 |
| --- | --- | --- |
| Button.prefab | `YButton` | 用于点击行为 |
| Text.prefab | `YText` | 用于静态或动态文本 |
| Image.prefab | `YImage` | 用于图片、美术资源、图标 |
| Slider.prefab | `YSlider` | 用于数值进度 |
| ScrollView.prefab | `YScrollView` | 用于滚动列表 |
| InputField.prefab | `YInput` | 用于输入 |

组件库交互：

- 左侧显示组件名称、类型和预览缩略图。
- 支持拖拽到 Scene 视图或当前 UI 根节点。
- 拖入后自动重命名，例如 `Btn_New`、`Txt_New`、`Img_New`。
- 拖入后自动创建或更新 `plan.json` 中该元素的轻量记录。

### 5.5 语义组件设计

语义组件负责告诉工具：这个节点是什么、如何生成策划案字段。它不负责复杂业务逻辑，也不保存策划注释和导出开关。

基础类：

```csharp
public abstract class YUIElement : MonoBehaviour
{
    public abstract string ElementType { get; }
    public virtual string DisplayName => gameObject.name;
    public virtual IEnumerable<string> GetDesignQuestions()
    {
        yield return "功能说明";
    }
}
```

按钮：

```csharp
public class YButton : YUIElement
{
    public override string ElementType => "Button";

    public override IEnumerable<string> GetDesignQuestions()
    {
        yield return "点击后行为";
        yield return "是否有音效";
        yield return "是否有点击动画";
        yield return "是否有禁用状态";
    }
}
```

文本：

```csharp
public class YText : YUIElement
{
    public override string ElementType => "Text";

    public override IEnumerable<string> GetDesignQuestions()
    {
        yield return "文本来源";
        yield return "默认文案";
        yield return "是否需要多语言";
        yield return "为空时表现";
    }
}
```

图片：

```csharp
public class YImage : YUIElement
{
    public override string ElementType => "Image";

    public override IEnumerable<string> GetDesignQuestions()
    {
        yield return "图片用途";
        yield return "是否需要美术出图";
        yield return "资源命名";
        yield return "是否有状态变化";
    }
}
```

滚动列表：

```csharp
public class YScrollView : YUIElement
{
    public override string ElementType => "ScrollView";

    public override IEnumerable<string> GetDesignQuestions()
    {
        yield return "数据来源";
        yield return "排序规则";
        yield return "刷新时机";
        yield return "为空时表现";
        yield return "列表项点击行为";
    }
}
```

### 5.6 语义信息存储策略

语义信息分两层：

1. Prefab 内语义脚本。
2. `plan.json` 迭代补充信息。

推荐规则：

- Prefab 只保存真实 UI 结构、UGUI 组件和 `YUIElement` 语义脚本。
- `plannerComment`、`exportToDesignDoc`、`version`、`lastModifiedTime`、`deletedElements` 放在 `plan.json`。
- `plan.json` 缺少某个 Prefab 元素记录时，工具自动补默认记录。
- 不把完整 UI 结构写入 `plan.json`，避免形成第二份 UI 数据。

推荐 `plan.json`：

```json
{
  "viewId": "BagView",
  "viewName": "背包界面",
  "version": 3,
  "lastModifiedTime": "2026-05-05 21:59:00",
  "lastGeneratedDoc": "Docs/UIPlans/Bag/BagView.design.md",
  "lastScreenshot": "Docs/UIPlans/Bag/BagView.png",
  "elements": {
    "Root/Header/Btn_Close": {
      "plannerComment": "关闭当前界面",
      "exportToDesignDoc": true
    }
  },
  "deletedElements": [
    {
      "elementId": "Btn_SellAll",
      "path": "Root/Footer/Btn_SellAll",
      "elementType": "Button",
      "deletedAtVersion": 3
    }
  ]
}
```

### 5.7 Prefab 扫描规则

扫描入口：

```csharp
public sealed class UIPrefabScanner
{
    public UIViewScanResult Scan(GameObject prefabRoot);
}
```

扫描规则：

1. 从 Prefab 根节点开始深度遍历。
2. 只收集挂载 `YUIElement` 的节点。
3. 如果节点未挂 `YUIElement`，但有 Button、Text、Image 等 UGUI 组件，Validate 时提示是否自动补挂。
4. 从 `plan.json` 读取该节点的 `exportToDesignDoc`。
5. 如果 `exportToDesignDoc = false`，节点不进入策划案正文，但仍可作为结构节点存在。
6. Path 由 Prefab 根节点到当前节点的 GameObject 名称组成。
7. ElementId 默认等于 GameObject 名称。
8. 同一 Prefab 内 ElementId 必须唯一。

扫描结果：

```csharp
public sealed class UIViewScanResult
{
    public string ViewId;
    public string ViewName;
    public List<UIElementScanInfo> Elements;
    public List<UIValidationIssue> Issues;
}

public sealed class UIElementScanInfo
{
    public string ElementId;
    public string DisplayName;
    public string ElementType;
    public string Path;
    public bool ExportToDesignDoc;
    public string PlannerComment;
    public List<string> DesignQuestions;
}
```

### 5.8 当前 UI 结构面板

中间面板显示当前 Prefab 中的语义节点树。

展示字段：

| 字段 | 说明 |
| --- | --- |
| 图标 | 根据组件类型显示 |
| ElementId | GameObject 名称 |
| Type | `YButton`、`YText` 等 |
| Export | 是否导出为策划案 |
| Status | 正常、新增、已删除、命名冲突、缺失脚本 |

交互：

- 点击节点时，同步选中 Unity Hierarchy 中的 GameObject。
- 双击节点时，在 Scene 视图聚焦。
- 勾选/取消 Export 时，立即更新 `plan.json` 中的轻量记录。
- 命名冲突以红色标记。
- 未挂语义脚本但可识别的 UGUI 节点以黄色标记，并提供“一键补挂”。

### 5.9 右侧语义面板

选中一个 UI 元素后，右侧显示：

```text
ElementId: Btn_Close
Type: Button
Path: Root/Header/Btn_Close
Export To Design Doc: true
Planner Comment: 关闭当前界面
Generated Questions:
  - 点击后行为
  - 是否有音效
  - 是否有点击动画
  - 是否有禁用状态
```

字段说明：

- `ElementId` 不建议单独编辑，直接修改 GameObject 名称。
- `Type` 由脚本类型决定，不允许在这里改。
- `Path` 只读。
- `Export To Design Doc` 可编辑，保存到 `plan.json`。
- `Planner Comment` 可编辑，保存到 `plan.json`。
- `Generated Questions` 只读，由组件类型提供。

### 5.10 新建 UI 页面流程

新建流程：

1. 点击 `New UI`。
2. 输入模块名和界面名。
3. 工具创建 Canvas 根节点和 UI 根节点。
4. 保存为 Prefab。
5. 初始化 `plan.json`。
6. 自动打开 Prefab 编辑模式。

推荐目录：

```text
Assets/Game/UIPrototypes/{Module}/{ViewId}/{ViewId}.prefab
Assets/Game/UIPrototypes/{Module}/{ViewId}/{ViewId}.plan.json
Docs/UIPlans/{Module}/{ViewId}.design.md
Docs/UIPlans/{Module}/{ViewId}.png
```

默认根节点结构：

```text
BagView
  Root
    Header
    Content
    Footer
```

### 5.11 组件拖入流程

拖入流程：

1. 策划从左侧组件库拖一个组件 Prefab。
2. 放到 Scene 视图或当前 UI 结构树中的某个父节点。
3. 工具实例化该组件。
4. 根据组件类型生成默认名称。
5. 检查名称是否重复。
6. 选中新元素并打开右侧语义面板。
7. 策划填写 `Planner Comment`，确认是否导出。

默认命名规则：

| 类型 | 前缀 | 示例 |
| --- | --- | --- |
| Button | Btn_ | `Btn_Close` |
| Text | Txt_ | `Txt_Title` |
| Image | Img_ | `Img_Icon` |
| Slider | Sld_ | `Sld_Progress` |
| ScrollView | List_ | `List_Item` |
| InputField | Input_ | `Input_Name` |

### 5.12 校验规则

点击 `Validate` 后执行：

| 规则 | 级别 | 处理 |
| --- | --- | --- |
| ElementId 重复 | Error | 必须修改 |
| 导出元素未挂 `YUIElement` | Error | 补挂或不导出 |
| GameObject 名称为空或默认名 | Warning | 建议重命名 |
| `Planner Comment` 为空 | Warning | 建议填写 |
| 图片类元素未说明是否需要美术 | Warning | 建议补充 |
| ScrollView 没有 Item 模板 | Warning | 建议补充 |
| Prefab 未保存 | Warning | 先保存再生成 |

校验结果显示在窗口底部，点击问题可定位到对应节点。

### 5.13 策划案生成流程

生成流程：

```text
Scan Prefab
  -> Validate
  -> Load Existing Markdown
  -> Extract Existing <!--content--> Blocks
  -> Generate New Structure From Prefab
  -> Restore Existing Content
  -> Append New Items With <!--content--> 待填写
  -> Mark Deleted Items
  -> Save Markdown
  -> Update plan.json Version And Modified Time
```

关键策略：

- 章节标题由 Prefab 层级和元素类型生成。
- 每个需要策划填写的字段下面只放一个 `<!--content-->`。
- 旧文档中 `<!--content-->` 后的内容按标题路径回填。
- 新增 UI 元素生成新的待填写内容。
- 删除 UI 元素保留旧内容，并在标题上标记 `[已删除]`。

### 5.14 截图生成流程

截图按钮行为：

1. 加载当前 Prefab。
2. 创建临时 Preview Scene 或使用 Prefab Stage。
3. 找到 Canvas 和 Camera。
4. 以指定分辨率渲染。
5. 输出 PNG 到策划案目录。
6. 更新 `plan.json.lastScreenshot`。

默认分辨率：

```text
1080 x 1920
```

可在窗口顶部提供分辨率下拉：

- 1080 x 1920
- 750 x 1334
- 1920 x 1080
- 自定义

### 5.15 文件与类建议

推荐代码目录：

```text
Assets/Scripts/Editor/UIPrototype/
  UIPrototypeEditorWindow.cs
  UIPrototypeComponentLibrary.cs
  UIPrefabScanner.cs
  UIDesignDocGenerator.cs
  UIScreenshotGenerator.cs
  UIPlanJsonStore.cs
  UIPrototypeValidator.cs

Assets/Scripts/Runtime/UIPrototype/
  YUIElement.cs
  YButton.cs
  YText.cs
  YImage.cs
  YSlider.cs
  YScrollView.cs
  YInput.cs
```

说明：

- Editor 代码只在 Unity Editor 中运行。
- Runtime 语义脚本挂在 Prefab 上，因此放 Runtime 目录。
- 如果不希望这些脚本进入正式包，后续可以通过 asmdef 或构建剥离策略处理。

### 5.16 MVP 实现边界

MVP 只做以下能力：

- 打开或新建 UI Prefab。
- 从组件库拖入预设组件。
- 扫描挂载 `YUIElement` 的节点。
- 编辑 `plannerComment` 和 `exportToDesignDoc`。
- 生成 `plan.json`。
- 生成截图。
- 生成 Markdown 策划案。
- 再次生成时保留 `<!--content-->` 内容。
- 新增节点自动生成待填写项。
- 删除节点标记为已删除。

MVP 暂不做：

- 自定义复杂布局系统。
- 类 Figma 操作体验。
- 多人协作锁。
- Word/飞书同步。
- AI 代码生成。
- 程序绑定代码生成。
