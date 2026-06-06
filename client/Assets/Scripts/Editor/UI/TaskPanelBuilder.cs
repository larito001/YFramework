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
///     ├ Coin/Energy 资源胶囊(顶部;UICurrencyPill,半透明黑底 + 图标 + 右对齐数值)
///     ├ PanelTitle  顶部「任务」标题整图(美术 sprite)
///     ├ Tabs   页签容器(HorizontalLayoutGroup),内含两个 CommonButton 实例:
///     │         日常任务 / 成就任务(选中绿/未选灰由 TaskPanel.SetTabColor 染 Bg 控制)
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
    private const string ViewportSpritePath = "Assets/Art/UI/NewUI/Shared/Sprite_Common/Frame/PanelFrame/PanelFrame_03_White_Bg.png";
    // 顶部标题图(美术整图「任务」,反推自预制体 PanelTitle 的 sprite)
    private const string PanelTitleSpritePath = "Assets/Art/UI/NewUI/Theme_Blue/Sprites/20260606-195615.png";

    private static readonly Color RootBg = new Color(0.91f, 0.90f, 0.95f, 1f);
    private static readonly Color ViewportTint = UITheme.PanelBacking; // 统一内容底板(深冷色,非纯黑)

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

        // ---------- 资源胶囊(顶部,与主页一致:金币在左 + 体力在右;胶囊宽度随数字自适应,不超框)----------
        // 左上锚点向右延展;金币与体力拉开足够间距(数字再多金币也长不到体力上)。
        var coinText = UICurrencyPill.Build(root.transform, "Coin", UICurrencyPill.IconGold, _font,
            new Vector2(0, 1), new Vector2(300, -48), new Vector2(0, 120), 48);
        var energyText = UICurrencyPill.Build(root.transform, "Energy", UICurrencyPill.IconEnergy, _font,
            new Vector2(0, 1), new Vector2(760, -48), new Vector2(0, 120), 48);

        // ---------- 顶部标题「任务」图(美术整图;反推自预制体的 sprite 与尺寸)----------
        var panelTitle = NewUI("PanelTitle", out var panelTitleRt, root.transform);
        panelTitleRt.anchorMin = panelTitleRt.anchorMax = new Vector2(0.5f, 1); panelTitleRt.pivot = new Vector2(0.5f, 1);
        panelTitleRt.anchoredPosition = new Vector2(0, -294); panelTitleRt.sizeDelta = new Vector2(853.2109f, 625.8423f);
        var panelTitleImg = panelTitle.AddComponent<Image>();
        panelTitleImg.sprite = LoadSprite(PanelTitleSpritePath);
        panelTitleImg.color = Color.white; panelTitleImg.preserveAspect = true; panelTitleImg.raycastTarget = false;

        // ---------- 页签容器:日常任务 / 成就任务(横向铺满,CommonButton 实例;下移到标题下方)----------
        var tabs = NewUI("Tabs", out var tabsRt, root.transform);
        tabsRt.anchorMin = new Vector2(0, 1); tabsRt.anchorMax = new Vector2(1, 1); tabsRt.pivot = new Vector2(0.5f, 1);
        tabsRt.anchoredPosition = new Vector2(0, -883); tabsRt.sizeDelta = new Vector2(-574.6357f, 160);
        var tabsHlg = tabs.AddComponent<HorizontalLayoutGroup>();
        tabsHlg.spacing = 100;
        tabsHlg.childAlignment = TextAnchor.MiddleCenter;
        tabsHlg.childControlWidth = true; tabsHlg.childControlHeight = true;
        tabsHlg.childForceExpandWidth = true; tabsHlg.childForceExpandHeight = true;

        // 两个通用绿色按钮(日常/成就);选中绿、未选灰由 TaskPanel.SetTabColor 染 Bg 控制。
        var commonBtn = AssetDatabase.LoadAssetAtPath<GameObject>(UIButtonFactory.CommonButtonPath);
        var tabDaily = UIButtonFactory.Build(commonBtn, "Tab_Daily", "日常任务", tabs.transform, fontSize: 34);
        var tabRegular = UIButtonFactory.Build(commonBtn, "Tab_Achieve", "成就任务", tabs.transform, fontSize: 34);

        // ---------- 竖向滚动任务列表(反推自预制体:撑到标题/页签下方) ----------
        var scrollGo = NewUI("Scroll", out var scrollRt, root.transform);
        scrollRt.anchorMin = Vector2.zero; scrollRt.anchorMax = Vector2.one;
        scrollRt.anchoredPosition = new Vector2(0, -260.45792f); scrollRt.sizeDelta = new Vector2(-100, -1565.116f);
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 40f;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        // 视口(裁剪)
        var viewport = NewUI("Viewport", out var viewportRt, scrollGo.transform);
        Stretch(viewportRt);
        var vpImg = viewport.AddComponent<Image>();
        vpImg.color = new Color(1f, 1f, 1f, 0.004f); // 透明:去掉列表背景(仅接拖拽)
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
        panel.energyText = energyText;
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
