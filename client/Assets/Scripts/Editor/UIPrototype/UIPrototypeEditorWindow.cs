using System.Collections.Generic;
using System.IO;
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public class UIPrototypeEditorWindow : EditorWindow
{
    private readonly UIPlanJsonStore planStore = new UIPlanJsonStore();
    private readonly UIPrefabScanner scanner = new UIPrefabScanner();
    private readonly UIPrototypeValidator validator = new UIPrototypeValidator();
    private readonly UIDesignDocGenerator designDocGenerator = new UIDesignDocGenerator();
    private readonly UIScreenshotGenerator screenshotGenerator = new UIScreenshotGenerator();
    private readonly UIPrototypeComponentLibrary componentLibrary = new UIPrototypeComponentLibrary();

    private GameObject currentPrefab;
    private string currentPrefabPath;
    private UIViewPlanInfo currentPlanInfo;
    private UIViewScanResult currentScanResult;
    private List<UIValidationIssue> validationIssues = new List<UIValidationIssue>();
    private UIElementScanInfo selectedElement;
    private bool currentPrefabLoadedByTool;
    private Vector2 componentListScroll;
    private Vector2 hierarchyScroll;
    private Vector2 inspectorScroll;
    private Vector2 issueScroll;
    private int resolutionIndex;

    private static readonly string[] ResolutionNames =
    {
        "1080 x 1920",
        "750 x 1334",
        "1920 x 1080"
    };

    private static readonly Vector2Int[] Resolutions =
    {
        new Vector2Int(1080, 1920),
        new Vector2Int(750, 1334),
        new Vector2Int(1920, 1080)
    };

    private const float IssuePanelHeight = 150f;
    private const float ToolbarHeight = 22f;

    [MenuItem("Tools/YFramework/UI Prototype Editor")]
    public static void Open()
    {
        GetWindow<UIPrototypeEditorWindow>("UI原型工具");
    }

    private void OnEnable()
    {
        minSize = new Vector2(960f, 600f);
    }

    private void OnDisable()
    {
        UnloadCurrentPrefab();
    }

    private void OnGUI()
    {
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            GUI.FocusControl(null);
        }

        SyncCurrentPrefabStage();
        DrawToolbar();

        float middleHeight = Mathf.Max(120f, position.height - ToolbarHeight * 2f - IssuePanelHeight);
        EditorGUILayout.BeginVertical(GUILayout.Height(middleHeight));
        EditorGUILayout.BeginHorizontal();
        DrawComponentLibraryPanel();
        DrawHierarchyPanel();
        DrawInspectorPanel();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        DrawIssuePanel();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("新建UI", EditorStyles.toolbarButton, GUILayout.Width(70)))
        {
            CreateNewUI();
        }

        if (GUILayout.Button("打开预制体", EditorStyles.toolbarButton, GUILayout.Width(95)))
        {
            OpenPrefabByDialog();
        }

        EditorGUI.BeginDisabledGroup(currentPrefab == null);
        if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(55)))
        {
            SaveCurrent();
        }

        if (GUILayout.Button(new GUIContent("扫描结构", "重新读取当前预制体中的 YUIElement 语义节点，并同步 plan.json 中的轻量记录。"), EditorStyles.toolbarButton, GUILayout.Width(75)))
        {
            ScanCurrent(true);
        }

        if (GUILayout.Button(new GUIContent("检查问题", "检查重复 ID、缺少语义脚本、空策划注释、列表缺 Item 模板等问题。不会修改预制体结构。"), EditorStyles.toolbarButton, GUILayout.Width(75)))
        {
            ValidateCurrent();
        }

        if (GUILayout.Button(new GUIContent("清理删除项", "彻底清空 plan.json 中的 deletedElements 历史删除记录。不会删除当前预制体节点。"), EditorStyles.toolbarButton, GUILayout.Width(85)))
        {
            ClearDeletedElements();
        }
        EditorGUI.EndDisabledGroup();

        GUILayout.FlexibleSpace();
        GUILayout.Label(string.IsNullOrEmpty(currentPrefabPath) ? "未选择预制体" : currentPrefabPath, EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("分辨率", EditorStyles.miniLabel, GUILayout.Width(45));
        resolutionIndex = EditorGUILayout.Popup(resolutionIndex, ResolutionNames, EditorStyles.toolbarPopup, GUILayout.Width(120));

        EditorGUI.BeginDisabledGroup(currentPrefab == null);
        if (GUILayout.Button("截图", EditorStyles.toolbarButton, GUILayout.Width(55)))
        {
            GenerateScreenshot();
        }

        if (GUILayout.Button("生成策划案", EditorStyles.toolbarButton, GUILayout.Width(90)))
        {
            GenerateDesignDoc();
        }
        EditorGUI.EndDisabledGroup();

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawComponentLibraryPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(240));
        EditorGUILayout.LabelField("组件库", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("刷新"))
        {
            AssetDatabase.Refresh();
            componentLibrary.ClearCache();
            Repaint();
        }

        if (GUILayout.Button("生成示例组件"))
        {
            CreateDefaultComponents();
        }
        EditorGUILayout.EndHorizontal();

        componentListScroll = EditorGUILayout.BeginScrollView(componentListScroll);
        List<GameObject> prefabs = componentLibrary.LoadComponentPrefabs();
        if (prefabs.Count == 0)
        {
            EditorGUILayout.HelpBox("未找到组件库 Prefab。可点击“生成示例组件”创建一套临时组件，或把项目正式 UI 组件 Prefab 放到 Assets/Game/UIPrototypeComponents。", MessageType.Info);
        }

        for (int i = 0; i < prefabs.Count; i++)
        {
            GameObject prefab = prefabs[i];
            EditorGUILayout.BeginHorizontal("box");
            EditorGUILayout.ObjectField(prefab, typeof(GameObject), false);
            EditorGUI.BeginDisabledGroup(currentPrefab == null);
            if (GUILayout.Button("添加", GUILayout.Width(48)))
            {
                AddComponentPrefab(prefab);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawHierarchyPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.MinWidth(360));
        EditorGUILayout.LabelField("当前UI结构", EditorStyles.boldLabel);

        if (currentPrefab == null)
        {
            EditorGUILayout.HelpBox("请先新建UI或打开预制体。", MessageType.Info);
            EditorGUILayout.HelpBox("扫描结构：读取当前预制体里已经挂载 YUIElement 的节点，并把新增/删除同步到 plan.json。检查问题：只做规则检查，不改结构；需要修复时可点下方问题里的“补挂”。", MessageType.None);
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.HelpBox("扫描结构会刷新中间列表和 plan.json；检查问题会在底部列出命名、语义脚本、注释等风险。", MessageType.None);

        hierarchyScroll = EditorGUILayout.BeginScrollView(hierarchyScroll);
        if (currentScanResult == null || currentScanResult.Elements.Count == 0)
        {
            EditorGUILayout.HelpBox("当前 Prefab 未扫描到 YUIElement 语义节点。", MessageType.Warning);
        }
        else
        {
            for (int i = 0; i < currentScanResult.Elements.Count; i++)
            {
                DrawElementRow(currentScanResult.Elements[i]);
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawElementRow(UIElementScanInfo element)
    {
        bool selected = selectedElement == element;
        GUIStyle style = selected ? EditorStyles.helpBox : GUIStyle.none;
        EditorGUILayout.BeginHorizontal(style);

        if (GUILayout.Button(element.ElementId, EditorStyles.label, GUILayout.Width(150)))
        {
            SelectElement(element);
        }

        GUILayout.Label(element.ElementType, GUILayout.Width(80));

        bool export = GUILayout.Toggle(element.ExportToDesignDoc, "导出", GUILayout.Width(75));
        if (export != element.ExportToDesignDoc)
        {
            element.ExportToDesignDoc = export;
            UIElementPlanInfo plan = planStore.GetOrCreateElement(currentPlanInfo, element.Path);
            plan.exportToDesignDoc = export;
            planStore.Save(currentPrefabPath, currentPlanInfo);
        }

        GUILayout.Label(element.Path, EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawInspectorPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(330));
        EditorGUILayout.LabelField("语义面板", EditorStyles.boldLabel);
        inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);

        if (selectedElement == null)
        {
            EditorGUILayout.HelpBox("选择一个语义节点后编辑导出和策划注释。", MessageType.Info);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.LabelField("元素ID", selectedElement.ElementId);
        EditorGUILayout.LabelField("类型", selectedElement.ElementType);
        EditorGUILayout.LabelField("路径", selectedElement.Path);

        UIElementPlanInfo plan = planStore.GetOrCreateElement(currentPlanInfo, selectedElement.Path);
        bool export = EditorGUILayout.Toggle("导出到策划案", plan.exportToDesignDoc);
        if (export != plan.exportToDesignDoc)
        {
            plan.exportToDesignDoc = export;
            selectedElement.ExportToDesignDoc = export;
            planStore.Save(currentPrefabPath, currentPlanInfo);
        }

        EditorGUILayout.LabelField("策划注释");
        string comment = EditorGUILayout.TextArea(plan.plannerComment, GUILayout.MinHeight(80));
        if (comment != plan.plannerComment)
        {
            plan.plannerComment = comment;
            selectedElement.PlannerComment = comment;
            planStore.Save(currentPrefabPath, currentPlanInfo);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("自动生成问题", EditorStyles.boldLabel);
        for (int i = 0; i < selectedElement.DesignQuestions.Count; i++)
        {
            EditorGUILayout.LabelField("- " + selectedElement.DesignQuestions[i], EditorStyles.wordWrappedMiniLabel);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawIssuePanel()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("校验结果", EditorStyles.boldLabel);
        issueScroll = EditorGUILayout.BeginScrollView(issueScroll, GUILayout.Height(120));

        if (validationIssues == null || validationIssues.Count == 0)
        {
            EditorGUILayout.LabelField("暂无问题。", EditorStyles.miniLabel);
        }
        else
        {
            for (int i = 0; i < validationIssues.Count; i++)
            {
                UIValidationIssue issue = validationIssues[i];
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("[" + issue.Severity + "]", GUILayout.Width(70));
                if (GUILayout.Button(issue.Message, EditorStyles.label))
                {
                    Selection.activeObject = issue.Context;
                }

                GameObject go = issue.Context as GameObject;
                if (go != null && go.GetComponent<YUIElement>() == null && UIPrefabScanner.GetRecommendedSemanticType(go) != null)
                {
                    if (GUILayout.Button("补挂", GUILayout.Width(48)))
                    {
                        AttachRecommendedSemantic(go);
                    }
                }

                GUILayout.Label(issue.Path, EditorStyles.miniLabel, GUILayout.Width(180));
                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void CreateNewUI()
    {
        EnsurePrototypeFolders();
        string path = EditorUtility.SaveFilePanelInProject(
            "新建UI预制体",
            "NewUIView",
            "prefab",
            "选择 UI Prototype Prefab 保存位置。",
            UIPrototypePathUtility.PrototypeRootPath);

        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        GameObject root = CreateDefaultUIRoot(Path.GetFileNameWithoutExtension(path));
        PrefabUtility.SaveAsPrefabAsset(root, path);
        DestroyImmediate(root);
        AssetDatabase.Refresh();

        OpenPrefab(path);
        currentPlanInfo.viewId = Path.GetFileNameWithoutExtension(path);
        currentPlanInfo.viewName = currentPlanInfo.viewId;
        planStore.Save(currentPrefabPath, currentPlanInfo);
        ScanCurrent(true);
    }

    private void OpenPrefabByDialog()
    {
        string fullPath = EditorUtility.OpenFilePanel("打开UI预制体", Application.dataPath, "prefab");
        if (string.IsNullOrEmpty(fullPath))
        {
            return;
        }

        string assetPath = FullPathToAssetPath(fullPath);
        if (string.IsNullOrEmpty(assetPath))
        {
            EditorUtility.DisplayDialog("打开预制体", "请选择当前 Unity 工程 Assets 目录下的 Prefab。", "确定");
            return;
        }

        OpenPrefab(assetPath);
    }

    private void OpenPrefab(string assetPath)
    {
        UnloadCurrentPrefab();
        currentPrefabPath = UIPrototypePathUtility.NormalizeAssetPath(assetPath);
        PrefabStage stage = PrefabStageUtility.OpenPrefab(currentPrefabPath);
        if (stage != null)
        {
            currentPrefab = stage.prefabContentsRoot;
            currentPrefabLoadedByTool = false;
        }
        else
        {
            currentPrefab = PrefabUtility.LoadPrefabContents(currentPrefabPath);
            currentPrefabLoadedByTool = true;
        }

        currentPlanInfo = planStore.LoadOrCreate(currentPrefabPath);
        selectedElement = null;
        ScanCurrent(false);
    }

    private void SaveCurrent()
    {
        if (currentPrefab == null)
        {
            return;
        }

        ScanCurrent(false);
        planStore.Save(currentPrefabPath, currentPlanInfo);
        PrefabUtility.SaveAsPrefabAsset(currentPrefab, currentPrefabPath);
        MarkCurrentPrefabStageDirty();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ShowNotification(new GUIContent("UI原型已保存。"));
    }

    private void ScanCurrent(bool syncPlan)
    {
        if (currentPrefab == null)
        {
            return;
        }

        currentScanResult = scanner.Scan(currentPrefab, currentPlanInfo);
        if (syncPlan)
        {
            planStore.SyncWithScan(currentPlanInfo, currentScanResult);
            planStore.Save(currentPrefabPath, currentPlanInfo);
            currentScanResult = scanner.Scan(currentPrefab, currentPlanInfo);
        }

        validationIssues = validator.Validate(currentScanResult);
        Repaint();
    }

    private void ValidateCurrent()
    {
        ScanCurrent(false);
        ShowNotification(new GUIContent(validationIssues.Count == 0 ? "校验通过。" : "校验完成。"));
    }

    private void GenerateScreenshot()
    {
        try
        {
            SaveCurrent();
            Vector2Int resolution = Resolutions[Mathf.Clamp(resolutionIndex, 0, Resolutions.Length - 1)];
            string path = screenshotGenerator.Generate(currentPrefab, currentPrefabPath, currentPlanInfo, resolution.x, resolution.y);
            planStore.Touch(currentPlanInfo);
            planStore.Save(currentPrefabPath, currentPlanInfo);
            ShowNotification(new GUIContent("截图已生成：" + path));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "截图失败",
                "截图生成失败，详细信息已输出到 Console。\n\n" + exception.Message,
                "确定");
        }
    }

    private void GenerateDesignDoc()
    {
        SaveCurrent();
        ScanCurrent(true);
        planStore.Touch(currentPlanInfo);
        string path = designDocGenerator.Generate(currentPrefabPath, currentPlanInfo, currentScanResult);
        planStore.Save(currentPrefabPath, currentPlanInfo);
        ShowNotification(new GUIContent("策划案已生成：" + path));
    }

    private void ClearDeletedElements()
    {
        if (currentPlanInfo == null)
        {
            return;
        }

        int count = currentPlanInfo.deletedElements == null ? 0 : currentPlanInfo.deletedElements.Count;
        if (count == 0)
        {
            ShowNotification(new GUIContent("没有可清理的删除记录。"));
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "清理删除项",
                "将从 plan.json 中彻底移除 " + count + " 条 deletedElements 历史删除记录。\n当前预制体内容不会被修改。",
                "清理",
                "取消"))
        {
            return;
        }

        planStore.ClearDeletedElements(currentPlanInfo);
        planStore.Save(currentPrefabPath, currentPlanInfo);
        ShowNotification(new GUIContent("已清理删除记录。"));
    }

    private void AddComponentPrefab(GameObject prefab)
    {
        Transform parent = selectedElement != null && selectedElement.Element != null
            ? selectedElement.Element.transform
            : currentPrefab.transform;

        GameObject instance = componentLibrary.AddComponentToParent(prefab, parent);
        if (instance != null)
        {
            YUIElement newElement = instance.GetComponent<YUIElement>();
            EditorUtility.SetDirty(currentPrefab);
            SaveCurrent();
            ScanCurrent(true);
            SelectElementByObject(newElement);
        }
    }

    private void CreateDefaultComponents()
    {
        try
        {
            componentLibrary.CreateDefaultComponentPrefabs();
            componentLibrary.ClearCache();
            AssetDatabase.Refresh();
            ShowNotification(new GUIContent("示例组件已生成。"));
            Repaint();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "生成示例组件失败",
                "创建示例组件库时发生错误，详细信息已输出到 Console。\n\n" + exception.Message,
                "确定");
        }
    }

    private void SelectElement(UIElementScanInfo element)
    {
        selectedElement = element;
        if (element.Element != null)
        {
            Selection.activeObject = element.Element.gameObject;
            EditorGUIUtility.PingObject(element.Element.gameObject);
        }
    }

    private void SelectElementByObject(YUIElement element)
    {
        if (element == null || currentScanResult == null)
        {
            return;
        }

        for (int i = 0; i < currentScanResult.Elements.Count; i++)
        {
            if (currentScanResult.Elements[i].Element == element)
            {
                SelectElement(currentScanResult.Elements[i]);
                return;
            }
        }
    }

    private void AttachRecommendedSemantic(GameObject go)
    {
        System.Type type = UIPrefabScanner.GetRecommendedSemanticType(go);
        if (type == null)
        {
            return;
        }

        Undo.AddComponent(go, type);
        EditorUtility.SetDirty(go);
        ScanCurrent(true);
        SaveCurrent();
    }

    private void UnloadCurrentPrefab()
    {
        if (currentPrefab != null && currentPrefabLoadedByTool)
        {
            PrefabUtility.UnloadPrefabContents(currentPrefab);
        }

        currentPrefab = null;
        currentPrefabPath = null;
        currentPlanInfo = null;
        currentScanResult = null;
        selectedElement = null;
        currentPrefabLoadedByTool = false;
        validationIssues.Clear();
    }

    private void SyncCurrentPrefabStage()
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage == null || stage.prefabContentsRoot == null || string.IsNullOrEmpty(stage.assetPath))
        {
            return;
        }

        string stagePath = UIPrototypePathUtility.NormalizeAssetPath(stage.assetPath);
        if (currentPrefab == stage.prefabContentsRoot && currentPrefabPath == stagePath)
        {
            return;
        }

        if (currentPrefab != null && currentPrefabLoadedByTool)
        {
            PrefabUtility.UnloadPrefabContents(currentPrefab);
        }

        currentPrefab = stage.prefabContentsRoot;
        currentPrefabPath = stagePath;
        currentPrefabLoadedByTool = false;
        currentPlanInfo = planStore.LoadOrCreate(currentPrefabPath);
        selectedElement = null;
        ScanCurrent(false);
    }

    private void MarkCurrentPrefabStageDirty()
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.prefabContentsRoot == currentPrefab)
        {
            EditorSceneManager.MarkSceneDirty(stage.scene);
        }
    }

    private static void EnsurePrototypeFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Game"))
        {
            AssetDatabase.CreateFolder("Assets", "Game");
        }

        if (!AssetDatabase.IsValidFolder(UIPrototypePathUtility.PrototypeRootPath))
        {
            AssetDatabase.CreateFolder("Assets/Game", "UIPrototypes");
        }
    }

    private static GameObject CreateDefaultUIRoot(string viewId)
    {
        GameObject view = new GameObject(viewId, typeof(RectTransform));
        view.layer = LayerMask.NameToLayer("UI");
        Canvas canvas = view.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = view.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        view.AddComponent<GraphicRaycaster>();

        GameObject root = CreateRectChild(view.transform, "Root");
        Stretch(root.GetComponent<RectTransform>());
        CreateRectChild(root.transform, "Header");
        CreateRectChild(root.transform, "Content");
        CreateRectChild(root.transform, "Footer");
        return view;
    }

    private static GameObject CreateRectChild(Transform parent, string name)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        child.layer = LayerMask.NameToLayer("UI");
        child.transform.SetParent(parent, false);
        return child;
    }

    private static void Stretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static string FullPathToAssetPath(string fullPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName.Replace("\\", "/");
        string normalized = fullPath.Replace("\\", "/");
        if (!normalized.StartsWith(projectRoot))
        {
            return null;
        }

        string assetPath = normalized.Substring(projectRoot.Length + 1);
        return assetPath.StartsWith("Assets/") ? assetPath : null;
    }
}
