---
name: gen-ui-from-plan
description: 根据 UI 原型工具产出的策划案 markdown（如 Docs/UIPlans/UI/<View>.design.md）生成 UIPageBase 派生的 C# 页面脚本。命中场景：用户提到"根据策划案生成UI/代码"、"为 X.design.md 生成脚本"、"把策划案转 cs"。
allowed-tools: Bash(python *), Bash(python3 *), Bash(ls *), Bash(cat *), Read, Write
---

# 根据策划案生成 UI 脚本

把 `Docs/UIPlans/UI/<View>.design.md` 生成为挂在对应 Prefab 上的 `<ViewId> : UIPageBase` 脚本。**所有解析与代码拼装都由 `scripts/gen_ui.py` 完成**——Claude 不要自己读策划案再逐个写字段，那样浪费 token 且容易漏。

## 使用流程

> **注意**：Editor 里点 `Tools > YFramework > UI Prototype Editor → 生成策划案` 已经会自动调本 skill 并把脚本挂到 prefab、按字段名绑定子节点（详见 `UIPrototypeEditorWindow.GenerateDesignDoc / UICodeBinder / UIPrototypeCodeBinderQueue`）。本 skill 主要服务两类场景：(1) 用户在 Claude 里直接让"根据策划案生成代码"；(2) 想在不开 Unity 的情况下批量重新生成 cs。

1. 用户给出策划案 md 路径（或只给 ViewId，自己拼 `Docs/UIPlans/UI/<ViewId>.design.md`）。
2. 直接调脚本：

   ```bash
   python "${CLAUDE_SKILL_DIR}/scripts/gen_ui.py" --design <design.md> [--out <out.cs>] [--force]
   ```

   - `--out` 不传时默认输出到 `Assets/Scripts/GamePlay/UI/<ViewId>.cs`
   - 目标已存在时不覆盖；要覆盖加 `--force`
3. 脚本 stdout 会打印生成路径、元素类型统计、跳过项。把这段简要回给用户即可。
4. 如果用户是在 Claude 里调用（非 Editor 触发），告诉用户：cs 已写出，但**字段绑定关系**只能在 Editor 里完成——回 Editor 里点一次"生成策划案"即可触发自动绑定（按钮逻辑里会复跑本 skill，最后 `UIPrototypeCodeBinderQueue` 在编译完后自动 AddComponent 并按字段名匹配子节点）。

## 流程对照（脚本既是 Editor 的子工具，也是独立 CLI）

```
Editor "生成策划案" 按钮
    ├─ UIDesignDocGenerator.Generate     (写 .design.md)
    ├─ Process.Start("python", gen_ui.py)（本 skill）→ 写 .cs
    ├─ AssetDatabase.Refresh             （触发编译）
    └─ UIPrototypeCodeBinderQueue.Enqueue
           ↓ (编译完)
       UICodeBinder.AttachAndBind        (AddComponent + SerializedObject 按字段名绑定)
```

Claude/CLI 触发时只走中间那段 Python，剩下的 Editor 那两步用户自行回 Unity 里点按钮触发。

## 生成规则（已固化在脚本中）

策划案里的所有非"待填写"内容都会被映射到代码。逐元素：

### Button
- 字段：`public Button <id>;`
- OnLoad：
  - "按钮文本" 非空 → `{ var label = <id>.GetComponentInChildren<Text>(true); if (label != null) label.text = "<文本>"; }`
  - 注册 `<id>.onClick.AddListener(On<Pascal>Click);`
- 方法体：
  - "点击后行为" 含 `关闭/退出` → `CloseSelf();`
  - 含 `\w+Panel` 形式的页面名 → `Show<XPanel>();`
  - 其它非空 → `// TODO: <原文>`
  - 空 → `// TODO: 实现点击行为`

### Image
- 字段：`public Image <id>;`
- OnLoad：
  - "资源命名" 非空 → `<id>.sprite = Resources.Load<Sprite>("<name>");`

### InputField
- 字段：`public InputField <id>;`（直接 `<id>.text` 读写）
- OnLoad：
  - 名字含 `password/pwd` 或 输入含义/校验 含 `密码` → `contentType = Password`
  - 含 `邮箱/email/mail` → `EmailAddress`
  - 含 `手机/phone/数字` → `IntegerNumber`
  - "默认占位文案" 非空 → 写到 `placeholder as Text` 块
  - 注册 `onValueChanged += On<Pascal>Changed`
  - 若 "提交或失焦行为" 非空 → 再注册 `onEndEdit += On<Pascal>EndEdit`
- 方法体：
  - `On<Pascal>Changed`：把 "合法性校验" 翻成代码：邮箱→Regex.IsMatch；`大于N位/至少N位/<N位` 等 → `value.Length </>/>=  N`；数字 → `^-?\d+(?:\.\d+)?$` 正则；都翻不出 → 仅注释
  - `On<Pascal>EndEdit`："提交或失焦行为" 含 `切换/焦/tab/下一` → 在本 View 的其它 InputField 中匹配（先按 id 片段子串，再按 `账号→account/user`、`密码→password` 等中→英映射，最后兜底"如果只有一个对端就选它"）匹到则 `<other>.Select(); <other>.ActivateInputField();`，没匹到 → `// TODO: <原文>`

### Slider
- 字段：`public Slider <id>;`
- OnLoad：
  - "最小值与最大值" 形如 `0,100` / `0~100` / `0 到 100` → `minValue` / `maxValue`
  - 注册 `onValueChanged += On<Pascal>Changed`
- 方法体：`// TODO: <拖动后行为>` 或默认占位

### Text
- 字段：`public Text <id>;`
- OnShow："默认文案" 非空 → `<id>.text = "...";`

### ScrollView
- 跳过，仅在文件里留一行 `// ScrollView 暂未生成: <id>`，等用户给规则后再扩展脚本

策划案里 `<!--content-begin-->...<!--content-end-->` 之间的 `待填写。` / `待填写` 一律视为空。

类名取策划案首行 `# <View>策划案` 中的 `<View>`，原样使用（一般已是 PascalCase 的 ViewId 例如 `TestUI`）。

## 退出码

- 0：生成成功
- 2：策划案文件不存在
- 3：目标 cs 已存在且未指定 `--force`

## 不在本 skill 范围

- 自动写 Prefab YAML 把脚本和字段引用挂上：需要 Unity Editor 才能算 fileID 与 guid，超出脚本能力，留给用户在 Editor 处理。
- ScrollView 列表项绑定：等用户给规则后再扩展脚本。
