#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成读取存档界面预制体 SaveSlotPanel.prefab 到 Resources/UI/Boot 下。
/// 结构:全屏遮罩 + 居中窗口 + 标题 + 滚动列表(ScrollRect)+ 行模板(SaveSlotRow,默认隐藏,运行时克隆)+ 返回按钮 + 空提示。
/// 脚本字段(btn_back/content/rowTemplate/emptyHint)在此接好。
///
/// 菜单:Tools/UI/Build SaveSlotPanel Prefab
/// </summary>
public static class SaveSlotPanelBuilder
{
    private const string Dir = "Assets/Resources/UI/Boot";
    private const string PrefabPath = Dir + "/SaveSlotPanel.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";

    private static TMP_FontAsset _font;

    [MenuItem("Tools/UI/Build SaveSlotPanel Prefab")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        // ---------- 根 ----------
        var root = NewUI("SaveSlotPanel", out var rootRt);
        Stretch(rootRt);
        root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        var cg = root.AddComponent<CanvasGroup>();
        root.AddComponent<YOTOUIShow>();

        // ---------- 窗口 ----------
        var window = NewUI("Window", out var winRt, root.transform);
        winRt.anchorMin = winRt.anchorMax = winRt.pivot = new Vector2(0.5f, 0.5f);
        winRt.sizeDelta = new Vector2(1000, 820);
        window.AddComponent<Image>().color = new Color(0.12f, 0.13f, 0.16f, 0.98f);

        // ---------- 标题 ----------
        var titleGo = NewUI("Title", out var titleRt, window.transform);
        titleRt.anchorMin = new Vector2(0, 1); titleRt.anchorMax = new Vector2(1, 1); titleRt.pivot = new Vector2(0.5f, 1);
        titleRt.anchoredPosition = new Vector2(0, -18); titleRt.sizeDelta = new Vector2(-40, 60);
        NewText(titleGo, "读取存档", 40, TextAlignmentOptions.Center);

        // ---------- 滚动列表 ----------
        var scroll = NewUI("ScrollView", out var scrollRt, window.transform);
        scrollRt.anchorMin = new Vector2(0, 0); scrollRt.anchorMax = new Vector2(1, 1);
        scrollRt.offsetMin = new Vector2(40, 110); scrollRt.offsetMax = new Vector2(-40, -90);
        var scrollBg = scroll.AddComponent<Image>();
        scrollBg.color = new Color(0f, 0f, 0f, 0.25f); // 半透明底,空白处也能接拖拽
        var scrollRect = scroll.AddComponent<ScrollRect>();

        var viewport = NewUI("Viewport", out var vpRt, scroll.transform);
        Stretch(vpRt);
        viewport.AddComponent<RectMask2D>();

        var content = NewUI("Content", out var contentRt, viewport.transform);
        contentRt.anchorMin = new Vector2(0, 1); contentRt.anchorMax = new Vector2(1, 1); contentRt.pivot = new Vector2(0.5f, 1);
        contentRt.anchoredPosition = Vector2.zero; contentRt.sizeDelta = new Vector2(0, 0);
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.spacing = 10;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = contentRt;
        scrollRect.viewport = vpRt;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;

        // ---------- 行模板(默认隐藏,运行时克隆)----------
        var rowComp = BuildRowTemplate(content.transform);

        // ---------- 空提示 ----------
        var emptyGo = NewUI("EmptyHint", out var emptyRt, window.transform);
        emptyRt.anchorMin = new Vector2(0, 0); emptyRt.anchorMax = new Vector2(1, 1);
        emptyRt.offsetMin = new Vector2(40, 110); emptyRt.offsetMax = new Vector2(-40, -90);
        var emptyText = NewText(emptyGo, "暂无存档", 30, TextAlignmentOptions.Center);
        emptyText.color = new Color(0.7f, 0.7f, 0.72f, 1f);
        emptyGo.SetActive(false); // 默认隐藏,清单就绪后由面板按是否为空切换

        // ---------- 返回按钮 ----------
        var backBtn = BuildButton("BackBtn", "返回", window.transform,
            new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(220, 64), new Color(0.3f, 0.32f, 0.4f, 1f));

        // ---------- 接脚本字段 ----------
        var panel = root.AddComponent<SaveSlotPanel>();
        panel.canvasGroup = cg;
        panel.uiType = UIEnum.SaveSlotPanel;
        panel.btn_back = backBtn;
        panel.content = contentRt;
        panel.rowTemplate = rowComp;
        panel.emptyHint = emptyGo;

        rowComp.gameObject.SetActive(false); // 模板隐藏

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[SaveSlotPanelBuilder] Built {PrefabPath}");
    }

    // ============================ 行模板 ============================

    private static SaveSlotRow BuildRowTemplate(Transform content)
    {
        var row = NewUI("RowTemplate", out var rowRt, content);
        var bg = row.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.22f, 0.27f, 1f);
        var loadBtn = row.AddComponent<Button>();
        loadBtn.targetGraphic = bg;
        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 96;

        // 存档名(上)
        var nameGo = NewUI("Name", out var nameRt, row.transform);
        nameRt.anchorMin = new Vector2(0, 0.45f); nameRt.anchorMax = new Vector2(0.72f, 1f);
        nameRt.offsetMin = new Vector2(24, 0); nameRt.offsetMax = new Vector2(0, -6);
        var nameText = NewText(nameGo, "存档 1", 30, TextAlignmentOptions.Left);

        // 最后游玩时间(下)
        var timeGo = NewUI("Time", out var timeRt, row.transform);
        timeRt.anchorMin = new Vector2(0, 0f); timeRt.anchorMax = new Vector2(0.72f, 0.45f);
        timeRt.offsetMin = new Vector2(24, 6); timeRt.offsetMax = new Vector2(0, 0);
        var timeText = NewText(timeGo, "—", 20, TextAlignmentOptions.Left);
        timeText.color = new Color(0.72f, 0.74f, 0.78f, 1f);

        // 删除按钮(右)
        var delBtn = BuildButton("DeleteBtn", "删除", row.transform,
            new Vector2(1, 0.5f), new Vector2(-100, 0), new Vector2(150, 60), new Color(0.5f, 0.25f, 0.25f, 1f));

        var rowComp = row.AddComponent<SaveSlotRow>();
        rowComp.nameText = nameText;
        rowComp.timeText = timeText;
        rowComp.loadBtn = loadBtn;
        rowComp.deleteBtn = delBtn;
        return rowComp;
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
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
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

        var labelGo = NewUI("Label", out var labelRt, go.transform);
        Stretch(labelRt);
        NewText(labelGo, label, 26, TextAlignmentOptions.Center);
        return btn;
    }
}
#endif
