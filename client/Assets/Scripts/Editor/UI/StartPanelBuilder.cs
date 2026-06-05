#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using YOTO;

/// <summary>
/// 一键生成主界面(大厅)预制体 StartPanel.prefab 到 Resources/UI/Boot 下,供 UIMgr/ResMgr 按路径加载。
/// 外壳从美术返工后的预制体反推(art-kit:bg / Button_Plus / CommonButton(Yellow));脚本字段在此接好。
///
/// 竖屏手机布局(画布宽恒为 1920 单位,竖屏高约 4000+):
///   背景:bg(全屏铺图) + 中上 logo 图
///   左上:设置(方形按钮,UnityEngine.UI.Button + setting 图标)
///   右上:体力胶囊 + 紧随其右上角的「广告补充」+ 按钮(Button_Plus);金币胶囊(体力下一行)
///   底部:左竖排 任务/商店(CommonButtonYellow) · 中 准备(CommonButton 主按钮) · 右竖排 图鉴/排行榜
///
/// 体力/金币胶囊:图标不烤进预制体——Icon 上挂 <see cref="CurrencyIconBinder"/>,运行时按币种从
/// Resources/UI/Icons 动态加载(方便换图)。
///
/// 菜单:Tools/UI/Build StartPanel Prefab
/// </summary>
public static class StartPanelBuilder
{
    private const string Dir = "Assets/Resources/UI/Boot";
    private const string PrefabPath = Dir + "/StartPanel.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";

    // ---- art-kit 预制体 ----
    private const string BgPrefabPath = "Assets/Resources/UI/bg.prefab";                                // 全屏背景
    private const string PlusBtnPrefabPath = "Assets/Art/UI/NewUI/Theme_Blue/Prefabs/Prefabs_Button/Button_Plus.prefab"; // 体力广告「+」
    private const string CommonButtonPath = "Assets/Resources/UI/Common/CommonButton.prefab";           // 中间「准备」主按钮
    private const string CommonButtonYellowPath = "Assets/Resources/UI/Common/CommonButtonYellow.prefab"; // 侧边功能按钮

    // ---- 美术图(Sprite,运行时由 art-kit 引用,这里按预制体还原)----
    private const string SettingIconPath = "Assets/Art/UI/NewUI/Theme_Blue/Sprites/setting.png";
    private const string LogoSpritePath = "Assets/Art/UI/NewUI/Theme_Blue/Sprites/logo_transparent.png";
    private const string ShopIconPath = "Assets/Art/UI/NewUI/Theme_Blue/Sprites/商店.png";
    private const string CodexIconPath = "Assets/Art/UI/NewUI/Theme_Blue/Sprites/图鉴.png";
    private const string LeaderboardIconPath = "Assets/Art/UI/NewUI/Theme_Blue/Sprites/排行榜.png";

    private static TMP_FontAsset _font;
    private static GameObject _bgPrefab, _plusPrefab, _commonButton, _commonButtonYellow;

