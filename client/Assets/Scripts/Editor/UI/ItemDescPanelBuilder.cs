#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成通用道具描述弹窗预制体 ItemDescPanel.prefab 到 Resources/UI/Common 下。
/// 结构:全屏暗底(点它关闭)+ 居中窗口 + 道具图标 + 名称 + 详细描述(自动换行)+ 关闭按钮。
/// 字段在此接好;运行时由 <see cref="ItemDescPanel"/> 按 ItemDescParam 填充。
///
/// 菜单:Tools/UI/Build ItemDescPanel Prefab
/// </summary>
public static class ItemDescPanelBuilder
{
    private const string Dir = "Assets/Resources/UI/Common";
    private const string PrefabPath = Dir + "/ItemDescPanel.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";

    private static TMP_FontAsset _font;

    [MenuItem("Tools/UI/Build ItemDescPanel Prefab")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        // 根:全屏暗底,挡住下层点击;整块是个 Button,点窗口外暗底即关闭。
        var root = NewUI("ItemDescPanel", out var rootRt);
        Stretch(rootRt);
        var rootImg = root.AddComponent<Image>();
        rootImg.color = new Color(0f, 0f, 0f, 0.6f);
        rootImg.raycastTarget = true;
        var bgBtn = root.AddComponent<Button>();
        bgBtn.targetGraphic = rootImg;
        bgBtn.transition = Selectable.Transition.None;
        var cg = root.AddComponent<CanvasGroup>();
        root.AddComponent<YOTOUIShow>();

        // 窗口(居中);窗口自带 Image 拦截点击,所以点窗口内部不会触发暗底关闭。
        var window = NewUI("Window", out var winRt, root.transform);
        winRt.anchorMin = winRt.anchorMax = winRt.pivot = new Vector2(0.5f, 0.5f);
        winRt.sizeDelta = new Vector2(1200, 1500);
        var winImg = window.AddComponent<Image>();
        winImg.color = new Color(0.13f, 0.14f, 0.17f, 0.99f);
        winImg.raycastTarget = true; // 拦截:点窗口不关

        // 道具图标(顶部居中方形)
        var iconGo = NewUI("Icon", out var iconRt, window.transform);
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 1f); iconRt.pivot = new Vector2(0.5f, 1f);
        iconRt.anchoredPosition = new Vector2(0, -60); iconRt.sizeDelta = new Vector2(420, 420);
        var iconImg = iconGo.AddComponent<Image>();
        iconImg.raycastTarget = false; iconImg.preserveAspect = true;

        // 名称(图标下方)
        var nameGo = NewUI("Name", out var nameRt, window.transform);
        nameRt.anchorMin = new Vector2(0, 1); nameRt.anchorMax = new Vector2(1, 1); nameRt.pivot = new Vector2(0.5f, 1);
        nameRt.anchoredPosition = new Vector2(0, -520); nameRt.sizeDelta = new Vector2(-80, 120);
        var nameText = NewText(nameGo, "道具名称", 56, TextAlignmentOptions.Center);

        // 详细描述(名称下方到关闭按钮上方,自动换行,顶部对齐)
        var descGo = NewUI("Desc", out var descRt, window.transform);
        descRt.anchorMin = new Vector2(0, 0); descRt.anchorMax = new Vector2(1, 1);
        descRt.offsetMin = new Vector2(70, 230); descRt.offsetMax = new Vector2(-70, -680);
        var descText = NewText(descGo, "", 38, TextAlignmentOptions.Top);
        descText.enableWordWrapping = true;
        descText.overflowMode = TextOverflowModes.Overflow;

        // 关闭按钮(底部居中)
        var closeBtn = BuildButton("CloseBtn", "关闭", window.transform,
            new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(560, 150), new Color(0.3f, 0.32f, 0.4f, 1f), out _);

        // 接脚本字段
        var panel = root.AddComponent<ItemDescPanel>();
        panel.canvasGroup = cg;
        panel.uiType = UIEnum.ItemDescPanel;
        panel.iconImage = iconImg;
        panel.nameText = nameText;
        panel.descText = descText;
        panel.closeBtn = closeBtn;
        panel.backgroundBtn = bgBtn;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ItemDescPanelBuilder] Built {PrefabPath}");
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

    private static Button BuildButton(string name, string label, Transform parent,
        Vector2 anchor, Vector2 anchoredPos, Vector2 size, Color color, out TextMeshProUGUI labelText)
    {
        var go = NewUI(name, out var rt, parent);
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = color;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var lblGo = NewUI("Label", out var lblRt, go.transform);
        Stretch(lblRt);
        labelText = NewText(lblGo, label, 34, TextAlignmentOptions.Center);
        return btn;
    }
}
#endif
