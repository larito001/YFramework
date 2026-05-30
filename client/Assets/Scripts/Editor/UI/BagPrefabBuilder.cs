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

    [MenuItem("Tools/Bag/Build Bag UI Prefabs")]
    public static void BuildAll()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);

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
        var root = NewUI("BagItem", out var rootRt);
        rootRt.sizeDelta = new Vector2(80, 80); // 占位,运行时按有效占格尺寸覆盖

        // 背景必须 raycastTarget=true 才能接收拖拽/点击
        var bg = root.AddComponent<Image>();
        bg.color = new Color(0.25f, 0.5f, 0.7f, 0.85f);
        bg.raycastTarget = true;

        var iconGo = NewUI("Icon", out var iconRt, root.transform);
        Stretch(iconRt, 4);
        var icon = iconGo.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        var nameGo = NewUI("Name", out var nameRt, root.transform);
        nameRt.anchorMin = new Vector2(0, 0); nameRt.anchorMax = new Vector2(1, 0); nameRt.pivot = new Vector2(0.5f, 0);
        nameRt.anchoredPosition = new Vector2(0, 2); nameRt.sizeDelta = new Vector2(-4, 22);
        var nameText = NewText(nameGo, "", 16, TextAlignmentOptions.Bottom);

        var widget = root.AddComponent<BagItemWidget>();
        widget.background = bg;
        widget.icon = icon;
        widget.nameText = nameText;

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

        PrefabUtility.SaveAsPrefabAsset(root, PanelPrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log($"[BagPrefabBuilder] Built {PanelPrefabPath}");
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
        if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
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
