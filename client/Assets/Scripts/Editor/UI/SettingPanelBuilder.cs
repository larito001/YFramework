#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成设置界面预制体 SettingPanel.prefab(覆盖旧的)到 Resources/UI/Setting 下。
/// 竖屏单页:背景图(bg.prefab 实例)+ 半透遮罩 + 居中竖向窗口 + 标题「设置」+ 声音页签(控件直接烤进预制体)+ 退出登录 + 返回。
/// 只保留声音设置;按键 / 画面页签已移除。尺寸按"画布宽恒为 1920 单位"(CanvasScaler match=width)给。
///
/// 声音控件(音乐/音效 滑条 + 数值 + 静音开关)在此构建并接到 <see cref="SoundSettingsTab"/> 的序列化数组,
/// 运行时不再 new GameObject;<see cref="SoundSettingsTab"/> 激活时只刷新/绑定 SoundMgr。
///
/// 菜单:Tools/UI/Build SettingPanel Prefab
/// </summary>
public static class SettingPanelBuilder
{
    private const string Dir = "Assets/Resources/UI/Setting";
    private const string PrefabPath = Dir + "/SettingPanel.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";
    private const string BgPrefabPath = "Assets/Resources/UI/bg.prefab";

    // 背景图(bg.prefab / 遮罩)覆盖整屏的尺寸,沿用美术给的手调值。
    private static readonly Vector2 BgSize = new Vector2(5504.974f, 3669.982f);

    private static TMP_FontAsset _font;

    [MenuItem("Tools/UI/Build SettingPanel Prefab")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        // ---------- 根 ----------
        var root = NewUI("SettingPanel", out var rootRt);
        Stretch(rootRt);
        root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        var cg = root.AddComponent<CanvasGroup>();
        root.AddComponent<YOTOUIShow>();

        // ---------- 背景图(bg.prefab 实例,放最底)----------
        var bgPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BgPrefabPath);
        if (bgPrefab != null)
        {
            var bg = (GameObject)PrefabUtility.InstantiatePrefab(bgPrefab);
            var bgRt = (RectTransform)bg.transform;
            bgRt.SetParent(root.transform, false);
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
        overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

        // ---------- 竖向窗口(居中)----------
        var window = NewUI("Window", out var winRt, root.transform);
        winRt.anchorMin = winRt.anchorMax = winRt.pivot = new Vector2(0.5f, 0.5f);
        winRt.sizeDelta = new Vector2(1400, 1900);
        window.AddComponent<Image>().color = new Color(0.12f, 0.13f, 0.16f, 0.98f);

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

        // ---------- 退出登录(返回上方,醒目红)----------
        var logoutBtn = BuildButton("LogoutBtn", "退出登录", window.transform,
            new Vector2(0.5f, 0), new Vector2(0, 200), new Vector2(360, 120), new Color(0.78f, 0.30f, 0.30f, 1f), 44);

        // ---------- 返回 ----------
        var backBtn = BuildButton("BackBtn", "返回", window.transform,
            new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(360, 120), new Color(0.3f, 0.32f, 0.4f, 1f), 44);

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
    /// 在 SoundTab 上构建:ScrollRect(Viewport+Content 纵向布局)+ 标题 +「音乐 / 音效」两行,
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

        NewLabel(content.transform, "声音设置", 700, 52, TextAlignmentOptions.Left);

        string[] names = { "音乐", "音效" };
        tab.sliders = new Slider[names.Length];
        tab.values = new TextMeshProUGUI[names.Length];
        tab.mutes = new Toggle[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            var row = NewRow(content.transform, 100);
            NewLabel(row.transform, names[i], 220, 40, TextAlignmentOptions.Left);
            tab.sliders[i] = NewSlider(row.transform);
            tab.values[i] = NewLabel(row.transform, "100%", 130, 36, TextAlignmentOptions.Right);
            NewLabel(row.transform, "静音", 110, 34, TextAlignmentOptions.Right);
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

    /// <summary>带 LayoutElement 宽度约束的文本(用于行内标签/数值)。</summary>
    private static TextMeshProUGUI NewLabel(Transform parent, string text, float width, float size, TextAlignmentOptions align)
    {
        var go = NewUI("Text", out _, parent);
        var tmp = NewText(go, text, size, align);
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
        return toggle;
    }

    private static Button BuildButton(string name, string label, Transform parent,
        Vector2 anchor, Vector2 anchoredPos, Vector2 size, Color color, float fontSize)
    {
        var go = NewUI(name, out var rt, parent);
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = color;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var lbl = NewUI("Label", out var lblRt, go.transform);
        Stretch(lblRt);
        NewText(lbl, label, fontSize, TextAlignmentOptions.Center);
        return btn;
    }
}
#endif
