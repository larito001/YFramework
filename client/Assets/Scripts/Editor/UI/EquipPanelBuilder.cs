#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成装备界面预制体 EquipPanel.prefab 到 Resources/UI/Equip 下,供 UIMgr/ResMgr 按路径加载。
/// 外壳(从美术返工后的预制体反推):
///   根:CanvasGroup + YOTOUIShow + EquipPanel(无 Image,背景由 bg.prefab 提供)
///     ├ bg       (Resources/UI/bg)      全屏背景图
///     ├ TransBg  (Resources/UI/TransBg) 窗口半透明底板(居中)
///     ├ backBtn  (Resources/UI/backBtn) 返回(左上,art-kit 按钮)
///     ├ Gold / Energy 资源胶囊(右上,UICurrencyPill,图标运行时动态加载)
///     ├ Title    标题「选择你的装备」(左对齐,顶部)
///     ├ Section_Weapon / Section_Scope / Section_Bullet 三个分类块(标签 + 横向滚动行)
///     └ Btn_Depart (Resources/UI/Common/CommonButton) 出发(底部居中,art-kit 按钮)
///
/// 每行的装备卡由 <see cref="EquipPanel"/> 运行时按 <see cref="LoadoutSystem.CategoryItems"/> 构建(Builder 只还原外壳)。
/// 尺寸按"画布宽恒为 1920 单位"(CanvasScaler match=width)给;字号统一走 <see cref="UITheme.Font"/>(×2)。
/// 菜单:Tools/UI/Build EquipPanel Prefab
/// </summary>
public static class EquipPanelBuilder
{
    private const string Dir = "Assets/Resources/UI/Equip";
    private const string PrefabPath = Dir + "/EquipPanel.prefab";
    private const string BgPrefabPath = "Assets/Resources/UI/bg.prefab";
    private const string TransBgPrefabPath = "Assets/Resources/UI/TransBg.prefab";
    private const string BackBtnPrefabPath = "Assets/Resources/UI/backBtn.prefab";
    private const string DepartBtnPrefabPath = "Assets/Resources/UI/Common/CommonButton.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";

    // 三个分类块在根内的顶部纵向位置(锚到顶,pivot(0.5,1)):枪械 → 瞄准镜 → 子弹,自上而下。
    private const float WeaponTop = -393f, ScopeTop = -913f, BulletTop = -1433f;

    private static TMP_FontAsset _font;
    private static GameObject _bgPrefab, _transBgPrefab, _backBtnPrefab, _departBtnPrefab;

