---
name: prefab-generation
description: 预制体生成 — 给定一份代码规划 id（或 git diff 范围、或显式 Panel/Entity 类名清单），扫描 `client/Assets/Scripts/GamePlay/` 下对应的 .cs，识别 UI Panel（继承 UIPageBase）与场景实体（继承 ObjectBase），在 `client/Assets/Scripts/Editor/PrefabBuilders/` 下生成 Unity Editor 构建器脚本：自动创建预制体层级、挂载 CanvasGroup + YOTOUIShow + Panel 脚本、按 public 字段名自动生成命名子 GameObject 并把引用回填到字段。**不直接写 .prefab 文件**，由 Unity 编译 Editor 脚本后用户点菜单（或批处理模式）实际产出 .prefab。当用户说"为 GP-Xxx 生成预制体"、"build prefabs for X"、"按规划生成预制体"、调用 /prefab-generation 时触发。
---

# 预制体生成 Skill

你现在的角色是**Unity Editor 工具开发者**。任务：把已生成的业务脚本（UI Panel / ObjectBase 实体）转成可一键产出 prefab 的 Editor 构建器，让美术不在场也能把"挂引用"的活先跑通。

**核心思路 — 为什么不直接写 .prefab**：

`.prefab` 是 Unity 序列化的 YAML，含 fileID 与 .cs.meta 的 GUID 引用。GUID 由 Unity 在导入 `.cs` 时生成。在 Claude Code 这一侧无法可靠生成对应的 .prefab + .meta。**所以本 skill 写的是 Editor 构建器**：用 C# 反射/`AddComponent<T>()`/`PrefabUtility.SaveAsPrefabAsset` 等 Unity API 在 Editor 内构造 prefab，**让 Unity 自己处理所有 GUID/序列化**。

## Step 0 · 始终先读规范与现有 UI 模式

进入本 skill 后，**第一步必须**确认（或读取）：

```
C:\UnityProject\YFramework\Docs\项目规范.md       (§3.1 Resources/ 路径与加载方式)
C:\UnityProject\YFramework\Docs\模块规范.md       (§6 UI 系统：UILayer / UIPageBase 必需 CanvasGroup + YOTOUIShow)
C:\UnityProject\YFramework\Docs\代码规范.md       (§7 UI / §10 Unity 特定约定)
```

并 Read 至少一个现有 Panel 作为参考（**理解字段命名约定与 RequireComponent 含义**）：

```
C:\UnityProject\YFramework\client\Assets\Scripts\Framework\UI\UIPageBase.cs     ([RequireComponent(CanvasGroup, YOTOUIShow)])
C:\UnityProject\YFramework\client\Assets\Scripts\GamePlay\UI\StartPanel.cs      (典型 Button 字段范式)
C:\UnityProject\YFramework\client\Assets\Scripts\GamePlay\UI\Setting\SettingPanel.cs  (含 List<T>、自定义 Ctrl 子组件)
```

如用户没指定来源（直接 `/prefab-generation` 无参），开放式问："要为哪份代码规划 / 哪些 Panel 生成预制体？给规划 id（如 `GP-Combat-Plan-v1`）、或 Panel 类名、或 `--diff`。"

## Step 1 · 定位生成范围

### 1.1 解析输入

| 用户输入 | 范围来源 | 操作 |
|---|---|---|
| 代码规划 id（推荐） | `代码规划/**/<id>.md` | Read 该规划，从 §6 资源清单与 §7 注册位置抽 prefab 清单 |
| Panel/Entity 类名（如 `CombatPanel`） | 类所在 .cs | Glob `**/<Name>.cs`，单类生成 |
| `--diff` | git working tree | 列 `git diff --name-only HEAD` 中 GamePlay 下的 `*Panel.cs` / `*Entity.cs` |
| 路径（目录/文件） | 显式 | Glob 范围内 .cs |

### 1.2 验证与去重

对每个候选目标 Read 一遍 .cs，按继承链分类：

- **UI Panel**：`<Class> : UIPageBase` 或 `: UIPageBase<TParam>` → **完整支持**
- **ObjectBase 实体**：`<Class> : ObjectBase` → **有限支持**（生成空 root + 挂脚本，art 留 TODO）
- **其他 MonoBehaviour 子组件**（如 `BaseSettingCtrl` 子类）：只在被某个 Panel 的字段引用时作为子节点出现，**不单独生成 prefab**
- **Manager / IGameService**：不需要 prefab，**跳过并明确告知用户**