    [MenuItem("Tools/UI/Build StartPanel Prefab")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);

        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        _bgPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BgPrefabPath);
        _plusPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlusBtnPrefabPath);
        _commonButton = AssetDatabase.LoadAssetAtPath<GameObject>(CommonButtonPath);
        _commonButtonYellow = AssetDatabase.LoadAssetAtPath<GameObject>(CommonButtonYellowPath);
        if (_bgPrefab == null || _plusPrefab == null || _commonButton == null || _commonButtonYellow == null)
        {
            Debug.LogError("[StartPanelBuilder] 缺少 art-kit 预制体(bg / Button_Plus / CommonButton / CommonButtonYellow),无法生成。");
            return;
        }

        // ---------- 根(全屏 + CanvasGroup + YOTOUIShow + StartPanel)----------
        var root = NewUI("StartPanel", out var rootRt);
        Stretch(rootRt);
        var cg = root.AddComponent<CanvasGroup>();
        root.AddComponent<YOTOUIShow>();

        // ---------- 背景:bg(art-kit,全屏铺图)----------
        var bg = (GameObject)PrefabUtility.InstantiatePrefab(_bgPrefab, root.transform);
        bg.name = "bg";
        var bgRt = (RectTransform)bg.transform;
        bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.anchoredPosition = Vector2.zero;
        bgRt.sizeDelta = new Vector2(5504.974f, 3669.982f);

        // ---------- 中上 logo 图 ----------
        var logoGo = NewUI("Image", out var logoRt, root.transform);
        logoRt.anchorMin = logoRt.anchorMax = logoRt.pivot = new Vector2(0.5f, 0.5f);
        logoRt.anchoredPosition = new Vector2(0, 660);
        logoRt.sizeDelta = new Vector2(1815, 866);
        var logoImg = logoGo.AddComponent<Image>();
        logoImg.sprite = LoadSprite(LogoSpritePath);
        logoImg.raycastTarget = false;

        // ---------- 左上:设置(方形按钮 + setting 图标)----------
        var btnSettingGo = NewUI("Btn_Setting", out var settingRt, root.transform);
        settingRt.SetSiblingIndex(2); // bg / logo 之后
        TopLeft(settingRt, new Vector2(60, -80), new Vector2(180, 180));
        var btnSetting = btnSettingGo.AddComponent<Button>();
        var settingBgGo = NewUI("Bg", out var settingBgRt, btnSettingGo.transform);
        Stretch(settingBgRt);
        var settingImg = settingBgGo.AddComponent<Image>();
        settingImg.sprite = LoadSprite(SettingIconPath);
        btnSetting.targetGraphic = settingImg;

        // ---------- 右上:体力广告「+」按钮(Button_Plus,art-kit,贴右上角)----------
        var plusGo = (GameObject)PrefabUtility.InstantiatePrefab(_plusPrefab, root.transform);
        plusGo.name = "Button_Plus";
        var plusRt = (RectTransform)plusGo.transform;
        plusRt.anchorMin = plusRt.anchorMax = new Vector2(1, 1);
        plusRt.pivot = new Vector2(0.5f, 0.5f);
        plusRt.anchoredPosition = new Vector2(-125, -130);
        plusRt.sizeDelta = new Vector2(79.415f, 81.12f);
        var btnEnergyAd = plusGo.GetComponent<Button>();
        if (btnEnergyAd == null) btnEnergyAd = plusGo.AddComponent<Button>(); // 源预制体根无 Button,实例上补一个

        // ---------- 右上:体力胶囊(图标 + 数值,图标动态加载)----------
        var energyText = BuildPill(root.transform, "Energy", "50",
            new Vector2(-176, -80), new Vector2(300, 100), CurrencyType.Energy);

        // ---------- 右上:金币胶囊(体力下一行)----------
        var goldText = BuildPill(root.transform, "Gold", "0",
            new Vector2(-60, -196), new Vector2(360, 100), CurrencyType.Gold);

        // ---------- 底部左竖排:任务(上)/ 商店(下)----------
        var leftCol = NewUI("LeftColumn", out var leftRt, root.transform);
        leftRt.anchorMin = leftRt.anchorMax = new Vector2(0, 0);
        leftRt.pivot = new Vector2(0, 0);
        leftRt.anchoredPosition = new Vector2(300, 150);
        leftRt.sizeDelta = new Vector2(220, 476);
        AddVLG(leftCol, 36);
        var btnTask = BuildYellowButton("Btn_Task", "任务", leftCol.transform, null);
        var btnShop = BuildYellowButton("Btn_Shop", "商店", leftCol.transform, LoadSprite(ShopIconPath), Color.black);

        // ---------- 底部中间:准备(CommonButton 主按钮)----------
        var prepareGo = (GameObject)PrefabUtility.InstantiatePrefab(_commonButton, root.transform);
        prepareGo.name = "Btn_Prepare";
        var prepareRt = (RectTransform)prepareGo.transform;
        prepareRt.anchorMin = prepareRt.anchorMax = new Vector2(0.5f, 0);
        prepareRt.pivot = new Vector2(0.5f, 0);
        prepareRt.anchoredPosition = new Vector2(0, 177);
        prepareRt.sizeDelta = new Vector2(202.4951f, 115.8548f);
        var btnPrepare = prepareGo.GetComponent<Button>();
        SetButtonText(prepareGo, "准备");

        // ---------- 底部右竖排:图鉴(上)/ 排行榜(下)----------
        var rightCol = NewUI("RightColumn", out var rightRt, root.transform);
        rightRt.anchorMin = rightRt.anchorMax = new Vector2(1, 0);
        rightRt.pivot = new Vector2(1, 0);
        rightRt.anchoredPosition = new Vector2(-300, 150);
        rightRt.sizeDelta = new Vector2(220, 476);
        AddVLG(rightCol, 36);
        var btnCodex = BuildYellowButton("Btn_Codex", "图鉴", rightCol.transform, LoadSprite(CodexIconPath));
        var btnLeaderboard = BuildYellowButton("Btn_Leaderboard", "排行榜", rightCol.transform, LoadSprite(LeaderboardIconPath));

        // ---------- 接脚本字段 ----------
        var panel = root.AddComponent<StartPanel>();
        panel.canvasGroup = cg;
        panel.uiType = UIEnum.StartPanel;
        panel.energyText = energyText;
        panel.goldText = goldText;
        panel.btn_setting = btnSetting;
        panel.btn_energyAd = btnEnergyAd;
        panel.btn_shop = btnShop;
        panel.btn_task = btnTask;
        panel.btn_prepare = btnPrepare;
        panel.btn_codex = btnCodex;
        panel.btn_leaderboard = btnLeaderboard;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[StartPanelBuilder] Built {PrefabPath}");
    }

    // ============================ 工具 ============================

    /// <summary>实例化 CommonButtonYellow,设置名称/文本/(可选)图标 sprite 与颜色,返回其 Button。尺寸由父级 VLG 控制。</summary>
    private static Button BuildYellowButton(string name, string label, Transform parent, Sprite icon, Color? iconColor = null)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(_commonButtonYellow, parent);
        go.name = name;
        go.SetActive(true);

        SetButtonText(go, label);

        if (icon != null)
        {
            // CommonButtonYellow 的 "Image" 子物体承载图标 sprite
            var imageTr = go.transform.Find("Image");
            var img = imageTr != null ? imageTr.GetComponent<Image>() : null;
            if (img != null)
            {
                img.sprite = icon;
                if (iconColor.HasValue) img.color = iconColor.Value;
            }
        }

        return go.GetComponent<Button>();
    }

    /// <summary>把按钮内的 TMP 文本设为 label(CommonButton / CommonButtonYellow 内含 Text(TMP))。</summary>
    private static void SetButtonText(GameObject buttonGo, string label)
    {
        var text = buttonGo.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null) text.text = label;
    }

    private static void AddVLG(GameObject go, float spacing)
    {
        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = spacing;
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = vlg.childForceExpandHeight = true;
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
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>锚定到左上角:anchoredPosition 以左上为原点(向右为 +x,向下为 -y)。</summary>
    private static void TopLeft(RectTransform rt, Vector2 anchoredPos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
    }

    /// <summary>锚定到右上角:anchoredPosition 以右上为原点(向左为 -x,向下为 -y)。</summary>
    private static void TopRight(RectTransform rt, Vector2 anchoredPos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
    }

    /// <summary>
    /// 右上角资源「胶囊」:深色圆角底 + 左侧图标 + 右对齐文本,返回文本(数值由 StartPanel 运行时刷新)。
    /// 图标不烤进预制体——Icon 上挂 <see cref="CurrencyIconBinder"/>,运行时按币种从 Resources/UI/Icons 动态加载。
    /// </summary>
    private static TextMeshProUGUI BuildPill(Transform parent, string name, string text, Vector2 anchoredPos, Vector2 size, CurrencyType? iconType = null)
    {
        var go = NewUI(name, out var rt, parent);
        TopRight(rt, anchoredPos, size);
        var bg = go.AddComponent<Image>();
        bg.sprite = BuiltinSprite("UI/Skin/UISprite.psd");
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.25f, 0.27f, 0.33f, 1f);

        if (iconType.HasValue)
        {
            // 左侧图标(竖直居中);图标动态加载——挂 CurrencyIconBinder,运行时贴图。
            const float iconSize = 72f, iconPad = 16f;
            var iconGo = NewUI("Icon", out var iconRt, go.transform);
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0, 0.5f);
            iconRt.pivot = new Vector2(0, 0.5f);
            iconRt.anchoredPosition = new Vector2(iconPad, 0f);
            iconRt.sizeDelta = new Vector2(iconSize, iconSize);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            iconGo.AddComponent<CurrencyIconBinder>().type = iconType.Value;
        }

        // 文本:四边拉伸,左让开图标(anchoredPosition.x=36 / sizeDelta.x=-128,与预制体一致),右对齐不换行。
        var textGo = NewUI("Text", out var textRt, go.transform);
        textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one;
        textRt.pivot = new Vector2(0.5f, 0.5f);
        textRt.anchoredPosition = new Vector2(36, 0);
        textRt.sizeDelta = new Vector2(-128, 0);
        var tmp = NewText(textGo, text, 96, TextAlignmentOptions.Right);
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
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

    /// <summary>按路径加载美术 Sprite(返回首个 Sprite 子资源,失败返回 null,Image 退化为白块)。</summary>
    private static Sprite LoadSprite(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s != null) return s;
        // 多 Sprite 图集情形:取第一个 Sprite 子资源
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
            if (obj is Sprite sp) return sp;
        return null;
    }

    /// <summary>加载 Unity 内置 UI 精灵(UISprite 等),失败返回 null(Image 退化为纯色块)。</summary>
    private static Sprite BuiltinSprite(string path)
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>(path);
    }
}
#endif
