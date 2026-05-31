#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成设置界面预制体 SettingPanel.prefab(覆盖旧的)到 Resources/UI/Setting 下。
/// 生成骨架:全屏遮罩 + 居中窗口 + 标题 + 三个页签按钮(声音/按键/画面)+ 三个页签容器(各挂对应 Tab 组件)+ 返回。
/// 页签内部控件由各 <see cref="SettingTabBase"/> 子类在运行时构建,这里只接好骨架字段与字体。
///
/// 菜单:Tools/UI/Build SettingPanel Prefab
/// </summary>
public static class SettingPanelBuilder
{
    private const string Dir = "Assets/Resources/UI/Setting";
    private const string PrefabPath = Dir + "/SettingPanel.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";

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

        // ---------- 窗口 ----------
        var window = NewUI("Window", out var winRt, root.transform);
        winRt.anchorMin = winRt.anchorMax = winRt.pivot = new Vector2(0.5f, 0.5f);
        winRt.sizeDelta = new Vector2(1120, 820);
        window.AddComponent<Image>().color = new Color(0.12f, 0.13f, 0.16f, 0.98f);

        // ---------- 标题 ----------
        var titleGo = NewUI("Title", out var titleRt, window.transform);
        titleRt.anchorMin = new Vector2(0, 1); titleRt.anchorMax = new Vector2(1, 1); titleRt.pivot = new Vector2(0.5f, 1);
        titleRt.anchoredPosition = new Vector2(0, -16); titleRt.sizeDelta = new Vector2(-40, 56);
        NewText(titleGo, "设置", 38, TextAlignmentOptions.Center);

        // ---------- 页签按钮 ----------
        var soundBtn = BuildTabButton("TabSound", "声音", window.transform, -280);
        var keyBtn = BuildTabButton("TabKey", "按键", window.transform, 0);
        var gfxBtn = BuildTabButton("TabGraphics", "画面", window.transform, 280);

        // ---------- 页签容器(填内容区,默认隐藏)----------
        var soundTab = BuildTabContainer<SoundSettingsTab>("SoundTab", window.transform);
        var keyTab = BuildTabContainer<KeybindingTab>("KeyTab", window.transform);
        var gfxTab = BuildTabContainer<GraphicsSettingsTab>("GraphicsTab", window.transform);

        // ---------- 返回 ----------
        var backBtn = BuildButton("BackBtn", "返回", window.transform,
            new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(220, 60), new Color(0.3f, 0.32f, 0.4f, 1f));

        // ---------- 接脚本字段 ----------
        var panel = root.AddComponent<SettingPanel>();
        panel.canvasGroup = cg;
        panel.uiType = UIEnum.SettingPanel;
        panel.backBtn = backBtn;
        panel.tabSoundBtn = soundBtn;
        panel.tabKeyBtn = keyBtn;
        panel.tabGraphicsBtn = gfxBtn;
        panel.soundTab = soundTab;
        panel.keyTab = keyTab;
        panel.graphicsTab = gfxTab;

        soundTab.SetActive(false);
        keyTab.SetActive(false);
        gfxTab.SetActive(false);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[SettingPanelBuilder] Built {PrefabPath}");
    }

    // ============================ 构件 ============================

    private static Button BuildTabButton(string name, string label, Transform parent, float x)
    {
        var go = NewUI(name, out var rt, parent);
        rt.anchorMin = new Vector2(0.5f, 1); rt.anchorMax = new Vector2(0.5f, 1); rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(x, -86); rt.sizeDelta = new Vector2(240, 56);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.27f, 0.33f, 1f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var lbl = NewUI("Label", out var lblRt, go.transform);
        Stretch(lblRt);
        NewText(lbl, label, 26, TextAlignmentOptions.Center);
        return btn;
    }

    private static GameObject BuildTabContainer<T>(string name, Transform parent) where T : SettingTabBase
    {
        var go = NewUI(name, out var rt, parent);
        // 内容区:上留 160(标题+页签),下留 110(返回),左右 30
        rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 1);
        rt.offsetMin = new Vector2(30, 110); rt.offsetMax = new Vector2(-30, -160);
        var tab = go.AddComponent<T>();
        tab.font = _font;
        return go;
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
        var lbl = NewUI("Label", out var lblRt, go.transform);
        Stretch(lblRt);
        NewText(lbl, label, 26, TextAlignmentOptions.Center);
        return btn;
    }
}
#endif
