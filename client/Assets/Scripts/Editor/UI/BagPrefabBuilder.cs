#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成网格背包 UI 预制体到 Resources/UI/Bag 下,供 UIMgr/ResMgr 按路径加载。
/// 程序化构建:Unity 自动解析脚本 GUID / TMP 字体,比手写 .prefab YAML 可靠;脚本字段引用在此接好。
///
/// 菜单:Tools/Bag/Build Bag UI Prefabs
/// 产物:
///   - BagItem.prefab        物品控件(BagItemWidget)
///   - BagPanel.prefab       纯背包面板(BagPanel:单网格)
///   - InteractPrompt.prefab 世界交互提示
///
/// 共享 UI(右键菜单/拆分弹窗/tooltip)挂在 <see cref="GridHostPanelBase"/> 字段上。
/// </summary>
public static class BagPrefabBuilder
{
    private const string Dir = "Assets/Resources/UI/Bag";
    private const string ItemPrefabPath = Dir + "/BagItem.prefab";
    private const string BagPanelPath = Dir + "/BagPanel.prefab";
    private const string PromptPath = Dir + "/InteractPrompt.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";

    private const float Cell = 80f;
    private const float Gap = 4f;

    private static TMP_FontAsset _font;

    [MenuItem("Tools/Bag/Build Bag UI Prefabs")]
    public static void BuildAll()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);

        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (_font == null) Debug.LogWarning($"[BagPrefabBuilder] 未找到字体 {FontPath},回退默认字体。");

        BuildItemPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var itemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ItemPrefabPath);
        BuildBagPanel(itemPrefab);
        BuildPromptPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[BagPrefabBuilder] Done. Prefabs at {Dir}");
    }

    // ============================ 物品控件 ============================

    private static void BuildItemPrefab()
    {
        var root = NewUI("BagItem", out var rootRt);
        rootRt.sizeDelta = new Vector2(Cell, Cell);
        root.AddComponent<BagItemWidget>();
        PrefabUtility.SaveAsPrefabAsset(root, ItemPrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log($"[BagPrefabBuilder] Built {ItemPrefabPath}");
    }

    // ============================ 纯背包面板 ============================

    private static void BuildBagPanel(GameObject itemPrefab)
    {
        // 背包网格 10×8 = 800×640px。窗口 920×900,网格区(去边距)820×700 容纳之,不与标题/按钮重叠。
        var root = MakeWindowRoot("BagPanel", new Vector2(920, 900), out var window);

        Title(window, "背包  (左键使用 / 右键菜单 / 拖拽移动)");

        var capGo = NewUI("CapacityText", out var capRt, window.transform);
        capRt.anchorMin = new Vector2(1, 1); capRt.anchorMax = new Vector2(1, 1); capRt.pivot = new Vector2(1, 1);
        capRt.anchoredPosition = new Vector2(-110, -22); capRt.sizeDelta = new Vector2(200, 36);
        var capText = NewText(capGo, "0/80", 24, TextAlignmentOptions.Right);

        var closeBtn = BuildButton("CloseBtn", "X", window.transform,
            new Vector2(1, 1), new Vector2(-36, -36), new Vector2(56, 56), new Color(0.5f, 0.2f, 0.2f, 1f));
        var sortBtn = BuildButton("SortBtn", "整理", window.transform,
            new Vector2(0.5f, 0), new Vector2(0, 42), new Vector2(180, 60), new Color(0.2f, 0.4f, 0.55f, 1f));

        // 单网格区:上留 90(标题) 下留 110(整理按钮),BagGridView 居中其内
        var gridArea = NewUI("GridArea", out var gaRt, window.transform);
        gaRt.anchorMin = new Vector2(0, 0); gaRt.anchorMax = new Vector2(1, 1);
        gaRt.offsetMin = new Vector2(50, 110); gaRt.offsetMax = new Vector2(-50, -90);
        var bagGrid = NewUI("BagGrid", out _, gridArea.transform).AddComponent<BagGridView>();
        Stretch((RectTransform)bagGrid.transform, 0);

        var panel = root.AddComponent<BagPanel>();
        var cg = root.GetComponent<CanvasGroup>();
        panel.canvasGroup = cg;
        panel.uiType = UIEnum.BagPanel;
        panel.bagGrid = bagGrid;
        panel.sortBtn = sortBtn;
        panel.closeBtn = closeBtn;
        panel.capacityText = capText;
        ApplyHostCommon(panel, itemPrefab, root.transform);

        PrefabUtility.SaveAsPrefabAsset(root, BagPanelPath);
        Object.DestroyImmediate(root);
        Debug.Log($"[BagPrefabBuilder] Built {BagPanelPath}");
    }


    // ============================ 交互提示 ============================

    private static void BuildPromptPrefab()
    {
        var root = NewUI("InteractPrompt", out var rootRt);
        rootRt.anchorMin = new Vector2(0.5f, 0); rootRt.anchorMax = new Vector2(0.5f, 0); rootRt.pivot = new Vector2(0.5f, 0);
        rootRt.anchoredPosition = new Vector2(0, 180); rootRt.sizeDelta = new Vector2(360, 56);
        root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        // 文本作为子物体(一个 GameObject 只能有一个 Graphic,不能 Image+TMP 同挂)
        var labelGo = NewUI("Label", out var labelRt, root.transform);
        Stretch(labelRt, 0);
        NewText(labelGo, "按 F 打开宝箱", 26, TextAlignmentOptions.Center);

        PrefabUtility.SaveAsPrefabAsset(root, PromptPath);
        Object.DestroyImmediate(root);
        Debug.Log($"[BagPrefabBuilder] Built {PromptPath}");
    }

    // ============================ 共享 UI(挂到 GridHostPanelBase)============================

    /// <summary>设置 host 基类公共字段 + 构建右键菜单/拆分弹窗/tooltip。</summary>
    private static void ApplyHostCommon(GridHostPanelBase panel, GameObject itemPrefab, Transform root)
    {
        panel.itemWidgetPrefab = itemPrefab;
        panel.cellSize = Cell;
        panel.cellGap = Gap;
        panel.uiFont = _font;
        BuildContextMenu(root, panel);
        BuildSplitDialog(root, panel);
        BuildTooltip(root, panel);
    }

    private static void BuildContextMenu(Transform parent, GridHostPanelBase panel)
    {
        var menu = NewUI("ContextMenu", out var menuRt, parent);
        Stretch(menuRt, 0);
        var blockerImg = menu.AddComponent<Image>();
        blockerImg.color = new Color(0, 0, 0, 0.01f);
        var blockerBtn = menu.AddComponent<Button>();
        blockerBtn.targetGraphic = blockerImg;

        var box = NewUI("Panel", out var boxRt, menu.transform);
        boxRt.pivot = new Vector2(0, 1);
        boxRt.sizeDelta = new Vector2(160, 232);
        box.AddComponent<Image>().color = new Color(0.16f, 0.17f, 0.2f, 0.98f);

        var use = BuildStackedButton("UseBtn", "使用", box.transform, 0, new Color(0.25f, 0.4f, 0.3f, 1f));
        var rotate = BuildStackedButton("RotateBtn", "旋转", box.transform, 1, new Color(0.3f, 0.4f, 0.5f, 1f));
        var split = BuildStackedButton("SplitBtn", "拆分", box.transform, 2, new Color(0.3f, 0.35f, 0.5f, 1f));
        var discard = BuildStackedButton("DiscardBtn", "丢弃", box.transform, 3, new Color(0.5f, 0.25f, 0.25f, 1f));

        panel.contextMenu = menu;
        panel.contextMenuPanel = boxRt;
        panel.ctxUseBtn = use;
        panel.ctxRotateBtn = rotate;
        panel.ctxSplitBtn = split;
        panel.ctxDiscardBtn = discard;
    }

    private static Button BuildStackedButton(string name, string label, Transform parent, int index, Color color)
    {
        var go = NewUI(name, out var rt, parent);
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
        rt.offsetMin = new Vector2(8, 0); rt.offsetMax = new Vector2(-8, 0);
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, 52);
        rt.anchoredPosition = new Vector2(0, -8 - index * 56);

        var img = go.AddComponent<Image>();
        img.color = color;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var labelGo = NewUI("Label", out var labelRt, go.transform);
        Stretch(labelRt, 0);
        NewText(labelGo, label, 24, TextAlignmentOptions.Center);
        return btn;
    }

    private static void BuildSplitDialog(Transform parent, GridHostPanelBase panel)
    {
        var dlg = NewUI("SplitDialog", out var dlgRt, parent);
        Stretch(dlgRt, 0);
        dlg.AddComponent<Image>().color = new Color(0, 0, 0, 0.5f);

        var box = NewUI("Panel", out var boxRt, dlg.transform);
        boxRt.anchorMin = boxRt.anchorMax = boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(420, 240);
        box.AddComponent<Image>().color = new Color(0.16f, 0.17f, 0.2f, 0.99f);

        var titleGo = NewUI("Title", out var titleRt, box.transform);
        titleRt.anchorMin = new Vector2(0, 1); titleRt.anchorMax = new Vector2(1, 1); titleRt.pivot = new Vector2(0.5f, 1);
        titleRt.anchoredPosition = new Vector2(0, -14); titleRt.sizeDelta = new Vector2(-24, 40);
        NewText(titleGo, "拆分堆叠", 26, TextAlignmentOptions.Center);

        var amtGo = NewUI("Amount", out var amtRt, box.transform);
        amtRt.anchorMin = new Vector2(0, 1); amtRt.anchorMax = new Vector2(1, 1); amtRt.pivot = new Vector2(0.5f, 1);
        amtRt.anchoredPosition = new Vector2(0, -64); amtRt.sizeDelta = new Vector2(-24, 36);
        var amtText = NewText(amtGo, "拆出 1  /  留 1", 22, TextAlignmentOptions.Center);

        var slider = BuildSlider("Slider", box.transform, new Vector2(0, 36), new Vector2(360, 30));

        var confirm = BuildButton("Confirm", "确认", box.transform,
            new Vector2(0.5f, 0), new Vector2(-100, 30), new Vector2(160, 54), new Color(0.25f, 0.45f, 0.3f, 1f));
        var cancel = BuildButton("Cancel", "取消", box.transform,
            new Vector2(0.5f, 0), new Vector2(100, 30), new Vector2(160, 54), new Color(0.45f, 0.3f, 0.3f, 1f));

        panel.splitDialog = dlg;
        panel.splitSlider = slider;
        panel.splitAmountText = amtText;
        panel.splitConfirmBtn = confirm;
        panel.splitCancelBtn = cancel;
    }

    private static Slider BuildSlider(string name, Transform parent, Vector2 anchoredPos, Vector2 size)
    {
        var go = NewUI(name, out var rt, parent);
        rt.anchorMin = new Vector2(0.5f, 0); rt.anchorMax = new Vector2(0.5f, 0); rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        var slider = go.AddComponent<Slider>();

        var bg = NewUI("Background", out var bgRt, go.transform);
        bgRt.anchorMin = new Vector2(0, 0.25f); bgRt.anchorMax = new Vector2(1, 0.75f);
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
        bg.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.35f, 1f);

        var fillArea = NewUI("Fill Area", out var faRt, go.transform);
        faRt.anchorMin = new Vector2(0, 0.25f); faRt.anchorMax = new Vector2(1, 0.75f);
        faRt.offsetMin = new Vector2(8, 0); faRt.offsetMax = new Vector2(-8, 0);
        var fill = NewUI("Fill", out var fillRt, fillArea.transform);
        fillRt.anchorMin = new Vector2(0, 0); fillRt.anchorMax = new Vector2(0, 1);
        fillRt.sizeDelta = new Vector2(10, 0);
        fill.AddComponent<Image>().color = new Color(0.4f, 0.6f, 0.9f, 1f);

        var handleArea = NewUI("Handle Slide Area", out var haRt, go.transform);
        haRt.anchorMin = Vector2.zero; haRt.anchorMax = Vector2.one;
        haRt.offsetMin = new Vector2(8, 0); haRt.offsetMax = new Vector2(-8, 0);
        var handle = NewUI("Handle", out var handleRt, handleArea.transform);
        handleRt.sizeDelta = new Vector2(22, 0);
        var handleImg = handle.AddComponent<Image>();
        handleImg.color = Color.white;

        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.wholeNumbers = true;
        slider.minValue = 1;
        slider.maxValue = 2;
        slider.value = 1;
        return slider;
    }

    private static void BuildTooltip(Transform parent, GridHostPanelBase panel)
    {
        var tip = NewUI("Tooltip", out var tipRt, parent);
        Stretch(tipRt, 0);

        var box = NewUI("Panel", out var boxRt, tip.transform);
        boxRt.pivot = new Vector2(0, 1);
        boxRt.sizeDelta = new Vector2(320, 168);
        var boxImg = box.AddComponent<Image>();
        boxImg.color = new Color(0.08f, 0.09f, 0.11f, 0.96f);
        boxImg.raycastTarget = false;

        var nameGo = NewUI("Name", out var nameRt, box.transform);
        nameRt.anchorMin = new Vector2(0, 1); nameRt.anchorMax = new Vector2(1, 1); nameRt.pivot = new Vector2(0.5f, 1);
        nameRt.anchoredPosition = new Vector2(0, -10); nameRt.sizeDelta = new Vector2(-24, 34);
        var nameText = NewText(nameGo, "", 24, TextAlignmentOptions.TopLeft);

        var descGo = NewUI("Desc", out var descRt, box.transform);
        descRt.anchorMin = new Vector2(0, 1); descRt.anchorMax = new Vector2(1, 1); descRt.pivot = new Vector2(0.5f, 1);
        descRt.anchoredPosition = new Vector2(0, -50); descRt.sizeDelta = new Vector2(-24, 78);
        var descText = NewText(descGo, "", 18, TextAlignmentOptions.TopLeft);
        descText.color = new Color(0.8f, 0.8f, 0.82f, 1f);
        descText.enableWordWrapping = true;
        descText.overflowMode = TextOverflowModes.Truncate;

        var valGo = NewUI("Value", out var valRt, box.transform);
        valRt.anchorMin = new Vector2(0, 0); valRt.anchorMax = new Vector2(1, 0); valRt.pivot = new Vector2(0.5f, 0);
        valRt.anchoredPosition = new Vector2(0, 10); valRt.sizeDelta = new Vector2(-24, 28);
        var valText = NewText(valGo, "", 18, TextAlignmentOptions.BottomLeft);
        valText.color = new Color(0.95f, 0.85f, 0.4f, 1f);

        panel.tooltip = tip;
        panel.tooltipPanel = boxRt;
        panel.tipNameText = nameText;
        panel.tipDescText = descText;
        panel.tipValueText = valText;
    }

    // ============================ 工具 ============================

    /// <summary>建一个 UIPageBase 标准根(全屏遮罩 + CanvasGroup + YOTOUIShow)+ 居中窗口,返回 root,out window。</summary>
    private static GameObject MakeWindowRoot(string name, Vector2 windowSize, out GameObject window)
    {
        var root = NewUI(name, out var rootRt);
        Stretch(rootRt, 0);
        root.AddComponent<CanvasGroup>();
        root.AddComponent<YOTOUIShow>();
        var dim = root.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.6f);

        window = NewUI("Window", out var winRt, root.transform);
        winRt.anchorMin = winRt.anchorMax = winRt.pivot = new Vector2(0.5f, 0.5f);
        winRt.sizeDelta = windowSize;
        winRt.anchoredPosition = Vector2.zero;
        window.AddComponent<Image>().color = new Color(0.12f, 0.13f, 0.16f, 0.96f);
        return root;
    }

    private static void Title(GameObject window, string text)
    {
        var titleGo = NewUI("Title", out var titleRt, window.transform);
        titleRt.anchorMin = new Vector2(0, 1); titleRt.anchorMax = new Vector2(0.5f, 1); titleRt.pivot = new Vector2(0, 1);
        titleRt.anchoredPosition = new Vector2(20, -16); titleRt.sizeDelta = new Vector2(-40, 44);
        NewText(titleGo, text, 26, TextAlignmentOptions.Left);
    }

    private static GameObject NewUI(string name, out RectTransform rt, Transform parent = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        rt = go.GetComponent<RectTransform>();
        if (parent != null) rt.SetParent(parent, false);
        return go;
    }

    private static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    private static TextMeshProUGUI NewText(GameObject go, string text, float size, TextAlignmentOptions align)
    {
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = UITheme.Font(size);
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        var font = _font != null ? _font : TMP_Settings.defaultFontAsset;
        if (font != null) tmp.font = font;
        return tmp;
    }

    private static Button BuildButton(string name, string label, Transform parent,
        Vector2 anchor, Vector2 anchoredPos, Vector2 size, Color color)
    {
        var go = NewUI(name, out var rt, parent);
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.color = color;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var labelGo = NewUI("Label", out var labelRt, go.transform);
        Stretch(labelRt, 0);
        NewText(labelGo, label, 26, TextAlignmentOptions.Center);
        return btn;
    }
}
#endif
