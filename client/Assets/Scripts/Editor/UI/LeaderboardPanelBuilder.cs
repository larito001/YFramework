#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成排行榜界面预制体 LeaderboardPanel.prefab 到 Resources/UI/Leaderboard 下,供 UIMgr/ResMgr 按路径加载。
/// 只搭外壳:深色背景 + 返回(左上) + 标题「排行榜」 + 居中状态提示 + 竖向滚动列表容器(ScrollRect)。
/// 榜单行(名次 + 昵称 + 分数)由 <see cref="LeaderboardPanel"/> 运行时按 <see cref="ILeaderboardService"/> 拉取结果构建。
///
/// 尺寸按"画布宽恒为 1920 单位"(CanvasScaler match=width)给,竖屏四边留边自适应。
/// 菜单:Tools/UI/Build LeaderboardPanel Prefab
/// </summary>
public static class LeaderboardPanelBuilder
{
    private const string Dir = "Assets/Resources/UI/Leaderboard";
    private const string PrefabPath = Dir + "/LeaderboardPanel.prefab";
    private const string ButtonPrefabPath = "Assets/Resources/UI/Common/CommonButton.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";

    private static TMP_FontAsset _font;
    private static GameObject _btnPrefab;

    [MenuItem("Tools/UI/Build LeaderboardPanel Prefab")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        _btnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabPath);
        if (_btnPrefab == null)
        {
            Debug.LogError($"[LeaderboardPanelBuilder] 未找到通用按钮 {ButtonPrefabPath},无法生成。");
            return;
        }

        // ---------- 根 ----------
        var root = NewUI("LeaderboardPanel", out var rootRt);
        Stretch(rootRt);
        root.AddComponent<Image>().color = new Color(0.10f, 0.11f, 0.14f, 1f);
        var cg = root.AddComponent<CanvasGroup>();
        root.AddComponent<YOTOUIShow>();

        // ---------- 顶部:返回(左上)----------
        var backBtn = BuildButton("Btn_Back", "返回", root.transform, 44);
        var backRt = (RectTransform)backBtn.transform;
        backRt.anchorMin = backRt.anchorMax = new Vector2(0, 1); backRt.pivot = new Vector2(0, 1);
        backRt.anchoredPosition = new Vector2(40, -40); backRt.sizeDelta = new Vector2(220, 110);

        // ---------- 标题 ----------
        var titleGo = NewUI("Title", out var titleRt, root.transform);
        titleRt.anchorMin = new Vector2(0, 1); titleRt.anchorMax = new Vector2(1, 1); titleRt.pivot = new Vector2(0.5f, 1);
        titleRt.anchoredPosition = new Vector2(0, -60); titleRt.sizeDelta = new Vector2(-120, 110);
        NewText(titleGo, "排行榜", 60, TextAlignmentOptions.Center, Color.white);

        // ---------- 中部:竖向滚动列表 ----------
        var scrollGo = NewUI("Scroll", out var scrollRt, root.transform);
        scrollRt.anchorMin = Vector2.zero; scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = new Vector2(50, 60); scrollRt.offsetMax = new Vector2(-50, -200);
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 40f;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        var viewport = NewUI("Viewport", out var viewportRt, scrollGo.transform);
        Stretch(viewportRt);
        var vpImg = viewport.AddComponent<Image>();
        vpImg.sprite = BuiltinSprite("UI/Skin/UISprite.psd");
        vpImg.type = Image.Type.Sliced;
        vpImg.color = new Color(0.16f, 0.17f, 0.21f, 1f);
        viewport.AddComponent<RectMask2D>();

        var content = NewUI("Content", out var contentRt, viewport.transform);
        contentRt.anchorMin = new Vector2(0, 1); contentRt.anchorMax = new Vector2(1, 1); contentRt.pivot = new Vector2(0.5f, 1);
        contentRt.anchoredPosition = Vector2.zero; contentRt.sizeDelta = Vector2.zero;
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 12;
        vlg.padding = new RectOffset(16, 16, 16, 16);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
        var csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewportRt;
        scroll.content = contentRt;

        // ---------- 状态提示(居中,覆盖在滚动区上)----------
        var statusGo = NewUI("Status", out var statusRt, scrollGo.transform);
        Stretch(statusRt);
        var statusText = NewText(statusGo, "加载中…", 44, TextAlignmentOptions.Center, new Color(0.8f, 0.8f, 0.85f, 1f));

        // ---------- 接脚本字段 ----------
        var panel = root.AddComponent<LeaderboardPanel>();
        panel.canvasGroup = cg;
        panel.uiType = UIEnum.LeaderboardPanel;
        panel.backBtn = backBtn;
        panel.content = contentRt;
        panel.statusText = statusText;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[LeaderboardPanelBuilder] Built {PrefabPath}");
    }

    // ============================ 构件 / 工具 ============================

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

    private static Sprite BuiltinSprite(string path)
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>(path);
    }
}
#endif
