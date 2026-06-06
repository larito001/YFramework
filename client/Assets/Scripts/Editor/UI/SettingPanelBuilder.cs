#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成设置界面预制体 SettingPanel.prefab(覆盖旧的)到 Resources/UI/Setting 下。
/// 外壳(从美术返工后的预制体反推):
///   根:全屏黑遮罩 Image + CanvasGroup + YOTOUIShow + SettingPanel
///     ├ bg                            (Resources/UI/bg)      全屏背景图(art-kit 实例)
///     ├ bg (1)                        程序化半透遮罩(压暗背景图,衬出窗口)
///     ├ backBtn                       (Resources/UI/backBtn) 返回(art-kit 实例,左上,接 SettingPanel.backBtn)
///     ├ Popup_Box_02_DecoLine_Basic   (Art/.../Popup_Box_02_DecoLine_Basic)窗口装饰边框(art-kit 实例)
///     └ Window                        逻辑容器(无 Image,底板由 Popup_Box 提供)
///         ├ Title      标题「设置」
///         ├ SoundTab   声音页签(控件烤进预制体;默认关闭,OnShow 时激活)
///         └ LogoutBtn  退出登录(程序化按钮 + 美术九宫格底图 Bg/InnerBorder1)
///
/// 声音控件(音乐/音效 滑条 + 数值 + 静音开关)在此构建并接到 <see cref="SoundSettingsTab"/> 的序列化数组,
/// 运行时不再 new GameObject;<see cref="SoundSettingsTab"/> 激活时只刷新/绑定 SoundMgr。
/// 返回按钮改用 art-kit backBtn.prefab(直接挂根下),不再是 Window 内的程序化按钮。
///
/// 菜单:Tools/UI/Build SettingPanel Prefab
/// </summary>
public static class SettingPanelBuilder
{
    private const string Dir = "Assets/Resources/UI/Setting";
    private const string PrefabPath = Dir + "/SettingPanel.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";

    // art-kit 预制体
    private const string BgPrefabPath = "Assets/Resources/UI/bg.prefab";
    private const string BackBtnPrefabPath = "Assets/Resources/UI/backBtn.prefab";
    private const string DecoBoxPrefabPath =
        "Assets/Art/UI/NewUI/Theme_Blue/Prefabs/Prefabs_Popup/Popup_Box_02_DecoLine_Basic.prefab";

    // 退出登录按钮用的美术九宫格底图(沿用预制体里手调的引用)
    private const string BtnBgSpritePath = "Assets/Art/UI/NewUI/Shared/Sprite_Common/Button/Button_01_White_Bg.Png";
    private const string BtnInnerBorderSpritePath = "Assets/Art/UI/NewUI/Shared/Sprite_Common/Button/Button_01_White_InnerBorder1.Png";

    // 全屏背景图 / 半透遮罩覆盖整屏的尺寸,沿用美术给的手调值。
    private static readonly Vector2 BgSize = new Vector2(5504.974f, 3669.982f);
    // 窗口(及装饰边框)尺寸,沿用美术手调值。
    private static readonly Vector2 WindowSize = new Vector2(1451.1f, 880.86f);
    private static readonly Vector2 DecoBoxSize = new Vector2(1431.0187f, 880.8599f);

    private static TMP_FontAsset _font;
    private static GameObject _bgPrefab, _backBtnPrefab, _decoBoxPrefab;
    private static Sprite _btnBgSprite, _btnInnerBorderSprite;

