#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成背包 UI 预制体到 Resources/UI/Bag 下，供 UIMgr/ResMgr 按路径加载。
/// 程序化构建（同 <see cref="WeaponPrefabBuilder"/> 约定）：Unity 自动解析脚本 GUID / TMP 字体，
/// 比手写 .prefab YAML 可靠。所有脚本字段引用在此接好，生成后无需在 Inspector 手动拖。
///
/// 菜单：Tools/Bag/Build Bag UI Prefabs
/// 产物：
///   - Assets/Resources/UI/Bag/BagItem.prefab   单个格子（BagItemView）
///   - Assets/Resources/UI/Bag/BagPanel.prefab  背包面板（BagPanel，含 ScrollView）
///
/// 美术后续只需：替换背景/按钮 Sprite、调整配色与布局，并把物品图标放到 item 配表 IconPath 指向的位置。
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

        // 先建格子预制体——面板的 ScrollView.itemPrefab 要引用它
        BuildItemPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var itemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ItemPrefabPath);
        BuildPanelPrefab(itemPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[BagPrefabBuilder] Done. Prefabs at {Dir}");
    }

    // ============================ 格子 ============================

    private static void BuildItemPrefab()
    {
        var root = NewUI("BagItem", out var rootRt);
        rootRt.sizeDelta = new Vector2(100, 100);

        var bg = root.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.35f);

        var button = root.AddComponent<Button>();
        button.targetGraphic = bg;

        // 图标
        var iconGo = NewUI("Icon", out var iconRt, root.transform);
        Stretch(iconRt, 6);
        var icon = iconGo.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        // 数量（右下角）
        var countGo = NewUI("Count", out var countRt, root.transform);
        countRt.anchorMin = new Vector2(1, 0);
        countRt.anchorMax = new Vector2(1, 0);
        countRt.pivot = new Vector2(1, 0);
        countRt.anchoredPosition = new Vector2(-6, 4);
        countRt.sizeDelta = new Vector2(60, 28);
        var count = NewText(countGo, "1", 22, TextAlignmentOptions.BottomRight);

        // 名称（顶部，可选）
        var nameGo = NewUI("Name", out var nameRt, root.transform);
        nameRt.anchorMin = new Vector2(0, 1);
        nameRt.anchorMax = new Vector2(1, 1);
        nameRt.pivot = new Vector2(0.5f, 1);
        nameRt.anchoredPosition = new Vector2(0, -2);
        nameRt.sizeDelta = new Vector2(0, 24);
        var nameText = NewText(nameGo, "", 18, TextAlignmentOptions.Top);

        // 接脚本引用
        var view = root.AddComponent<BagItemView>();
        view.button = button;
        view.icon = icon;
        view.count = count;
        view.nameText = nameText;

        PrefabUtility.SaveAsPrefabAsset(root, ItemPrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log($"[BagPrefabBuilder] Built {ItemPrefabPath}");
    }

    // ============================ 面板 ============================

    private static void BuildPanelPrefab(GameObject itemPrefab)
    {
        var root = NewUI("BagPanel", out var rootRt);
        Stretch(rootRt, 0);

        // UIPageBase 必需：CanvasGroup + YOTOUIShow（先加，避免 RequireComponent 重复添加）
        var cg = root.AddComponent<CanvasGroup>();
        var show = root.AddComponent<YOTOUIShow>();
        SetField(show, "canvasGroup", cg);

        // 半透明遮罩背景
        var dim = root.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.6f);

        // 窗口
        var window = NewUI("Window", out var winRt, root.transform);
        winRt.anchorMin = winRt.anchorMax = winRt.pivot = new Vector2(0.5f, 0.5f);
        winRt.sizeDelta = new Vector2(900, 640);
        winRt.anchoredPosition = Vector2.zero;
        var winBg = window.AddComponent<Image>();
        winBg.color = new Color(0.12f, 0.13f, 0.16f, 0.96f);

        // 标题
        var titleGo = NewUI("Title", out var titleRt, window.transform);
        titleRt.anchorMin = new Vector2(0, 1); titleRt.anchorMax = new Vector2(1, 1); titleRt.pivot = new Vector2(0.5f, 1);
        titleRt.anchoredPosition = new Vector2(0, -16); titleRt.sizeDelta = new Vector2(-40, 48);
        NewText(titleGo, "背包", 34, TextAlignmentOptions.Left);

        // 容量文本（右上）
        var capGo = NewUI("CapacityText", out var capRt, window.transform);
        capRt.anchorMin = new Vector2(1, 1); capRt.anchorMax = new Vector2(1, 1); capRt.pivot = new Vector2(1, 1);
        capRt.anchoredPosition = new Vector2(-90, -22); capRt.sizeDelta = new Vector2(200, 36);
        var capText = NewText(capGo, "0/30", 24, TextAlignmentOptions.Right);

        // 关闭按钮（右上角）
        var closeBtn = BuildButton("CloseBtn", "X", window.transform, out _,
            new Vector2(1, 1), new Vector2(-40, -40), new Vector2(56, 56), new Color(0.5f, 0.2f, 0.2f, 1f));

        // 整理按钮（底部）
        var sortBtn = BuildButton("SortBtn", "整理", window.transform, out _,
            new Vector2(0.5f, 0), new Vector2(0, 44), new Vector2(180, 60), new Color(0.2f, 0.4f, 0.55f, 1f));

        // 滚动视图
        var scrollGo = NewUI("ScrollView", out var scrollRt, window.transform);
        scrollRt.anchorMin = new Vector2(0, 0); scrollRt.anchorMax = new Vector2(1, 1); scrollRt.pivot = new Vector2(0.5f, 0.5f);
        scrollRt.offsetMin = new Vector2(24, 96);   // 底部给整理按钮留空
        scrollRt.offsetMax = new Vector2(-24, -72); // 顶部给标题留空
        var scrollBgImg = scrollGo.AddComponent<Image>();
        scrollBgImg.color = new Color(1f, 1f, 1f, 0.03f);

        // viewport（裁剪）
        var viewportGo = NewUI("Viewport", out var viewportRt, scrollGo.transform);
        Stretch(viewportRt, 0);
        viewportGo.AddComponent<RectMask2D>();

        // content
        var contentGo = NewUI("Content", out var contentRt, viewportGo.transform);
        contentRt.anchorMin = new Vector2(0, 1); contentRt.anchorMax = new Vector2(0, 1); contentRt.pivot = new Vector2(0, 1);
        contentRt.anchoredPosition = Vector2.zero;

        var scroll = scrollGo.AddComponent<YOTOScrollView>();
        SetEnum(scroll, "layout", 0);        // Vertical
        SetInt(scroll, "columns", 5);
        SetInt(scroll, "spacing", 10);
        SetField(scroll, "content", contentRt);
        SetField(scroll, "viewport", viewportRt);
        SetField(scroll, "itemPrefab", itemPrefab);

        // 接脚本引用
        var panel = root.AddComponent<BagPanel>();
        panel.canvasGroup = cg;
        panel.uiType = UIEnum.BagPanel;
        panel.scrollView = scroll;
        panel.sortBtn = sortBtn;
        panel.closeBtn = closeBtn;
        panel.capacityText = capText;
        panel.poolSize = 40;

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

    /// <summary>四边拉伸填满父级，inset 为统一内边距。</summary>
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

    private static Button BuildButton(string name, string label, Transform parent, out TextMeshProUGUI labelTmp,
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
        labelTmp = NewText(labelGo, label, 26, TextAlignmentOptions.Center);

        return btn;
    }

    private static void SetField(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p != null) { p.objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        else Debug.LogWarning($"[BagPrefabBuilder] 字段未找到: {target.GetType().Name}.{field}");
    }

    private static void SetInt(Object target, string field, int value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p != null) { p.intValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }

    private static void SetEnum(Object target, string field, int enumIndex)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p != null) { p.enumValueIndex = enumIndex; so.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
#endif
