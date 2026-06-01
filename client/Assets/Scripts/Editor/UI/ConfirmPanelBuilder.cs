#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成通用确认弹窗预制体 ConfirmPanel.prefab 到 Resources/UI/Common 下。
/// 结构:全屏遮罩 + 居中窗口 + 标题 + 正文 + 取消/确认两个按钮。脚本字段在此接好。
///
/// 菜单:Tools/UI/Build ConfirmPanel Prefab
/// </summary>
public static class ConfirmPanelBuilder
{
    private const string Dir = "Assets/Resources/UI/Common";
    private const string PrefabPath = Dir + "/ConfirmPanel.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";

    private static TMP_FontAsset _font;

    [MenuItem("Tools/UI/Build ConfirmPanel Prefab")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        // 根:全屏遮罩(挡住下层点击)
        var root = NewUI("ConfirmPanel", out var rootRt);
        Stretch(rootRt);
        root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        var cg = root.AddComponent<CanvasGroup>();
        root.AddComponent<YOTOUIShow>();

        // 窗口(竖屏适配:画布宽恒 1920 单位,窗口取较宽较高的尺寸,在手机竖屏上才不至于又小又扁)
        var window = NewUI("Window", out var winRt, root.transform);
        winRt.anchorMin = winRt.anchorMax = winRt.pivot = new Vector2(0.5f, 0.5f);
        winRt.sizeDelta = new Vector2(1120, 760);
        window.AddComponent<Image>().color = new Color(0.13f, 0.14f, 0.17f, 0.99f);

        // 标题(顶部,带高 120 容下 40pt×2)
        var titleGo = NewUI("Title", out var titleRt, window.transform);
        titleRt.anchorMin = new Vector2(0, 1); titleRt.anchorMax = new Vector2(1, 1); titleRt.pivot = new Vector2(0.5f, 1);
        titleRt.anchoredPosition = new Vector2(0, -30); titleRt.sizeDelta = new Vector2(-60, 120);
        var titleText = NewText(titleGo, "提示", 40, TextAlignmentOptions.Center);

        // 正文(居中,自动换行;上让出标题、下让出按钮)
        var msgGo = NewUI("Message", out var msgRt, window.transform);
        msgRt.anchorMin = new Vector2(0, 0); msgRt.anchorMax = new Vector2(1, 1);
        msgRt.offsetMin = new Vector2(50, 230); msgRt.offsetMax = new Vector2(-50, -160);
        var msgText = NewText(msgGo, "", 32, TextAlignmentOptions.Center);
        msgText.enableWordWrapping = true;

        // 取消 / 确认(底部并排,加大触控面积)
        var cancelBtn = BuildButton("CancelBtn", "取消", window.transform,
            new Vector2(0.5f, 0), new Vector2(-290, 50), new Vector2(480, 150), new Color(0.3f, 0.32f, 0.4f, 1f), out var cancelLabel);
        var confirmBtn = BuildButton("ConfirmBtn", "确认", window.transform,
            new Vector2(0.5f, 0), new Vector2(290, 50), new Vector2(480, 150), new Color(0.55f, 0.28f, 0.28f, 1f), out var confirmLabel);

        // 接脚本字段
        var panel = root.AddComponent<ConfirmPanel>();
        panel.canvasGroup = cg;
        panel.uiType = UIEnum.ConfirmPanel;
        panel.titleText = titleText;
        panel.messageText = msgText;
        panel.confirmBtn = confirmBtn;
        panel.cancelBtn = cancelBtn;
        panel.confirmLabel = confirmLabel;
        panel.cancelLabel = cancelLabel;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ConfirmPanelBuilder] Built {PrefabPath}");
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