### 1.3 抽取 prefab 路径

prefab 落点路径必须**严格匹配代码里的注册行**，不能臆造：

| 来源 | 路径来源 |
|---|---|
| UI Panel | `GameProjectBootstrapper.cs` 里的 `uiConfig.Register<TPanel>(UIEnum.X, UILayerEnum.Y, "UI/XxxPanel")` 第三参 → 实际路径为 `Assets/Resources/UI/XxxPanel.prefab` |
| Loading Panel | `uiConfig.RegisterLoading<...>(..., "UI/XxxPanel", ...)` 同上 |
| ObjectBase 实体 | 代码规划 §6 资源清单 / 实体调用方传入的 path 字符串。**从代码静态扫不出来时**：报告里要求用户提供 |

### 1.4 列范围给用户确认

输出一段话：

```
将为以下目标生成 Editor 构建器：

UI Panel（5）:
  CombatPanel       → Assets/Resources/UI/CombatPanel.prefab
  CombatHUDPanel    → Assets/Resources/UI/CombatHUDPanel.prefab
  ...

ObjectBase 实体（1）:
  CombatEntity      → Assets/Resources/Combat/CombatEntity.prefab  (路径来自规划 §6)

跳过（不需要 prefab 或无法静态推断）:
  CombatManager     (IGameService，无 prefab)
  HitFx             (实体路径运行时由 data 决定，需用户显式提供 path)

Editor 脚本输出位置：
  client/Assets/Scripts/Editor/PrefabBuilders/

生成后用户在 Unity 内点菜单 [YFramework/Build Prefabs/<Name>] 生成实际 .prefab。
```

**用户不确认前不写任何文件**。

## Step 2 · 静态分析每个 Panel/Entity 的字段

对每个 UI Panel（重点）：

### 2.1 字段抽取

只抽取**需要 Inspector 引用的字段**：

```
- public 字段（如 StartPanel.btn_new）
- [SerializeField] private 字段（同样需要 Inspector 引用）
```

跳过：

```
- private 非 SerializeField（运行时用，不挂引用）
- 静态字段
- 属性（property）
- 字段类型为 Manager / IGameService / 任意非 UnityEngine 类型（运行时通过 GetService 取，不在 Inspector）
```

### 2.2 字段类型 → 子节点映射

按下表分类。**未列出的类型一律生成 TODO 注释，让用户手动挂引用**：

| C# 字段类型 | 子 GO 组件构成 | 辅助方法 | 备注 |
|---|---|---|---|
| `Button` | RectTransform + Image + Button + 子 Label (TMP_Text) | `CreateButton(parent, name, label)` | label 默认用字段名 |
| `YOTOButton` | RectTransform + Image + YOTOButton + 子 Label | `CreateYOTOButton(...)` | YOTOButton 继承 Button |
| `TextMeshProUGUI` / `TMP_Text` | RectTransform + TextMeshProUGUI | `CreateTMP(parent, name, text)` | text 默认用字段名 |
| `Text` (legacy) | RectTransform + Text | `CreateText(...)` | 仅当代码确实用 legacy Text 时 |
| `Image` | RectTransform + Image | `CreateImage(parent, name)` | |
| `RawImage` | RectTransform + RawImage | `CreateRawImage(...)` | |
| `Slider` | 完整 Slider 层级（Background/Fill/Handle） | `CreateSlider(...)` | |
| `Toggle` | Toggle 层级（Background/Checkmark/Label） | `CreateToggle(...)` | |
| `TMP_InputField` | InputField 层级 | `CreateTMPInput(...)` | |
| `CanvasGroup` | RectTransform + CanvasGroup | `CreateChildCanvasGroup(...)` | |
| `GameObject` | RectTransform 子 GO | `CreateChildGO(parent, name)` | |
| `Transform` / `RectTransform` | 同上，回填 `.transform` | 同上 | |
| `YOTOScrollView` | RectTransform + YOTOScrollView 组件 + Viewport/Content 占位 | `CreateScrollView(...)` | 仅基础层级，复杂内容留 TODO |
| `List<T>` / `T[]` | **不创建子节点**，仅生成 TODO 注释 | — | 元素数量、子组件类型由业务运行时决定 |
| 其他 MonoBehaviour 子类（如 `BaseSettingCtrl`） | RectTransform + AddComponent<T> | `CreateChildWithComponent<T>(...)` | 当 T 不是 UI 视觉组件时只挂脚本 |
| 其他类型（自定义 ScriptableObject 等） | TODO 注释 | — | 无法在场景内 new，留人工 |

