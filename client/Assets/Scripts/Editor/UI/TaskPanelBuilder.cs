#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成任务界面预制体 TaskPanel.prefab 到 Resources/UI/Task 下,供 UIMgr/ResMgr 按路径加载。
/// 只搭外壳:浅紫背景 + 返回 / 资源金币(绿色胶囊) + 每日任务/常规任务 两页签 + 竖向滚动列表容器(ScrollRect)。
/// 任务卡(标题 + 进度 + 奖励 + 前往)由 <see cref="TaskPanel"/> 运行时按配表 <c>taskConfig</c> 构建。
///
/// 尺寸按"画布宽恒为 1920 单位"(CanvasScaler match=width)给,竖屏四边留边自适应。
/// 菜单:Tools/UI/Build TaskPanel Prefab
/// </summary>
public static class TaskPanelBuilder
{
    private const string Dir = "Assets/Resources/UI/Task";
    private const string PrefabPath = Dir + "/TaskPanel.prefab";
    private const string ButtonPrefabPath = "Assets/Resources/UI/Common/CommonButton.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";

    private static TMP_FontAsset _font;
    private static GameObject _btnPrefab;

    [MenuItem("Tools/UI/Build TaskPanel Prefab")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        _btnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabPath);
        if (_btnPrefab == null)
        {
            Debug.LogError($"[TaskPanelBuilder] 未找到通用按钮 {ButtonPrefabPath},无法生成。");
            return;
        }

        // ---------- 根(浅紫背景 + CanvasGroup + YOTOUIShow + TaskPanel)----------
        var root = NewUI("TaskPanel", out var rootRt);
        Stretch(rootRt);
        root.AddComponent<Image>().color = new Color(0.91f, 0.90f, 0.95f, 1f);
        var cg = root.AddComponent<CanvasGroup>();
        root.AddComponent<YOTOUIShow>();

        // ---------- 顶部:返回(左上)----------
        var backBtn = BuildButton("Btn_Back", "返回", root.transform, 44);
        var backRt = (RectTransform)backBtn.transform;
        backRt.anchorMin = backRt.anchorMax = new Vector2(0, 1); backRt.pivot = new Vector2(0, 1);
        backRt.anchoredPosition = new Vector2(40, -40); backRt.sizeDelta = new Vector2(200, 90);

        // ---------- 顶部:资源金币(绿色胶囊)----------
        var coinGo = NewUI("Coin", out var coinRt, root.transform);
        coinRt.anchorMin = coinRt.anchorMax = new Vector2(0, 1); coinRt.pivot = new Vector2(0, 1);
        coinRt.anchoredPosition = new Vector2(280, -48); coinRt.sizeDelta = new Vector2(340, 96);
        var coinBg = coinGo.AddComponent<Image>();
        coinBg.sprite = BuiltinSprite("UI/Skin/UISprite.psd");
        coinBg.type = Image.Type.Sliced;
        coinBg.color = new Color(0.56f, 0.78f, 0.30f, 1f);
        var coinText = NewChildText(coinGo, "Value", "0", 48, TextAlignmentOptions.Right);
        ((RectTransform)coinText.transform).offsetMax = new Vector2(-28, 0);

        // ---------- 页签:每日任务 / 常规任务 ----------
        var tabs = NewUI("Tabs", out var tabsRt, root.transform);
        tabsRt.anchorMin = new Vector2(0, 1); tabsRt.anchorMax = new Vector2(1, 1); tabsRt.pivot = new Vector2(0.5f, 1);
        tabsRt.anchoredPosition = new Vector2(0, -200); tabsRt.sizeDelta = new Vector2(-120, 120);
        var tabsHlg = tabs.AddComponent<HorizontalLayoutGroup>();
        tabsHlg.spacing = 40;
        tabsHlg.childAlignment = TextAnchor.MiddleCenter;
        tabsHlg.childControlWidth = true; tabsHlg.childControlHeight = true;
        tabsHlg.childForceExpandWidth = false; tabsHlg.childForceExpandHeight = false;

        var tabDaily = BuildTab("Tab_Daily", "每日任务", tabs.transform, new Color(0.56f, 0.78f, 0.30f, 1f)); // 默认选中
        var tabRegular = BuildTab("Tab_Regular", "常规任务", tabs.transform, new Color(0.72f, 0.70f, 0.80f, 1f));

        // ---------- 中部:竖向滚动列表 ----------
        var scrollGo = NewUI("Scroll", out var scrollRt, root.transform);
        scrollRt.anchorMin = Vector2.zero; scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = new Vector2(50, 140); scrollRt.offsetMax = new Vector2(-50, -360);
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 40f;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        // 视口(裁剪)
        var viewport = NewUI("Viewport", out var viewportRt, scrollGo.transform);
        Stretch(viewportRt);
        var vpImg = viewport.AddComponent<Image>();
        vpImg.sprite = BuiltinSprite("UI/Skin/UISprite.psd");
        vpImg.type = Image.Type.Sliced;
        vpImg.color = new Color(0.86f, 0.84f, 0.93f, 1f);
        viewport.AddComponent<RectMask2D>();

        // 内容容器(VerticalLayoutGroup + ContentSizeFitter:卡片自上而下排列,高度自适应)
        var content = NewUI("Content", out var contentRt, viewport.transform);
        contentRt.anchorMin = new Vector2(0, 1); contentRt.anchorMax = new Vector2(1, 1); contentRt.pivot = new Vector2(0.5f, 1);
        contentRt.anchoredPosition = Vector2.zero; contentRt.sizeDelta = new Vector2(0, 0);
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 28;
        vlg.padding = new RectOffset(24, 24, 24, 24);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
        var csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewportRt;
        scroll.content = contentRt;

        // ---------- 接脚本字段 ----------
        var panel = root.AddComponent<TaskPanel>();
        panel.canvasGroup = cg;
        panel.uiType = UIEnum.TaskPanel;
        panel.backBtn = backBtn;
        panel.coinText = coinText;
        panel.tabDaily = tabDaily;
        panel.tabRegular = tabRegular;
        panel.content = contentRt;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[TaskPanelBuilder] Built {PrefabPath}");
    }

    // ============================ 构件 ============================

    private static Button BuildTab(string name, string label, Transform parent, Color color)
    {
        var btn = BuildButton(name, label, parent, 48);
        var le = btn.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 420; le.preferredHeight = 110;
        if (btn.targetGraphic is Image img) img.color = color;
        return btn;
    }

    private static Button BuildButton(string name, string label, Transform parent, float fontSize)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(_btnPrefab, parent);
        go.name = name;
        go.SetActive(true);
        var text = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null) { text.text = label; text.fontSize = UITheme.Font(fontSize); }
        return go.GetComponent<Button>();
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

    private static TextMeshProUGUI NewChildText(GameObject parent, string name, string text, float size, TextAlignmentOptions align)
    {
        var go = NewUI(name, out var rt, parent.transform);
        Stretch(rt);
        return NewText(go, text, size, align, Color.white);
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
