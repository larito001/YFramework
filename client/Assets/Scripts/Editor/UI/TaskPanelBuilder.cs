#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成任务界面预制体 TaskPanel.prefab 到 Resources/UI/Task 下,供 UIMgr/ResMgr 按路径加载。
/// 外壳从美术返工后的预制体反推(art-kit 实例 + 自定义布局):
///   根:浅紫 Image + CanvasGroup + YOTOUIShow + TaskPanel
///     ├ bg     (Resources/UI/bg)     全屏背景图(prefab 实例,偏置摆放)
///     ├ back   (Resources/UI/backBtn) 返回(左上,prefab 实例)
///     ├ Coin   绿色资源胶囊(右上偏左;深绿底 + 左侧金币图标 + 右对齐数值)
///     ├ Tabs   页签容器(HorizontalLayoutGroup),内含两个 Tab_01.prefab 实例:
///     │         每日任务 / 主线任务(选中态靠 Tab_01 的 "Focus" 子物体,见 TaskPanel.SetTabColor)
///     └ Scroll 竖向滚动任务列表(Viewport + VerticalLayoutGroup 容器 Content)
///
/// 任务卡(标题 + 进度 + 奖励 + 前往/领取)由 <see cref="TaskPanel"/> 运行时按配表 <c>taskConfig</c> 动态生成,不进 Builder。
/// 货币图标不烤进预制体:Icon 上挂 CurrencyIconBinder,运行时按币种从 Resources 动态加载(见 UICurrencyPill.AddIconLeft)。
/// 菜单:Tools/UI/Build TaskPanel Prefab
/// </summary>
public static class TaskPanelBuilder
{
    private const string Dir = "Assets/Resources/UI/Task";
    private const string PrefabPath = Dir + "/TaskPanel.prefab";
    private const string BgPrefabPath = "Assets/Resources/UI/bg.prefab";
    private const string BackBtnPrefabPath = "Assets/Resources/UI/backBtn.prefab";
    private const string TabPrefabPath = "Assets/Resources/UI/Tab_01.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";
    // 视口底框(art-kit 半透明圆角盒;预制体里 Viewport 用的就是它)
    private const string ViewportSpritePath = "Assets/Art/UI/NewUI/Shared/Sprite_Common/Popup/Popup_Box_01~03_White_Bg.png";

    // Coin 胶囊为绿色(非 UICurrencyPill 默认深灰),故自定义底色 + AddIconLeft。
    private static readonly Color CoinPill = new Color(0.56f, 0.78f, 0.30f, 1f);
    private static readonly Color RootBg = new Color(0.91f, 0.90f, 0.95f, 1f);
    private static readonly Color ViewportTint = new Color(0f, 0f, 0f, 0.5254902f);

    private static TMP_FontAsset _font;
    private static GameObject _bgPrefab, _backBtnPrefab, _tabPrefab;

