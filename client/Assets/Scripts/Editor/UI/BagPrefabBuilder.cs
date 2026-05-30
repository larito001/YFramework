#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成网格空间背包 UI 预制体到 Resources/UI/Bag 下,供 UIMgr/ResMgr 按路径加载。
/// 程序化构建(同 <see cref="WeaponPrefabBuilder"/> 约定):Unity 自动解析脚本 GUID / TMP 字体,
/// 比手写 .prefab YAML 可靠。脚本字段引用在此接好,生成后无需在 Inspector 手动拖。
///
/// 菜单:Tools/Bag/Build Bag UI Prefabs
/// 产物:
///   - Assets/Resources/UI/Bag/BagItem.prefab   可拖拽/旋转物品控件(BagItemWidget)
///   - Assets/Resources/UI/Bag/BagPanel.prefab  背包面板(BagPanel,含 GridRoot)
///
/// 物品尺寸/格盘大小是运行时按配表与 BagSystem 网格尺寸生成的,这里窗口给足够大即可。
/// 美术后续替换背景/按钮 Sprite、把物品图标放到 item 配表 IconPath 指向的位置。
/// </summary>
public static class BagPrefabBuilder
{
    private const string Dir = "Assets/Resources/UI/Bag";
    private const string ItemPrefabPath = Dir + "/BagItem.prefab";
    private const string PanelPrefabPath = Dir + "/BagPanel.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";

    private static TMP_FontAsset _font; // 本次生成用的 UI 字体(SIMHEI)

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
        BuildPanelPrefab(itemPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[BagPrefabBuilder] Done. Prefabs at {Dir}");
    }

    // ============================ 物品控件 ============================

    private static void BuildItemPrefab()
    {
        // 只需一个挂了 BagItemWidget 的空根:格块与图标由 widget 在运行时按形状自建。
        // 拖拽/点击落在子格块(带 raycast)上,事件冒泡到根上的 BagItemWidget 处理。
        var root = NewUI("BagItem", out var rootRt);
        rootRt.sizeDelta = new Vector2(80, 80); // 占位,运行时按包围盒覆盖
        root.AddComponent<BagItemWidget>();

        PrefabUtility.SaveAsPrefabAsset(root, ItemPrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log($"[BagPrefabBuilder] Built {ItemPrefabPath}");
    }

    // ============================ 面板 ============================

    private static void BuildPanelPrefab(GameObject itemPrefab)
    {
        var root = NewUI("BagPanel", out var rootRt);
        Stretch(rootRt, 0);

        var cg = root.AddComponent<CanvasGroup>();
        root.AddComponent<YOTOUIShow>(); // UIPageBase 要求;内部自取 CanvasGroup

        var dim = root.AddComponent<Image>(); // 半透遮罩 + 拦截点击
        dim.color = new Color(0f, 0f, 0f, 0.6f);

        var window = NewUI("Window", out var winRt, root.transform);
        winRt.anchorMin = winRt.anchorMax = winRt.pivot = new Vector2(0.5f, 0.5f);
        winRt.sizeDelta = new Vector2(980, 760);
        winRt.anchoredPosition = Vector2.zero;
        window.AddComponent<Image>().color = new Color(0.12f, 0.13f, 0.16f, 0.96f);

        var titleGo = NewUI("Title", out var titleRt, window.transform);
        titleRt.anchorMin = new Vector2(0, 1); titleRt.anchorMax = new Vector2(1, 1); titleRt.pivot = new Vector2(0.5f, 1);
        titleRt.anchoredPosition = new Vector2(20, -16); titleRt.sizeDelta = new Vector2(-40, 44);
        NewText(titleGo, "背包  (左键使用 / 右键旋转 / 拖拽移动)", 26, TextAlignmentOptions.Left);

        var capGo = NewUI("CapacityText", out var capRt, window.transform);
        capRt.anchorMin = new Vector2(1, 1); capRt.anchorMax = new Vector2(1, 1); capRt.pivot = new Vector2(1, 1);
        capRt.anchoredPosition = new Vector2(-90, -22); capRt.sizeDelta = new Vector2(220, 36);
        var capText = NewText(capGo, "0/80", 24, TextAlignmentOptions.Right);

        var closeBtn = BuildButton("CloseBtn", "X", window.transform,
            new Vector2(1, 1), new Vector2(-36, -36), new Vector2(56, 56), new Color(0.5f, 0.2f, 0.2f, 1f));

        var sortBtn = BuildButton("SortBtn", "整理", window.transform,
            new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(180, 60), new Color(0.2f, 0.4f, 0.55f, 1f));

        // 格盘容器:空 RectTransform,轴心/大小/位置由 BagPanel.BuildGrid 运行时按背包尺寸覆盖
        var gridGo = NewUI("GridRoot", out var gridRt, window.transform);
        gridRt.anchorMin = gridRt.anchorMax = new Vector2(0.5f, 0.5f);
        gridRt.pivot = new Vector2(0f, 1f);
        gridRt.anchoredPosition = Vector2.zero;
        gridRt.sizeDelta = new Vector2(800, 640);

        var panel = root.AddComponent<BagPanel>();
        panel.canvasGroup = cg;
        panel.uiType = UIEnum.BagPanel;
        panel.gridRoot = gridRt;
        panel.itemWidgetPrefab = itemPrefab;
        panel.sortBtn = sortBtn;
        panel.closeBtn = closeBtn;
        panel.capacityText = capText;
        panel.cellSize = 80f;
        panel.cellGap = 4f;
        panel.uiFont = _font; // 序列化注入,运行时数量标签用

        BuildContextMenu(root.transform, panel);
        BuildSplitDialog(root.transform, panel);

        PrefabUtility.SaveAsPrefabAsset(root, PanelPrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log($"[BagPrefabBuilder] Built {PanelPrefabPath}");
    }

    // ============================ 右键菜单 ============================

    private static void BuildContextMenu(Transform parent, BagPanel panel)
    {
        // 全屏 blocker(点空白关闭):近透明 Image + Button
        var menu = NewUI("ContextMenu", out var menuRt, parent);
        Stretch(menuRt, 0);
        var blockerImg = menu.AddComponent<Image>();
        blockerImg.color = new Color(0, 0, 0, 0.01f);
        var blockerBtn = menu.AddComponent<Button>();
        blockerBtn.targetGraphic = blockerImg;

        // 小菜单本体(运行时移到鼠标处),竖排 4 个按钮
        var box = NewUI("Panel", out var boxRt, menu.transform);
        boxRt.pivot = new Vector2(0, 1); // 左上为锚,出现在鼠标右下
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

    /// <summary>菜单内竖排按钮:高 52,按 index 往下排,左右各留 8 边距。</summary>
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

    // ============================ 拆分弹窗 ============================

    private static void BuildSplitDialog(Transform parent, BagPanel panel)
    {
        var dlg = NewUI("SplitDialog", out var dlgRt, parent);
        Stretch(dlgRt, 0);
        dlg.AddComponent<Image>().color = new Color(0, 0, 0, 0.5f); // 半透 dim,拦截背后点击

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

    /// <summary>构建水平 Slider(背景 + 填充 + 手柄),返回 Slider 组件。</summary>
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

    // ============================ 工具 ============================

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
        tmp.fontSize = size;
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