### 2.3 输出每 Panel 的"字段→子节点"清单

内部清单（不写文件，对话上下文保留）：

```
CombatPanel:
  自动可建:
    btn_fire        : Button            → CreateButton(root, "btn_fire", "Fire")
    btn_skill       : Button            → CreateButton(root, "btn_skill", "Skill")
    txt_hp          : TextMeshProUGUI   → CreateTMP(root, "txt_hp", "HP")
    img_portrait    : Image             → CreateImage(root, "img_portrait")
  TODO:
    skillCtrlList   : List<SkillCtrl>   → 用户运行时自定义元素数量

GameMainPanel:
  自动可建:
    scrollBar       : Image
    scrollView      : YOTOScrollView    → CreateScrollView(...)
    bagBtn          : Button
    time            : TextMeshProUGUI
    dayIcon         : GameObject        → CreateChildGO(...)
    nightIcon       : GameObject        → CreateChildGO(...)
  TODO:
    （无）
```

## Step 3 · 生成 Editor 构建器脚本

### 3.1 生成共享 Helpers（首次或缺失时）

文件：`client/Assets/Scripts/Editor/PrefabBuilders/PrefabBuilderHelpers.cs`

如果文件**已存在 → 跳过**（不覆盖既有可能被人工调过的 helpers）。

```csharp
#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

internal static class PrefabBuilderHelpers
{
    public static GameObject CreateRoot(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go;
    }

    public static GameObject CreateChildGO(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(100, 100);
        return go;
    }

    public static GameObject CreateChildCanvasGroup(Transform parent, string name)
    {
        var go = CreateChildGO(parent, name);
        go.AddComponent<CanvasGroup>();
        return go;
    }

    public static GameObject CreateImage(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(100, 100);
        return go;
    }

    public static GameObject CreateRawImage(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(100, 100);
        return go;
    }

    public static GameObject CreateTMP(Transform parent, string name, string text)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200, 40);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 24;
        return go;
    }

    public static GameObject CreateText(Transform parent, string name, string text)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200, 40);
        var t = go.GetComponent<Text>();
        t.text = text;
        t.alignment = TextAnchor.MiddleCenter;
        return go;
    }

    public static GameObject CreateButton(Transform parent, string name, string label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(160, 40);
        var labelGO = CreateTMP(go.transform, "Label", label);
        var labelRt = labelGO.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;
        return go;
    }

    public static GameObject CreateChildWithComponent<T>(Transform parent, string name) where T : Component
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.AddComponent<T>();
        return go;
    }

    public static void EnsureAssetDir(string assetPath)
    {
        var dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(dir)) return;
        if (!AssetDatabase.IsValidFolder(dir))
        {
            Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }
    }

    public static GameObject SavePrefab(GameObject root, string assetPath, bool overwrite = true)
    {
        EnsureAssetDir(assetPath);
        if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null)
        {
            Debug.LogWarning($"[PrefabBuilder] {assetPath} 已存在且 overwrite=false，跳过保存。");
            return null;
        }
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, assetPath);
        Debug.Log($"[PrefabBuilder] Saved {assetPath}");
        return prefab;
    }
}
#endif
```

### 3.2 生成单个 Builder（每个 Panel/Entity 一个文件）

文件：`client/Assets/Scripts/Editor/PrefabBuilders/<Name>PrefabBuilder.cs`

骨架（以 CombatPanel 为例）：