    [MenuItem("Tools/UI/Build SettingPanel Prefab")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        _bgPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BgPrefabPath);
        _backBtnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BackBtnPrefabPath);
        _decoBoxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DecoBoxPrefabPath);
        _btnBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BtnBgSpritePath);
        _btnInnerBorderSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BtnInnerBorderSpritePath);

        // ---------- 根(全屏遮罩 + CanvasGroup + YOTOUIShow + SettingPanel)----------
        var root = NewUI("SettingPanel", out var rootRt);
        Stretch(rootRt);
        root.AddComponent<Image>().color = UITheme.Scrim;
        var cg = root.AddComponent<CanvasGroup>();
        root.AddComponent<YOTOUIShow>();

        // ---------- 背景图(bg.prefab 实例,最底)----------
        if (_bgPrefab != null)
        {
            var bg = (GameObject)PrefabUtility.InstantiatePrefab(_bgPrefab, root.transform);
            bg.name = "bg";
            var bgRt = (RectTransform)bg.transform;
            bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = new Vector2(0.5f, 0.5f);
            bgRt.anchoredPosition = Vector2.zero;
            bgRt.sizeDelta = BgSize;
        }
        else
        {
            Debug.LogWarning($"[SettingPanelBuilder] 找不到背景预制体 {BgPrefabPath},跳过背景图。");
        }

        // ---------- 半透遮罩(压暗背景图,衬出窗口)----------
        var overlay = NewUI("bg (1)", out var ovRt, root.transform);
        ovRt.anchorMin = ovRt.anchorMax = ovRt.pivot = new Vector2(0.5f, 0.5f);
        ovRt.anchoredPosition = Vector2.zero;
        ovRt.sizeDelta = BgSize;
        overlay.AddComponent<Image>().color = UITheme.Scrim;

        // ---------- 返回(art-kit backBtn.prefab,左上)----------
        Button backBtn = null;
        if (_backBtnPrefab != null)
        {
            var backGo = (GameObject)PrefabUtility.InstantiatePrefab(_backBtnPrefab, root.transform);
            backGo.name = "backBtn";
            var backRt = (RectTransform)backGo.transform;
            backRt.anchorMin = backRt.anchorMax = new Vector2(0, 1);
            backRt.pivot = new Vector2(0.5f, 0.5f);
            backRt.anchoredPosition = new Vector2(155.2f, -136f);
            backRt.sizeDelta = new Vector2(243.246f, 201.326f);
            backBtn = backGo.GetComponent<Button>();
        }
        else
        {
            Debug.LogWarning($"[SettingPanelBuilder] 找不到返回按钮预制体 {BackBtnPrefabPath},返回按钮缺失。");
        }

        // ---------- 窗口装饰边框(art-kit Popup_Box,在 Window 之下当底板)----------
        if (_decoBoxPrefab != null)
        {
            var deco = (GameObject)PrefabUtility.InstantiatePrefab(_decoBoxPrefab, root.transform);
            deco.name = "Popup_Box_02_DecoLine_Basic";
            var decoRt = (RectTransform)deco.transform;
            decoRt.anchorMin = decoRt.anchorMax = decoRt.pivot = new Vector2(0.5f, 0.5f);
            decoRt.anchoredPosition = Vector2.zero;
            decoRt.sizeDelta = DecoBoxSize;
        }
        else
        {
            Debug.LogWarning($"[SettingPanelBuilder] 找不到窗口装饰预制体 {DecoBoxPrefabPath},窗口无底板。");
        }

        // ---------- 逻辑容器 Window(无 Image,底板交给 Popup_Box)----------
        var window = NewUI("Window", out var winRt, root.transform);
        winRt.anchorMin = winRt.anchorMax = winRt.pivot = new Vector2(0.5f, 0.5f);
        winRt.anchoredPosition = Vector2.zero;
        winRt.sizeDelta = WindowSize;

        // ---------- 标题 ----------
        var titleGo = NewUI("Title", out var titleRt, window.transform);
        titleRt.anchorMin = new Vector2(0, 1); titleRt.anchorMax = new Vector2(1, 1); titleRt.pivot = new Vector2(0.5f, 1);
        titleRt.anchoredPosition = new Vector2(0, -30); titleRt.sizeDelta = new Vector2(-60, 110);
        NewText(titleGo, "设置", 60, TextAlignmentOptions.Center);

        // ---------- 声音页签(控件烤进预制体)----------
        var soundTab = NewUI("SoundTab", out var soundRt, window.transform);
        soundRt.anchorMin = new Vector2(0, 0); soundRt.anchorMax = new Vector2(1, 1); soundRt.pivot = new Vector2(0.5f, 0.5f);
        soundRt.anchoredPosition = new Vector2(0, 90);
        soundRt.sizeDelta = new Vector2(-80, -500); // 左右各留 40,上下留标题与按钮
        var tab = soundTab.AddComponent<SoundSettingsTab>();
        BuildSoundControls(soundTab, tab);
        // 默认关闭:由 SettingPanel.OnShow 的 soundTab.SetActive(true) 触发 OnEnable→刷新/绑定。
        soundTab.SetActive(false);

        // ---------- 退出登录(底部居中,美术九宫格底图 + 内描边)----------
        var logoutBtn = BuildLogoutButton("LogoutBtn", "退出登录", window.transform,
            new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(442.505f, 177.002f));

        // ---------- 接脚本字段 ----------
        var panel = root.AddComponent<SettingPanel>();
        panel.canvasGroup = cg;
        panel.uiType = UIEnum.SettingPanel;
        panel.backBtn = backBtn;
        panel.logoutBtn = logoutBtn;
        panel.soundTab = soundTab;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[SettingPanelBuilder] Built {PrefabPath}");
    }

    // ============================ 声音控件(烤进预制体) ============================

    /// <summary>
    /// 在 SoundTab 上构建:ScrollRect(Viewport+Content 纵向布局)+「音乐 / 音效」两行,
    /// 每行 = 名称标签 + 滑条 + 百分比文本 + "静音" + 开关。控件引用写回 <paramref name="tab"/> 的序列化数组。
    /// </summary>
    private static void BuildSoundControls(GameObject tabGo, SoundSettingsTab tab)
    {
        // 容器做成 ScrollRect:内容超出可视区时纵向滚动,不向下溢出压到按钮。
        var scroll = tabGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        var viewport = NewUI("Viewport", out var vpRt, tabGo.transform);
        Stretch(vpRt);
        viewport.AddComponent<RectMask2D>();
        viewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); // 透明底,空白处也能接拖拽

        var content = NewUI("Content", out var contentRt, viewport.transform);
        contentRt.anchorMin = new Vector2(0, 1); contentRt.anchorMax = new Vector2(1, 1); contentRt.pivot = new Vector2(0.5f, 1);
        contentRt.anchoredPosition = Vector2.zero; contentRt.sizeDelta = Vector2.zero;
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 24;
        vlg.padding = new RectOffset(40, 40, 32, 32);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = vpRt;
        scroll.content = contentRt;

        string[] names = { "音乐", "音效" };
        tab.sliders = new Slider[names.Length];
        tab.values = new TextMeshProUGUI[names.Length];
        tab.mutes = new Toggle[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            var row = NewRow(content.transform, 100);
            NewLabel(row.transform, names[i], 220, 40, TextAlignmentOptions.Left);
            tab.sliders[i] = NewSlider(row.transform);
            tab.values[i] = NewLabel(row.transform, "100%", 190, 36, TextAlignmentOptions.Right);
            NewLabel(row.transform, "静音", 170, 34, TextAlignmentOptions.Right);
            tab.mutes[i] = NewToggle(row.transform);
        }
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
        tmp.text = text;
        tmp.fontSize = UITheme.Font(size);
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        var font = _font != null ? _font : TMP_Settings.defaultFontAsset;
        if (font != null) tmp.font = font;
        return tmp;
    }

    /// <summary>带 LayoutElement 宽度约束的文本(用于行内标签/数值)。
    /// 单词不换行:这些是「音乐/100%/静音」等单标签,字号经 UITheme.Font 放大(×2)后若窄于文本会被 TMP 竖向折行,故强制单行。</summary>
    private static TextMeshProUGUI NewLabel(Transform parent, string text, float width, float size, TextAlignmentOptions align)
    {
        var go = NewUI("Text", out _, parent);
        var tmp = NewText(go, text, size, align);
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = width;
        le.preferredWidth = width;
        return tmp;
    }

    /// <summary>一行(水平布局),高度固定。</summary>
    private static GameObject NewRow(Transform parent, float height)
    {
        var go = NewUI("Row", out _, parent);
        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12;
        hlg.padding = new RectOffset(8, 8, 0, 0);
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;
        return go;
    }

    private static Image NewImage(Transform parent, string name, Color color)
    {
        var go = NewUI(name, out _, parent);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static Slider NewSlider(Transform parent)
    {
        var go = NewUI("Slider", out _, parent);
        var le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1; le.minWidth = 360; le.preferredHeight = 52;
        var slider = go.AddComponent<Slider>();

        var bg = NewImage(go.transform, "Background", new Color(0.3f, 0.3f, 0.35f, 1f));
        var bgRt = bg.rectTransform; bgRt.anchorMin = new Vector2(0, 0.3f); bgRt.anchorMax = new Vector2(1, 0.7f); bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;

        var fillArea = NewUI("Fill Area", out var faRt, go.transform);
        faRt.anchorMin = new Vector2(0, 0.25f); faRt.anchorMax = new Vector2(1, 0.75f); faRt.offsetMin = new Vector2(10, 0); faRt.offsetMax = new Vector2(-10, 0);
        var fill = NewImage(fillArea.transform, "Fill", new Color(0.4f, 0.6f, 0.9f, 1f));
        var fillRt = fill.rectTransform; fillRt.anchorMin = new Vector2(0, 0); fillRt.anchorMax = new Vector2(0, 1); fillRt.sizeDelta = new Vector2(18, 0);

        var handleArea = NewUI("Handle Slide Area", out var haRt, go.transform);
        haRt.anchorMin = Vector2.zero; haRt.anchorMax = Vector2.one; haRt.offsetMin = new Vector2(10, 0); haRt.offsetMax = new Vector2(-10, 0);
        var handle = NewImage(handleArea.transform, "Handle", Color.white);
        var handleRt = handle.rectTransform; handleRt.sizeDelta = new Vector2(40, 0);

        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0; slider.maxValue = 1; slider.value = 1;
        return slider;
    }

    private static Toggle NewToggle(Transform parent)
    {
        var go = NewUI("Toggle", out _, parent);
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = 64; le.preferredWidth = 64; le.preferredHeight = 64;
        var toggle = go.AddComponent<Toggle>();

        var bg = NewImage(go.transform, "Background", new Color(0.25f, 0.27f, 0.32f, 1f));
        var bgRt = bg.rectTransform; bgRt.anchorMin = new Vector2(0, 0.5f); bgRt.anchorMax = new Vector2(0, 0.5f); bgRt.pivot = new Vector2(0, 0.5f);
        bgRt.sizeDelta = new Vector2(56, 56); bgRt.anchoredPosition = Vector2.zero;
        var check = NewImage(bg.transform, "Checkmark", new Color(0.4f, 0.85f, 0.45f, 1f));
        var cRt = check.rectTransform; cRt.anchorMin = new Vector2(0.5f, 0.5f); cRt.anchorMax = new Vector2(0.5f, 0.5f); cRt.sizeDelta = new Vector2(36, 36); cRt.anchoredPosition = Vector2.zero;

        toggle.targetGraphic = bg;
        toggle.graphic = check;
        toggle.isOn = false;
        return toggle;
    }

    /// <summary>
    /// 退出登录按钮:底部居中,美术九宫格红底图(Bg + InnerBorder1 内描边)+ 居中文字。
    /// Button 不设 targetGraphic(与预制体一致),点击区域由底图 raycast 提供。
    /// </summary>
    private static Button BuildLogoutButton(string name, string label, Transform parent,
        Vector2 anchor, Vector2 anchoredPos, Vector2 size)
    {
        var go = NewUI(name, out var rt, parent);
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = null; // 与美术返工后的预制体一致(无 target graphic)

        // 底图(铺满,九宫格红底)
        var bg = NewImage(go.transform, "Bg (1)", new Color(0.9568628f, 0.21960786f, 0.33333334f, 1f));
        Stretch(bg.rectTransform);
        if (_btnBgSprite != null) { bg.sprite = _btnBgSprite; bg.type = Image.Type.Sliced; }

        // 内描边(略微内缩的九宫格亮色描边)
        var inner = NewImage(go.transform, "InnerBorder1", new Color(1f, 0.427451f, 0.45098042f, 1f));
        var innerRt = inner.rectTransform;
        innerRt.anchorMin = Vector2.zero; innerRt.anchorMax = Vector2.one;
        innerRt.anchoredPosition = new Vector2(-0.20501709f, 1.6899948f);
        innerRt.sizeDelta = new Vector2(-5.885f, -12.34f);
        if (_btnInnerBorderSprite != null) { inner.sprite = _btnInnerBorderSprite; inner.type = Image.Type.Sliced; }

        // 文字
        var lbl = NewUI("Label", out var lblRt, go.transform);
        Stretch(lblRt);
        NewText(lbl, label, 40, TextAlignmentOptions.Center);
        return btn;
    }
}
#endif
