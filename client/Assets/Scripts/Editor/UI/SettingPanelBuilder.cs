#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成设置界面预制体 SettingPanel.prefab(覆盖旧的)到 Resources/UI/Setting 下。
/// 竖屏单页:全屏遮罩 + 居中竖向窗口 + 标题「设置」+ 声音页签容器(<see cref="SoundSettingsTab"/>,运行时构建控件)+ 返回。
/// 只保留声音设置;按键 / 画面页签已移除。尺寸按"画布宽恒为 1920 单位"(CanvasScaler match=width)给。
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

        // ---------- 声音页签容器(填内容区)----------
        var soundTab = NewUI("SoundTab", out var soundRt, window.transform);
        soundRt.anchorMin = new Vector2(0, 0); soundRt.anchorMax = new Vector2(1, 1);
        soundRt.offsetMin = new Vector2(40, 190);  // 下留返回按钮
        soundRt.offsetMax = new Vector2(-40, -160); // 上留标题
        var tab = soundTab.AddComponent<SoundSettingsTab>();
        tab.font = _font;

        // ---------- 返回 ----------
        var backBtn = BuildButton("BackBtn", "返回", window.transform,
            new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(360, 120), new Color(0.3f, 0.32f, 0.4f, 1f), 44);

        // ---------- 接脚本字段 ----------
        var panel = root.AddComponent<SettingPanel>();
        panel.canvasGroup = cg;
        panel.uiType = UIEnum.SettingPanel;
        panel.backBtn = backBtn;
        panel.soundTab = soundTab;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[SettingPanelBuilder] Built {PrefabPath}");
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