```csharp
#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

internal static class CombatPanelPrefabBuilder
{
    private const string PrefabPath = "Assets/Resources/UI/CombatPanel.prefab";

    [MenuItem("YFramework/Build Prefabs/CombatPanel")]
    public static void Build()
    {
        var root = PrefabBuilderHelpers.CreateRoot("CombatPanel");
        root.AddComponent<CanvasGroup>();
        root.AddComponent<YOTOUIShow>();
        var panel = root.AddComponent<CombatPanel>();

        // 自动生成（来自 Step 2.3 字段映射）
        var btnFire = PrefabBuilderHelpers.CreateButton(root.transform, "btn_fire", "btn_fire");
        panel.btn_fire = btnFire.GetComponent<Button>();

        var btnSkill = PrefabBuilderHelpers.CreateButton(root.transform, "btn_skill", "btn_skill");
        panel.btn_skill = btnSkill.GetComponent<Button>();

        var txtHp = PrefabBuilderHelpers.CreateTMP(root.transform, "txt_hp", "HP");
        panel.txt_hp = txtHp.GetComponent<TextMeshProUGUI>();

        var imgPortrait = PrefabBuilderHelpers.CreateImage(root.transform, "img_portrait");
        panel.img_portrait = imgPortrait.GetComponent<Image>();

        // TODO: 手动挂引用（自动生成跳过）
        // - skillCtrlList : List<SkillCtrl>  // 元素数量由业务运行时决定，请在编辑器内手动添加并拖入

        var prefab = PrefabBuilderHelpers.SavePrefab(root, PrefabPath, overwrite: true);
        Object.DestroyImmediate(root);
        if (prefab != null)
        {
            EditorGUIUtility.PingObject(prefab);
            Selection.activeObject = prefab;
        }
    }
}
#endif
```

ObjectBase 实体的 builder（仅基础骨架，不挂任何 UI 组件）：

```csharp
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

internal static class CombatEntityPrefabBuilder
{
    private const string PrefabPath = "Assets/Resources/Combat/CombatEntity.prefab";

    [MenuItem("YFramework/Build Prefabs/CombatEntity")]
    public static void Build()
    {
        var root = new GameObject("CombatEntity");
        // CombatEntity 是 ObjectBase（纯 C# 类），不能 AddComponent。
        // 真正运行时由 ObjectBase.InstanceGObj 实例化此 prefab，挂到 SceneModelBase。
        // 这里只占位 GameObject，留 TODO 让美术/程序补充：
        //   - 视觉：MeshRenderer / SpriteRenderer / Animator 等
        //   - 物理：Collider / Rigidbody（如需）
        //   - 子节点：根据规划补
        // SceneModelBase 会在运行时自动 AddComponent，无需在 prefab 里挂。

        var prefab = PrefabBuilderHelpers.SavePrefab(root, PrefabPath, overwrite: true);
        Object.DestroyImmediate(root);
        if (prefab != null) EditorGUIUtility.PingObject(prefab);
    }
}
#endif
```

### 3.3 生成 BuildAll 入口

文件：`client/Assets/Scripts/Editor/PrefabBuilders/AllPrefabBuilders.cs`

每次重新生成时**追加新的 Build 调用**到 `BuildAll`，**不删除既有**（既有调用对应的 builder 文件如果还在，应当继续被 BuildAll 调用）。

```csharp
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

internal static class AllPrefabBuilders
{
    [MenuItem("YFramework/Build Prefabs/[All]")]
    public static void BuildAll()
    {
        Debug.Log("[PrefabBuilder] BuildAll 开始");
        // ↓↓↓ 按字母序追加（生成的脚本自动维护此清单）↓↓↓
        CombatEntityPrefabBuilder.Build();
        CombatPanelPrefabBuilder.Build();
        // ↑↑↑ 自动维护区域（手工修改会被本 skill 下次执行时重写）↑↑↑
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PrefabBuilder] BuildAll 完成");
    }
}
#endif
```

> 实现细节：要求把"自动维护区域"包在固定标记 (`↓↓↓ ... ↑↑↑`) 之间，再次执行本 skill 时只重写标记内的行，标记外不动。

## Step 4 · 静态自检

写完所有 builder 后逐项核对：

### 4.1 文件层面

- [ ] `Editor/PrefabBuilders/` 目录下每个新增 `.cs` 都被 `#if UNITY_EDITOR` 包住，避免业务编译。
- [ ] 每个文件命名 `<Name>PrefabBuilder.cs`，类名与文件名一致。
- [ ] `AllPrefabBuilders.BuildAll` 内列出每一个新增 builder 的 `.Build()` 调用。
- [ ] 路径常量 `PrefabPath` **严格匹配** Bootstrapper 注册行的 `"UI/XxxPanel"` 解析后的路径（`Assets/Resources/UI/XxxPanel.prefab`）。

### 4.2 字段映射层面（用 Grep 自查每个新生成的 builder）

- [ ] 每个 `panel.<field> = ...` 的左值都在 Panel.cs 里能找到对应 public 字段（用 Grep `^\s*public\s+<Type>\s+<field>` 逐条核）。
- [ ] 字段类型与 builder 里 `GetComponent<T>()` 的 T 一致（`public Button btn_x` → `GetComponent<Button>()`，不能写成 `GetComponent<Image>()`）。
- [ ] List/Array 字段在 builder 内只有 TODO 注释，不强行赋值。

