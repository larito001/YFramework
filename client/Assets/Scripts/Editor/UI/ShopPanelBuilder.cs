#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成商店面板预制体 ShopPanel.prefab 到 Resources/UI/Shop 下,供 UIMgr/ResMgr 按路径加载。
/// 外壳(从美术返工后的预制体反推):
///   根:全屏黑遮罩 + CanvasGroup + YOTOUIShow + ShopPanel
///     ├ bg      (Resources/UI/bg)      全屏背景图
///     └ Window  逻辑容器(无底板)
///         ├ backBtn (Resources/UI/backBtn) 返回(左上)
///         ├ Gold / Energy 资源胶囊(右上)
///         ├ Merchant 武器展示区(上 ~40% 屏,运行时填入 3D 转台模型)
///         ├ Tabs    分类页签(展示区与列表「中间」,3×绿色通用按钮+文本前图标:武器/瞄准镜/子弹)
///         └ Scroll  武器列表(下 ~43% 屏,3 列卡片网格)
///
/// 自上而下固定为「展示区 → 页签 → 列表」三段,均用比例锚点摆好,不再由 <see cref="ShopPanel"/> 运行时重排。
/// 卡片(图片/名称/价格)仍由 <see cref="ShopPanel"/> 运行时按 <see cref="ShopSystem.CatalogOf"/> 构建。
/// 菜单:Tools/UI/Build ShopPanel Prefab
/// </summary>
public static class ShopPanelBuilder
{
    private const string Dir = "Assets/Resources/UI/Shop";
    private const string PrefabPath = Dir + "/ShopPanel.prefab";
    private const string BgPrefabPath = "Assets/Resources/UI/bg.prefab";
    private const string BackBtnPrefabPath = "Assets/Resources/UI/backBtn.prefab";
    private const string CommonButtonYellowPath = "Assets/Resources/UI/Common/CommonButtonYellow.prefab"; // 看广告按钮
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";

    // 分类页签图标(文本前带 icon):武器=枪 / 瞄准镜=准星 / 子弹=弹药
    private const string IconWeapon = "Assets/Art/UI/HONETi/Textures/Common/Icons/256x256px/Gun256x256.png";
    private const string IconScope = "Assets/Art/UI/NewUI/Shared/Icons/PictoIcon/256/target_1.png";
    private const string IconBullet = "Assets/Art/UI/HONETi/Textures/Common/Icons/256x256px/Ammunition256x256.png";

    // 三段竖向分区(Window 内比例锚点):展示区 → 页签 → 列表,自上而下。
    private const float DisplayTop = 0.93f, DisplayBottom = 0.53f; // 武器展示区
    private const float TabsTop = 0.52f, TabsBottom = 0.45f;       // 分类页签(夹在展示区与列表中间)
    private const float ListTop = 0.43f;                           // 武器列表(0 ~ ListTop)

    private static TMP_FontAsset _font;
    private static GameObject _bgPrefab, _backBtnPrefab;