    [MenuItem("Tools/UI/Build EquipPanel Prefab")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        _bgPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BgPrefabPath);
        _transBgPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TransBgPrefabPath);
        _backBtnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BackBtnPrefabPath);
        _departBtnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DepartBtnPrefabPath);

        // ---------- 根(无 Image;背景交给 bg.prefab)----------
        var root = NewUI("EquipPanel", out var rootRt);
        Stretch(rootRt);
        var cg = root.AddComponent<CanvasGroup>();
        root.AddComponent<YOTOUIShow>();

        // 全屏背景图(铺满)
        var bg = (GameObject)PrefabUtility.InstantiatePrefab(_bgPrefab, root.transform);
        bg.name = "bg";
        var bgRt = (RectTransform)bg.transform;
        bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.anchoredPosition = Vector2.zero;
        bgRt.sizeDelta = new Vector2(5504.974f, 3669.982f);

        // 窗口半透明底板(居中)
        var transBg = (GameObject)PrefabUtility.InstantiatePrefab(_transBgPrefab, root.transform);
        transBg.name = "TransBg";
        var transRt = (RectTransform)transBg.transform;
        transRt.anchorMin = transRt.anchorMax = transRt.pivot = new Vector2(0.5f, 0.5f);
        transRt.anchoredPosition = new Vector2(-16.737f, -85.43f);
        transRt.sizeDelta = new Vector2(1819.367f, 3035.621f);

        // ---------- 返回(左上,art-kit 按钮)----------
        var backGo = (GameObject)PrefabUtility.InstantiatePrefab(_backBtnPrefab, root.transform);
        backGo.name = "backBtn";
        var backRt = (RectTransform)backGo.transform;
        backRt.anchorMin = backRt.anchorMax = new Vector2(0, 1); backRt.pivot = new Vector2(0.5f, 0.5f);
        backRt.anchoredPosition = new Vector2(155.2f, -111.9f); backRt.sizeDelta = new Vector2(243.246f, 201.326f);
        var backBtn = backGo.GetComponent<Button>();

        // ---------- 资源(右上):金币 + 体力胶囊(图标运行时动态加载,不烤 Sprite)----------
        var coinText = UICurrencyPill.Build(root.transform, "Gold", UICurrencyPill.IconGold, _font,
            new Vector2(1, 1), new Vector2(-40, -40), new Vector2(300, 80));
        var energyText = UICurrencyPill.Build(root.transform, "Energy", UICurrencyPill.IconEnergy, _font,
            new Vector2(1, 1), new Vector2(-40, -132), new Vector2(300, 80));

        // ---------- 标题 ----------
        var titleGo = NewUI("Title", out var titleRt, root.transform);
        titleRt.anchorMin = new Vector2(0, 1); titleRt.anchorMax = new Vector2(1, 1); titleRt.pivot = new Vector2(0, 1);
        titleRt.anchoredPosition = new Vector2(40, -273); titleRt.sizeDelta = new Vector2(-80, 90);
        NewText(titleGo, "选择你的装备", 56, TextAlignmentOptions.Left);

        // ---------- 三个分类行 ----------
        var weaponRow = BuildSection("Weapon", "枪械", root.transform, WeaponTop);
        var scopeRow = BuildSection("Scope", "瞄准镜", root.transform, ScopeTop);
        var bulletRow = BuildSection("Bullet", "子弹", root.transform, BulletTop);

        // ---------- 出发(底部居中,art-kit CommonButton)----------
        var departGo = (GameObject)PrefabUtility.InstantiatePrefab(_departBtnPrefab, root.transform);
        departGo.name = "Btn_Depart";
        var departRt = (RectTransform)departGo.transform;
        departRt.anchorMin = departRt.anchorMax = new Vector2(0.5f, 0); departRt.pivot = new Vector2(0.5f, 0);
        departRt.anchoredPosition = new Vector2(0, 230); departRt.sizeDelta = new Vector2(700, 200);
        var departText = departGo.GetComponentInChildren<TextMeshProUGUI>(true);
        if (departText != null) { departText.text = "出发"; departText.fontSize = UITheme.Font(56); }
        var departBtn = departGo.GetComponent<Button>();

        // 兄弟顺序:bg, TransBg, backBtn, Gold, Energy, Title, Section_Weapon, Section_Scope, Section_Bullet, Btn_Depart
        // (InstantiatePrefab / NewUI 按创建顺序入栈,与预制体一致,无需手动重排)

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

    /// <summary>一个分类块:标签 + 横向滚动行。返回行内容容器(EquipPanel 往里铺卡)。
    /// 还原预制体:块锚顶、左右留边、高 480;标签顶部高 70;滚动行让出标签后铺满,Clamped 横向滚动。</summary>
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
        scrollRt.anchoredPosition = new Vector2(0, -80); scrollRt.sizeDelta = new Vector2(0, -80); // 顶部让出标签
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = true; scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        var viewportGo = NewUI("Viewport", out var viewportRt, scrollGo.transform);
        Stretch(viewportRt);
        viewportGo.AddComponent<Image>().color = new Color(1, 1, 1, 0.03f);
        viewportGo.AddComponent<RectMask2D>();

        var contentGo = NewUI("Content", out var contentRt, viewportGo.transform);
        contentRt.anchorMin = new Vector2(0, 0); contentRt.anchorMax = new Vector2(0, 1); contentRt.pivot = new Vector2(0, 0.5f);
        contentRt.anchoredPosition = Vector2.zero; contentRt.sizeDelta = new Vector2(0, 100);
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