### 4.3 不修改业务代码

- [ ] `git diff --name-only -- client/Assets/Scripts/GamePlay/` 应为空（本 skill 不动业务）。
- [ ] `git diff --name-only -- client/Assets/Scripts/Framework/` 应为空。

### 4.4 现有 prefab 安全

- [ ] 对每个待生成的 `PrefabPath`，检查 `client/Assets/Resources/...` 下是否已存在同名 `.prefab`。
- [ ] 已存在 → 在 builder 里把 `overwrite: true` 改为 `false` 并加注释 `// 已存在原始 prefab，BuildAll 默认不覆盖；如需覆盖手动改 overwrite: true`。**不要默默覆盖美术资产**。

## Step 5 · 交付报告

```
## 预制体生成 Editor 脚本已就绪

### 生成的文件
- client/Assets/Scripts/Editor/PrefabBuilders/PrefabBuilderHelpers.cs   (共享辅助，已存在则跳过)
- client/Assets/Scripts/Editor/PrefabBuilders/CombatPanelPrefabBuilder.cs
- client/Assets/Scripts/Editor/PrefabBuilders/CombatEntityPrefabBuilder.cs
- client/Assets/Scripts/Editor/PrefabBuilders/AllPrefabBuilders.cs       (BuildAll 菜单)

### 自检
- 字段映射对齐 ............ ✅（自动可建 N 个 / TODO 手动 M 个）
- PrefabPath 与 Bootstrapper 注册行一致 ✅
- 业务代码未修改 .......... ✅
- 现有 prefab 安全 ........ ✅（X 个 overwrite=true / Y 个 overwrite=false 因已存在）

### 用户操作
1. 切到 Unity Editor，让其编译新增 Editor 脚本（自动）。
2. 顶部菜单 [YFramework/Build Prefabs/[All]] —— 一键生成所有 prefab。
   或单独点 [YFramework/Build Prefabs/<Name>] 只生成某一个。
3. Console 应输出 `[PrefabBuilder] Saved Assets/Resources/UI/XxxPanel.prefab`。
4. 在 Project 窗口验证：所选 prefab 上 CombatPanel 脚本已挂、btn_xxx / txt_xxx 等字段已自动填充。
5. 美术接手细化视觉（替换 Image / 调字号 / 调坐标），引用关系无需重连。

### 仍需人工的（TODO）
- CombatPanel.skillCtrlList : List<SkillCtrl>   元素数量由业务决定，Inspector 内手动添加
- CombatEntity prefab 的视觉 / 物理组件   需美术补
- Resources/Sfx/sword_hit.ogg 等音频资源    @owner（来自规划 §6 资源清单）

### 可选：批处理模式（无人值守）
项目根执行：
    Unity -batchmode -projectPath "C:\UnityProject\YFramework\client" \
      -executeMethod AllPrefabBuilders.BuildAll -quit -logFile -

要求 Unity 可执行文件在 PATH，且当前没有别的 Unity 实例占用工程。
```

## 严守红线

- **不写 `.prefab` / `.unity` / `.meta` 文件**。本 skill 只写 `.cs`（Editor 脚本），prefab 由 Unity 通过 builder 实际产出。
- **不修改业务代码**（`GamePlay/**/*.cs` 与 `Framework/**/*.cs`）。Panel 字段名/类型不一致是上游 code-generation 的责任，不在本 skill 处理。
- **不臆造路径**。prefab 路径必须有源（Bootstrapper 注册行 / 代码规划 §6）。无源 → 让用户提供，不要 `"UI/Xxx.prefab"` 凭直觉填。
- **不静默覆盖既有 prefab**。`Resources/...` 下已有同名 prefab → builder 默认 `overwrite: false` + 注释提醒。
- **不为没有 Inspector 引用需求的字段创建子节点**（如 private 非 SerializeField、Manager 类型字段）。
- **不发明字段映射**。Step 2.2 表外的类型一律 TODO 注释，不要"猜"。
- **不删 BuildAll 既有调用**。本 skill 重新执行时，只追加 / 替换"自动维护区域"内的本次范围；区域外的已存在调用保留（除非用户明确要求清理）。
- **始终中文撰写交付报告**；代码内注释保持英文/中文皆可（同代码规范 §3）。
- **始终用绝对日期** `YYYY-MM-DD`。
