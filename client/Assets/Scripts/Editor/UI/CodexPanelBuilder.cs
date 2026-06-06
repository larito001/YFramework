#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成图鉴界面预制体 CodexPanel.prefab 到 Resources/UI/Codex 下,供 UIMgr/ResMgr 按路径加载。
/// 外壳已按美术返工后的预制体反推:
///   根:浅紫 Image + CanvasGroup + YOTOUIShow + CodexPanel
///     ├ bg      (art-kit Resources/UI/bg.prefab)      全屏背景图
///     ├ backBtn (art-kit Resources/UI/backBtn.prefab) 返回(左上)
///     ├ Coin    资源金币(绿色胶囊,UICurrencyPill.AddIconLeft 接金币图标)
///     ├ Panel   中部内容板(深色半透明底,内含 2 列网格 Grid)
///     └ Pager   底部分页条(HorizontalLayoutGroup;4×nextBtn.prefab + 居中页码 PageText)
///
/// 分页方向键用 art-kit Resources/UI/nextBtn.prefab;页码文本为程序化 TMP;背景/返回为 art-kit prefab 实例。
/// 卡片(3D 模型 + 解锁徽标)由 <see cref="CodexPanel"/> 运行时按 animal 配表分页构建。
/// 菜单:Tools/UI/Build CodexPanel Prefab
/// </summary>
public static class CodexPanelBuilder
{
    private const string Dir = "Assets/Resources/UI/Codex";
    private const string PrefabPath = Dir + "/CodexPanel.prefab";
    private const string NextBtnPrefabPath = "Assets/Resources/UI/nextBtn.prefab"; // 分页方向键(美术按钮)
    private const string BgPrefabPath = "Assets/Resources/UI/bg.prefab";
    private const string BackBtnPrefabPath = "Assets/Resources/UI/backBtn.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";

    private static TMP_FontAsset _font;
    private static GameObject _nextBtnPrefab, _bgPrefab, _backBtnPrefab;