    [MenuItem("Tools/UI/Build TaskPanel Prefab")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        _bgPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BgPrefabPath);
        _backBtnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BackBtnPrefabPath);
        _tabPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TabPrefabPath);
        if (_bgPrefab == null || _backBtnPrefab == null || _tabPrefab == null)
        {
            Debug.LogError($"[TaskPanelBuilder] 缺少 art-kit 预制体(bg/backBtn/Tab_01),无法生成。");
            return;
        }

        // ---------- 根(浅紫背景 + CanvasGroup + YOTOUIShow + TaskPanel)----------
        var root = NewUI("TaskPanel", out var rootRt);
        Stretch(rootRt);
        root.AddComponent<Image>().color = RootBg;
        var cg = root.AddComponent<CanvasGroup>();
        root.AddComponent<YOTOUIShow>();

        // ---------- 全屏背景图(art-kit bg.prefab 实例)----------
        var bg = (GameObject)PrefabUtility.InstantiatePrefab(_bgPrefab, root.transform);
        var bgRt = (RectTransform)bg.transform;
        bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.anchoredPosition = new Vector2(121f, -60f);
        bgRt.sizeDelta = new Vector2(5504.974f, 3669.982f);

        // ---------- 返回(左上,art-kit backBtn.prefab 实例)----------
        var backGo = (GameObject)PrefabUtility.InstantiatePrefab(_backBtnPrefab, root.transform);
        backGo.name = "back";
        var backRt = (RectTransform)backGo.transform;
        backRt.anchorMin = backRt.anchorMax = new Vector2(0, 1); backRt.pivot = new Vector2(0.5f, 0.5f);
        backRt.anchoredPosition = new Vector2(155.2f, -111.9f); backRt.sizeDelta = new Vector2(243.246f, 201.326f);
        var backBtn = backGo.GetComponent<Button>();

        // ---------- 资源金币(右上偏左,绿色胶囊:深绿底 + 左侧金币图标 + 右对齐数值)----------
        var coinGo = NewUI("Coin", out var coinRt, root.transform);
        coinRt.anchorMin = coinRt.anchorMax = new Vector2(0, 1); coinRt.pivot = new Vector2(0, 1);
        coinRt.anchoredPosition = new Vector2(300, -48); coinRt.sizeDelta = new Vector2(360, 120);
        var coinBg = coinGo.AddComponent<Image>();
        coinBg.sprite = BuiltinSprite("UI/Skin/UISprite.psd");
        coinBg.type = Image.Type.Sliced;
        coinBg.color = CoinPill;
        var coinText = NewChildText(coinGo, "Value", "0", 48, TextAlignmentOptions.Right);
        UICurrencyPill.AddIconLeft(coinGo, (RectTransform)coinText.transform, UICurrencyPill.IconGold); // 金币图标运行时动态加载

        // ---------- 页签容器:每日任务 / 主线任务(横向铺满,Tab_01.prefab 实例)----------
        var tabs = NewUI("Tabs", out var tabsRt, root.transform);
        tabsRt.anchorMin = new Vector2(0, 1); tabsRt.anchorMax = new Vector2(1, 1); tabsRt.pivot = new Vector2(0.5f, 1);
        tabsRt.anchoredPosition = new Vector2(0, -216); tabsRt.sizeDelta = new Vector2(-100, 160);
        var tabsHlg = tabs.AddComponent<HorizontalLayoutGroup>();
        tabsHlg.spacing = 24;
        tabsHlg.childAlignment = TextAnchor.MiddleCenter;
        tabsHlg.childControlWidth = true; tabsHlg.childControlHeight = true;
        tabsHlg.childForceExpandWidth = true; tabsHlg.childForceExpandHeight = true;

        var tabDaily = BuildTab("Tab_01", "每日任务", tabs.transform);     // 默认选中(Focus 由 TaskPanel 运行时切换)
        var tabRegular = BuildTab("Tab_01 (1)", "主线任务", tabs.transform);

        // ---------- 竖向滚动任务列表 ----------
        var scrollGo = NewUI("Scroll", out var scrollRt, root.transform);
        scrollRt.anchorMin = Vector2.zero; scrollRt.anchorMax = Vector2.one;
        scrollRt.anchoredPosition = new Vector2(0, -60); scrollRt.sizeDelta = new Vector2(-100, -620);
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 40f;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        // 视口(裁剪)
        var viewport = NewUI("Viewport", out var viewportRt, scrollGo.transform);
        Stretch(viewportRt);
        var vpImg = viewport.AddComponent<Image>();
        vpImg.sprite = LoadSprite(ViewportSpritePath);
        vpImg.type = Image.Type.Sliced;
        vpImg.color = ViewportTint;
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

    /// <summary>实例化 Tab_01.prefab 页签并写入子 Text(TMP) 文字;尺寸由父级 HorizontalLayoutGroup 控制,故不设 RectTransform。
    /// 选中态由 Tab_01 的 "Focus" 子物体表示(TaskPanel.SetTabColor 运行时切换),Builder 不再用 targetGraphic 变色。</summary>
    private static Button BuildTab(string name, string label, Transform parent)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(_tabPrefab, parent);
        go.name = name;
        go.SetActive(true);
        var text = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null) text.text = label;
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

    private static Sprite LoadSprite(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
#endif