    [MenuItem("Tools/UI/Build ShopPanel Prefab")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        _bgPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BgPrefabPath);
        _backBtnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BackBtnPrefabPath);

        // ---------- 根(全屏遮罩 + CanvasGroup + YOTOUIShow + ShopPanel)----------
        var root = NewUI("ShopPanel", out var rootRt);
        Stretch(rootRt);
        var mask = root.AddComponent<Image>();
        mask.color = UITheme.Scrim; // 统一弹窗遮罩(深冷色,非纯黑)
        var cg = root.AddComponent<CanvasGroup>();
        root.AddComponent<YOTOUIShow>();

        // 全屏背景图(铺满,压住黑遮罩)
        var bg = (GameObject)PrefabUtility.InstantiatePrefab(_bgPrefab, root.transform);
        var bgRt = (RectTransform)bg.transform;
        bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.anchoredPosition = Vector2.zero;
        bgRt.sizeDelta = new Vector2(5504.974f, 3669.982f);

        // ---------- 逻辑容器 Window(无底板)----------
        var win = NewUI("Window", out var winRt, root.transform);
        winRt.anchorMin = Vector2.zero; winRt.anchorMax = Vector2.one;
        winRt.offsetMin = new Vector2(50, 50); winRt.offsetMax = new Vector2(-50, -50);

        // 返回(左上)
        var backGo = (GameObject)PrefabUtility.InstantiatePrefab(_backBtnPrefab, win.transform);
        var backRt = (RectTransform)backGo.transform;
        backRt.anchorMin = backRt.anchorMax = new Vector2(0, 1); backRt.pivot = new Vector2(0.5f, 0.5f);
        backRt.anchoredPosition = new Vector2(155.2f, -111.9f); backRt.sizeDelta = new Vector2(243.246f, 201.326f);
        var backBtn = backGo.GetComponent<Button>();

        // 资源(右上):金币 + 体力,均为「图标 + 数值」胶囊,不写文字
        var coinText = UICurrencyPill.Build(win.transform, "Gold", UICurrencyPill.IconGold, _font,
            new Vector2(1, 1), new Vector2(-30, -25), new Vector2(300, 80));
        var energyText = UICurrencyPill.Build(win.transform, "Energy", UICurrencyPill.IconEnergy, _font,
            new Vector2(1, 1), new Vector2(-30, -117), new Vector2(300, 80));

        // ---------- 武器展示区(上段;运行时填入 3D 转台模型,占位图为圆形 Knob)----------
        var avatarGo = NewUI("Merchant", out var avatarRt, win.transform);
        avatarRt.anchorMin = new Vector2(0.06f, DisplayBottom); avatarRt.anchorMax = new Vector2(0.94f, DisplayTop);
        avatarRt.offsetMin = Vector2.zero; avatarRt.offsetMax = Vector2.zero;
        var avatarImg = avatarGo.AddComponent<Image>();
        avatarImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        avatarImg.color = new Color(0.85f, 0.85f, 0.9f, 1f);

        // ---------- 列表深色底板(手调:衬在列表后;层级在页签之前)----------
        var listBgGo = NewUI("listBg", out var listBgRt, win.transform);
        listBgRt.anchorMin = new Vector2(0, 1); listBgRt.anchorMax = new Vector2(0, 1); listBgRt.pivot = new Vector2(0.5f, 0.5f);
        listBgRt.anchoredPosition = new Vector2(911.79f, -2486.1f); listBgRt.sizeDelta = new Vector2(1756.4f, 1530.9f);
        listBgGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.42745098f);

        // ---------- 分类页签:武器 / 瞄准镜 / 子弹(与列表同宽、矮身、紧贴列表上沿)----------
        // 锚到列表上沿(ListTop),底边距列表 8px(紧贴),固定高 160(和任务页签一致),左右 30 与列表对齐。
        var bar = NewUI("Tabs", out var barRt, win.transform);
        barRt.anchorMin = new Vector2(0, ListTop); barRt.anchorMax = new Vector2(1, ListTop);
        barRt.pivot = new Vector2(0.5f, 0);
        barRt.offsetMin = new Vector2(30, 8); barRt.offsetMax = new Vector2(-30, 168);
        var hlg = bar.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 24;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;

        // 页签:绿色通用按钮 + 文本前图标(选中绿/未选灰由 ShopPanel.SetTabColor 染 Bg/InnerBorder1/文字控制)。
        var commonBtn = AssetDatabase.LoadAssetAtPath<GameObject>(UIButtonFactory.CommonButtonPath);
        var tabWeapon = UIButtonFactory.BuildWithLeftIcon(commonBtn, "Tab_Weapon", "武器", LoadSprite(IconWeapon), bar.transform, fontSize: 30);
        var tabScope = UIButtonFactory.BuildWithLeftIcon(commonBtn, "Tab_Scope", "瞄准镜", LoadSprite(IconScope), bar.transform, fontSize: 30);
        var tabBullet = UIButtonFactory.BuildWithLeftIcon(commonBtn, "Tab_Bullet", "子弹", LoadSprite(IconBullet), bar.transform, fontSize: 30);

        // ---------- 武器列表:3 列卡片滚动网格(下段)----------
        var scrollGo = NewUI("Scroll", out var scrollRt, win.transform);
        scrollRt.anchorMin = new Vector2(0, 0); scrollRt.anchorMax = new Vector2(1, ListTop);
        scrollRt.offsetMin = new Vector2(30, 30);  // 底部留 30 边距
        scrollRt.offsetMax = new Vector2(-30, 0);
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

        // ---------- 看广告领金币按钮(展示区左下角;Window 最后兄弟→盖在运行时模型预览之上,可点)----------
        var adPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CommonButtonYellowPath);
        var adGo = (GameObject)PrefabUtility.InstantiatePrefab(adPrefab, win.transform);
        adGo.name = "AdGoldBtn";
        ((RectTransform)adGo.transform).localScale = Vector3.one; // 复位 art-kit 烤的缩放
        var adRt = (RectTransform)adGo.transform;
        adRt.anchorMin = adRt.anchorMax = new Vector2(0.06f, DisplayBottom); // 展示区左下角
        adRt.pivot = new Vector2(0f, 0f);
        adRt.sizeDelta = new Vector2(340f, 110f);
        adRt.anchoredPosition = new Vector2(12f, 14f); // 略内缩,不贴边
        var adBtn = adGo.GetComponent<Button>();
        var adBgTf = adGo.transform.Find("Bg");
        if (adBgTf != null) adBtn.targetGraphic = adBgTf.GetComponent<Image>(); // 置灰逻辑染这块底
        var adImgChild = adGo.transform.Find("Image"); // 黄按钮的图标位,广告按钮不用→隐藏
        if (adImgChild != null) adImgChild.gameObject.SetActive(false);
        var adLabel = adGo.GetComponentInChildren<TextMeshProUGUI>(true);
        if (adLabel != null)
        {
            // 黄按钮的文字 RT 是为「图标+文字」竖排留的(纵向内缩 132),宽按钮上会塌到底部;这里铺满居中。
            var lblRt = (RectTransform)adLabel.transform;
            lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one; lblRt.offsetMin = Vector2.zero; lblRt.offsetMax = Vector2.zero;
            adLabel.alignment = TextAlignmentOptions.Center;
            adLabel.text = "看广告 +1000"; adLabel.fontSize = UITheme.Font(26);
        }

        // ---------- 分割线(手调:浅绿,挂根上盖在窗口之上)----------
        var lineGo = NewUI("line", out var lineRt, root.transform);
        lineRt.anchorMin = lineRt.anchorMax = lineRt.pivot = new Vector2(0.5f, 0.5f);
        lineRt.anchoredPosition = new Vector2(1.7113f, -235.2517f); lineRt.sizeDelta = new Vector2(1759.6661f, 24.4746f);
        lineGo.AddComponent<Image>().color = new Color(0.65882355f, 0.7764706f, 0.5254902f, 1f);

        // ---------- 接脚本字段 ----------
        var panel = root.AddComponent<ShopPanel>();
        panel.canvasGroup = cg;
        panel.uiType = UIEnum.ShopPanel;
        panel.backBtn = backBtn;
        panel.coinText = coinText;
        panel.energyText = energyText;
        panel.merchantAvatar = avatarImg;
        panel.grid = contentRt;
        panel.tabWeapon = tabWeapon;
        panel.tabScope = tabScope;
        panel.tabBullet = tabBullet;
        panel.adGoldButton = adBtn;
        panel.adGoldLabel = adLabel;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ShopPanelBuilder] Built {PrefabPath}");
    }

    // ============================ 工具 ============================

    private static Sprite LoadSprite(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null) Debug.LogWarning($"[ShopPanelBuilder] 找不到精灵 {path}");
        return s;
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
