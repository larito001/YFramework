#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成商店面板预制体 ShopPanel.prefab 到 Resources/UI/Shop 下,供 UIMgr/ResMgr 按路径加载。
/// 只搭外壳:全屏遮罩 + 居中窗口(返回 / 资源金币 / 商人头像 + 3 列卡片滚动网格 + 底部分类页签)。
/// 卡片(图片/名称/价格)由 <see cref="ShopPanel"/> 运行时按 <see cref="ShopSystem.CatalogOf"/> 构建。
///
/// 尺寸按"画布宽恒为 1920 单位"(CanvasScaler match=width)给,竖屏下窗口四边留边自适应。
/// 菜单:Tools/UI/Build ShopPanel Prefab
/// </summary>
public static class ShopPanelBuilder
{
    private const string Dir = "Assets/Resources/UI/Shop";
    private const string PrefabPath = Dir + "/ShopPanel.prefab";
    private const string ButtonPrefabPath = "Assets/Resources/UI/Common/CommonButton.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";

    private static TMP_FontAsset _font;
    private static GameObject _btnPrefab;

    [MenuItem("Tools/UI/Build ShopPanel Prefab")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        _btnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabPath);

        // ---------- 根(全屏遮罩 + CanvasGroup + YOTOUIShow + ShopPanel)----------
        var root = NewUI("ShopPanel", out var rootRt);
        Stretch(rootRt);
        var mask = root.AddComponent<Image>();
        mask.color = new Color(0f, 0f, 0f, 0.6f);
        var cg = root.AddComponent<CanvasGroup>();
        root.AddComponent<YOTOUIShow>();

        // ---------- 居中窗口 ----------
        var win = NewUI("Window", out var winRt, root.transform);
        winRt.anchorMin = Vector2.zero; winRt.anchorMax = Vector2.one;
        winRt.offsetMin = new Vector2(50, 50); winRt.offsetMax = new Vector2(-50, -50);
        win.AddComponent<Image>().color = new Color(0.12f, 0.13f, 0.16f, 1f);

        // 返回(左上)
        var backBtn = BuildButton("Btn_Back", "返回", win.transform, 44);
        var backRt = (RectTransform)backBtn.transform;
        backRt.anchorMin = backRt.anchorMax = new Vector2(0, 1); backRt.pivot = new Vector2(0, 1);
        backRt.anchoredPosition = new Vector2(30, -25); backRt.sizeDelta = new Vector2(220, 90);

        // 资源金币(右上)
        var coinGo = NewUI("Coin", out var coinRt, win.transform);
        coinRt.anchorMin = coinRt.anchorMax = new Vector2(1, 1); coinRt.pivot = new Vector2(1, 1);
        coinRt.anchoredPosition = new Vector2(-30, -25); coinRt.sizeDelta = new Vector2(380, 150);
        var coinText = NewText(coinGo, "金币 0\n钻石 0", 40, TextAlignmentOptions.TopRight, Color.white);

        // 商人头像(中上,圆形占位)
        var avatarGo = NewUI("Merchant", out var avatarRt, win.transform);
        avatarRt.anchorMin = avatarRt.anchorMax = new Vector2(0.5f, 1); avatarRt.pivot = new Vector2(0.5f, 1);
        avatarRt.anchoredPosition = new Vector2(0, -30); avatarRt.sizeDelta = new Vector2(300, 300);
        var avatarImg = avatarGo.AddComponent<Image>();
        avatarImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        avatarImg.color = new Color(0.85f, 0.85f, 0.9f, 1f);

        // ---------- 滚动网格(3 列)----------
        var scrollGo = NewUI("Scroll", out var scrollRt, win.transform);
        scrollRt.anchorMin = Vector2.zero; scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = new Vector2(30, 240);   // 底部让出页签
        scrollRt.offsetMax = new Vector2(-30, -380); // 顶部让出头像
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true;

        var viewportGo = NewUI("Viewport", out var viewportRt, scrollGo.transform);
        Stretch(viewportRt);
        viewportGo.AddComponent<Image>().color = new Color(1, 1, 1, 0.02f);
        viewportGo.AddComponent<RectMask2D>();

        var contentGo = NewUI("Content", out var contentRt, viewportGo.transform);
        contentRt.anchorMin = new Vector2(0, 1); contentRt.anchorMax = new Vector2(1, 1); contentRt.pivot = new Vector2(0.5f, 1);
        contentRt.anchoredPosition = Vector2.zero;
        var glg = contentGo.AddComponent<GridLayoutGroup>();
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 3;
        glg.cellSize = new Vector2(550, 560);
        glg.spacing = new Vector2(30, 30);
        glg.padding = new RectOffset(20, 20, 20, 20);
        glg.childAlignment = TextAnchor.UpperCenter;
        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewportRt; scroll.content = contentRt;

        // ---------- 底部页签:武器 / 瞄准镜 / 子弹 / 准备 ----------
        var bar = NewUI("Tabs", out var barRt, win.transform);
        barRt.anchorMin = new Vector2(0, 0); barRt.anchorMax = new Vector2(1, 0); barRt.pivot = new Vector2(0.5f, 0);
        barRt.offsetMin = new Vector2(30, 30); barRt.offsetMax = new Vector2(-30, 210);
        var hlg = bar.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 24;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;

        var tabWeapon = BuildButton("Tab_Weapon", "武器商店", bar.transform, 40);
        var tabScope = BuildButton("Tab_Scope", "瞄准镜商店", bar.transform, 40);
        var tabBullet = BuildButton("Tab_Bullet", "子弹商店", bar.transform, 40);
        var prepareBtn = BuildButton("Btn_Prepare", "准备", bar.transform, 40);

        // ---------- 接脚本字段 ----------
        var panel = root.AddComponent<ShopPanel>();
        panel.canvasGroup = cg;
        panel.uiType = UIEnum.ShopPanel;
        panel.backBtn = backBtn;
        panel.coinText = coinText;
        panel.merchantAvatar = avatarImg;
        panel.grid = contentRt;
        panel.tabWeapon = tabWeapon;
        panel.tabScope = tabScope;
        panel.tabBullet = tabBullet;
        panel.prepareBtn = prepareBtn;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ShopPanelBuilder] Built {PrefabPath}");
    }

    // ============================ 工具 ============================

    private static Button BuildButton(string name, string label, Transform parent, float fontSize)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(_btnPrefab, parent);
        go.name = name;
        go.SetActive(true);
        var text = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null) { text.text = label; text.fontSize = UITheme.Font(fontSize); }
        return go.GetComponent<Button>();
    }

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

    private static TextMeshProUGUI NewText(GameObject go, string text, float size, TextAlignmentOptions align, Color color)
    {
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = UITheme.Font(size); tmp.alignment = align; tmp.color = color; tmp.raycastTarget = false;
        var font = _font != null ? _font : TMP_Settings.defaultFontAsset;
        if (font != null) tmp.font = font;
        return tmp;
    }
}
#endif