    [MenuItem("Tools/UI/Build CodexPanel Prefab")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        _nextBtnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NextBtnPrefabPath);
        _bgPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BgPrefabPath);
        _backBtnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BackBtnPrefabPath);
        if (_nextBtnPrefab == null)
        {
            Debug.LogError($"[CodexPanelBuilder] 未找到分页按钮 {NextBtnPrefabPath},无法生成。");
            return;
        }

        // ---------- 根(浅紫背景 + CanvasGroup + YOTOUIShow + CodexPanel)----------
        var root = NewUI("CodexPanel", out var rootRt);
        Stretch(rootRt);
        root.AddComponent<Image>().color = new Color(0.91f, 0.9f, 0.95f, 1f); // 浅紫底(Simple,无 Sprite)
        var cg = root.AddComponent<CanvasGroup>();
        root.AddComponent<YOTOUIShow>();

        // ---------- 全屏背景图(art-kit bg.prefab)----------
        var bg = (GameObject)PrefabUtility.InstantiatePrefab(_bgPrefab, root.transform);
        var bgRt = (RectTransform)bg.transform;
        bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.anchoredPosition = Vector2.zero;
        bgRt.sizeDelta = new Vector2(5504.974f, 3669.982f);

        // ---------- 返回(art-kit backBtn.prefab,左上)----------
        var backGo = (GameObject)PrefabUtility.InstantiatePrefab(_backBtnPrefab, root.transform);
        backGo.name = "backBtn";
        var backRt = (RectTransform)backGo.transform;
        backRt.anchorMin = backRt.anchorMax = new Vector2(0, 1); backRt.pivot = new Vector2(0.5f, 0.5f);
        backRt.anchoredPosition = new Vector2(155.2f, -111.9f); backRt.sizeDelta = new Vector2(243.246f, 201.326f);
        var backBtn = backGo.GetComponent<Button>();

        // ---------- 顶部:资源金币(统一资源胶囊,返回按钮右侧;图标运行时动态加载)----------
        var coinGo = NewUI("Coin", out var coinRt, root.transform);
        coinRt.anchorMin = coinRt.anchorMax = new Vector2(0, 1); coinRt.pivot = new Vector2(0, 1);
        coinRt.anchoredPosition = new Vector2(280, -48); coinRt.sizeDelta = new Vector2(820, 96);
        var coinBg = coinGo.AddComponent<Image>();
        UICurrencyPill.ApplyBackground(coinBg);
        var coinText = NewChildText(coinGo, "Value", "0", 40, TextAlignmentOptions.Right);
        ((RectTransform)coinText.transform).offsetMax = new Vector2(-28, 0); // 右侧留边距
        UICurrencyPill.AddIconLeft(coinGo, (RectTransform)coinText.transform, UICurrencyPill.IconGold); // 金币图标,运行时加载

        // ---------- 中部:内容面板(深色半透明底 + 2 列网格容器)----------
        var panelGo = NewUI("Panel", out var panelRt, root.transform);
        panelRt.anchorMin = Vector2.zero; panelRt.anchorMax = Vector2.one; panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = new Vector2(0, 53.23253f);
        panelRt.sizeDelta = new Vector2(-100, -613.5349f);
        var panelBg = panelGo.AddComponent<Image>();
        panelBg.sprite = BuiltinSprite("UI/Skin/UISprite.psd");
        panelBg.type = Image.Type.Sliced;
        panelBg.color = new Color(0f, 0f, 0f, 0.5294118f); // 深色半透明底

        var gridGo = NewUI("Grid", out var gridRt, panelGo.transform);
        gridRt.anchorMin = Vector2.zero; gridRt.anchorMax = Vector2.one; gridRt.pivot = new Vector2(0.5f, 0.5f);
        gridRt.anchoredPosition = Vector2.zero;
        gridRt.sizeDelta = new Vector2(-60, -60); // 四边各留 30
        var glg = gridGo.AddComponent<GridLayoutGroup>();
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 2;
        glg.cellSize = new Vector2(820, 880);
        glg.spacing = new Vector2(40, 40);
        glg.padding = new RectOffset(30, 30, 30, 30);
        glg.childAlignment = TextAnchor.UpperCenter;

        // ---------- 底部:分页条(首页 / 上一页 / 页码 / 下一页 / 末页)----------
        var pager = NewUI("Pager", out var pagerRt, root.transform);
        pagerRt.anchorMin = new Vector2(0, 0); pagerRt.anchorMax = new Vector2(1, 0); pagerRt.pivot = new Vector2(0.5f, 0);
        pagerRt.anchoredPosition = new Vector2(-0.000061035156f, 152);
        pagerRt.sizeDelta = new Vector2(-1039.6182f, 123.2462f);
        var pagerHlg = pager.AddComponent<HorizontalLayoutGroup>();
        pagerHlg.spacing = 30;
        pagerHlg.childAlignment = TextAnchor.UpperLeft;
        pagerHlg.childControlWidth = true; pagerHlg.childControlHeight = true;
        pagerHlg.childForceExpandWidth = true; pagerHlg.childForceExpandHeight = true;

        // 分页方向键:用 nextBtn.prefab(美术分页按钮),名字与文本按预制体还原(首键沿用 nextBtn 默认文本)
        var btnFirst = BuildNavButton("nextBtn", null, pager.transform);
        var btnPrev = BuildNavButton("nextBtn (1)", "<", pager.transform);
        var pageText = BuildPageText(pager.transform);
        var btnNext = BuildNavButton("nextBtn (2)", ">", pager.transform);
        var btnLast = BuildNavButton("nextBtn (3)", ">>", pager.transform);

        // ---------- 接脚本字段 ----------
        var panel = root.AddComponent<CodexPanel>();
        panel.canvasGroup = cg;
        panel.uiType = UIEnum.CodexPanel;
        panel.backBtn = backBtn;
        panel.coinText = coinText;
        panel.grid = gridRt;
        panel.pageText = pageText;
        panel.btnFirst = btnFirst;
        panel.btnPrev = btnPrev;
        panel.btnNext = btnNext;
        panel.btnLast = btnLast;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[CodexPanelBuilder] Built {PrefabPath}");
    }

    // ============================ 构件 ============================

    /// <summary>分页方向按钮:nextBtn.prefab 实例,尺寸由父级 HorizontalLayoutGroup 拉伸控制。
    /// <paramref name="label"/> 为 null 时保留 nextBtn 自带的默认文本。</summary>
    private static Button BuildNavButton(string name, string label, Transform parent)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(_nextBtnPrefab, parent);
        go.name = name;
        go.SetActive(true);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero; // 由 HLG 控制,缩放保留 CommonButton 自带的 3 倍
        if (label != null)
        {
            var text = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null) text.text = label;
        }
        return go.GetComponent<Button>();
    }

    /// <summary>页码文本:夹在方向按钮中间,深色显示在浅底上。</summary>
    private static TextMeshProUGUI BuildPageText(Transform parent)
    {
        var go = NewUI("PageText", out var rt, parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0, 0); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero; rt.sizeDelta = Vector2.zero;
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = 260; le.preferredHeight = 110;
        return NewText(go, "1 / 1", 52, TextAlignmentOptions.Center, new Color(0.25f, 0.24f, 0.3f, 1f));
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