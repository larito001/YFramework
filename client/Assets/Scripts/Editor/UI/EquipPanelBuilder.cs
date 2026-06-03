#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成装备界面预制体 EquipPanel.prefab 到 Resources/UI/Equip 下。
/// 外壳:全屏遮罩 + 居中窗口(返回 / 资源金币 / 标题「选择你的装备」 + 枪械/瞄准镜/子弹 三个横向滚动行 + 出发)。
/// 每行的装备卡由 <see cref="EquipPanel"/> 运行时按 <see cref="LoadoutSystem.CategoryItems"/> 构建。
///
/// 尺寸按"画布宽恒为 1920 单位"(CanvasScaler match=width)给,竖屏四边留边自适应。
/// 菜单:Tools/UI/Build EquipPanel Prefab
/// </summary>
public static class EquipPanelBuilder
{
    private const string Dir = "Assets/Resources/UI/Equip";
    private const string PrefabPath = Dir + "/EquipPanel.prefab";
    private const string ButtonPrefabPath = "Assets/Resources/UI/Common/CommonButton.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";

    private static TMP_FontAsset _font;
    private static GameObject _btnPrefab;

    [MenuItem("Tools/UI/Build EquipPanel Prefab")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        _btnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabPath);

        // ---------- 根 ----------
        var root = NewUI("EquipPanel", out var rootRt);
        Stretch(rootRt);
        root.AddComponent<Image>().color = new Color(0.10f, 0.11f, 0.14f, 1f);
        var cg = root.AddComponent<CanvasGroup>();
        root.AddComponent<YOTOUIShow>();

        // ---------- 顶部:返回 / 资源金币 ----------
        var backBtn = BuildButton("Btn_Back", "返回", root.transform, 44);
        var backRt = (RectTransform)backBtn.transform;
        backRt.anchorMin = backRt.anchorMax = new Vector2(0, 1); backRt.pivot = new Vector2(0, 1);
        backRt.anchoredPosition = new Vector2(40, -40); backRt.sizeDelta = new Vector2(220, 90);

        // 资源(右上):金币 + 体力,均为「图标 + 数值」胶囊,不写文字
        var coinText = UICurrencyPill.Build(root.transform, "Gold", UICurrencyPill.IconGold, _font,
            new Vector2(1, 1), new Vector2(-40, -40), new Vector2(300, 80));
        var energyText = UICurrencyPill.Build(root.transform, "Energy", UICurrencyPill.IconEnergy, _font,
            new Vector2(1, 1), new Vector2(-40, -132), new Vector2(300, 80));

        // ---------- 标题 ----------
        var titleGo = NewUI("Title", out var titleRt, root.transform);
        titleRt.anchorMin = new Vector2(0, 1); titleRt.anchorMax = new Vector2(1, 1); titleRt.pivot = new Vector2(0, 1);
        titleRt.anchoredPosition = new Vector2(40, -180); titleRt.sizeDelta = new Vector2(-80, 90);
        NewText(titleGo, "选择你的装备", 56, TextAlignmentOptions.Left);

        // ---------- 三个分类行 ----------
        var weaponRow = BuildSection("Weapon", "枪械", root.transform, -300);
        var scopeRow = BuildSection("Scope", "瞄准镜", root.transform, -820);
        var bulletRow = BuildSection("Bullet", "子弹", root.transform, -1340);

        // ---------- 出发 ----------
        var departBtn = BuildButton("Btn_Depart", "出发", root.transform, 56);
        var departRt = (RectTransform)departBtn.transform;
        departRt.anchorMin = departRt.anchorMax = new Vector2(0.5f, 0); departRt.pivot = new Vector2(0.5f, 0);
        departRt.anchoredPosition = new Vector2(0, 80); departRt.sizeDelta = new Vector2(700, 200);

        // ---------- 接脚本字段 ----------
        var panel = root.AddComponent<EquipPanel>();
        panel.canvasGroup = cg;
        panel.uiType = UIEnum.EquipPanel;
        panel.backBtn = backBtn;
        panel.coinText = coinText;
        panel.energyText = energyText;
        panel.weaponRow = weaponRow;
        panel.scopeRow = scopeRow;
        panel.bulletRow = bulletRow;
        panel.departBtn = departBtn;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[EquipPanelBuilder] Built {PrefabPath}");
    }

    // ============================ 构件 ============================

    /// <summary>一个分类块:标签 + 横向滚动行。返回行内容容器(EquipPanel 往里铺卡)。</summary>
    private static RectTransform BuildSection(string name, string label, Transform parent, float yTop)
    {
        // 块:顶部对齐、左右铺满(留边),固定高度
        var block = NewUI($"Section_{name}", out var blockRt, parent);
        blockRt.anchorMin = new Vector2(0, 1); blockRt.anchorMax = new Vector2(1, 1); blockRt.pivot = new Vector2(0.5f, 1);
        blockRt.anchoredPosition = new Vector2(0, yTop); blockRt.sizeDelta = new Vector2(-80, 480);

        // 标签
        var labelGo = NewUI("Label", out var labelRt, block.transform);
        labelRt.anchorMin = new Vector2(0, 1); labelRt.anchorMax = new Vector2(1, 1); labelRt.pivot = new Vector2(0, 1);
        labelRt.anchoredPosition = new Vector2(0, 0); labelRt.sizeDelta = new Vector2(0, 70);
        NewText(labelGo, label, 44, TextAlignmentOptions.Left);

        // 横向滚动行(标签下方)
        var scrollGo = NewUI("Scroll", out var scrollRt, block.transform);
        scrollRt.anchorMin = new Vector2(0, 0); scrollRt.anchorMax = new Vector2(1, 1); scrollRt.pivot = new Vector2(0.5f, 1);
        scrollRt.offsetMin = new Vector2(0, 0); scrollRt.offsetMax = new Vector2(0, -80); // 顶部让出标签
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = true; scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        var viewportGo = NewUI("Viewport", out var viewportRt, scrollGo.transform);
        Stretch(viewportRt);
        viewportGo.AddComponent<Image>().color = new Color(1, 1, 1, 0.03f);
        viewportGo.AddComponent<RectMask2D>();

        var contentGo = NewUI("Content", out var contentRt, viewportGo.transform);
        contentRt.anchorMin = new Vector2(0, 0); contentRt.anchorMax = new Vector2(0, 1); contentRt.pivot = new Vector2(0, 0.5f);
        contentRt.anchoredPosition = Vector2.zero;
        var hlg = contentGo.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 24;
        hlg.padding = new RectOffset(16, 16, 16, 16);
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewportRt; scroll.content = contentRt;
        return contentRt;
    }

    private static Button BuildButton(string name, string label, Transform parent, float fontSize)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(_btnPrefab, parent);
        go.name = name;
        go.SetActive(true);
        var text = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null) { text.text = label; text.fontSize = UITheme.Font(fontSize); }
        return go.GetComponent<Button>();
    }

    // ============================ 工具 ============================

    private static GameObject NewUI(string name, out RectTransform rt, Transform parent = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        rt = go.GetComponent<RectTransform>();
        if (parent != null) rt.SetParent(parent, false);
        return go;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static TextMeshProUGUI NewText(GameObject go, string text, float size, TextAlignmentOptions align)
    {
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = UITheme.Font(size); tmp.alignment = align; tmp.color = Color.white; tmp.raycastTarget = false;
        var font = _font != null ? _font : TMP_Settings.defaultFontAsset;
        if (font != null) tmp.font = font;
        return tmp;
    }
}
#endif
