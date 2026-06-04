#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成图鉴界面预制体 CodexPanel.prefab 到 Resources/UI/Codex 下,供 UIMgr/ResMgr 按路径加载。
/// 只搭外壳:浅紫背景 + 返回 / 资源金币(绿色胶囊) + 居中内容面板(2 列网格容器) + 底部分页条。
/// 卡片(图片 + 解锁徽标)由 <see cref="CodexPanel"/> 运行时按配表 <c>codexConfig</c> 分页构建。
///
/// 尺寸按"画布宽恒为 1920 单位"(CanvasScaler match=width)给,竖屏四边留边自适应。
/// 菜单:Tools/UI/Build CodexPanel Prefab
/// </summary>
public static class CodexPanelBuilder
{
    private const string Dir = "Assets/Resources/UI/Codex";
    private const string PrefabPath = Dir + "/CodexPanel.prefab";
    private const string ButtonPrefabPath = "Assets/Resources/UI/Common/CommonButton.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";

    private static TMP_FontAsset _font;
    private static GameObject _btnPrefab;

    [MenuItem("Tools/UI/Build CodexPanel Prefab")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        _btnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabPath);
        if (_btnPrefab == null)
        {
            Debug.LogError($"[CodexPanelBuilder] 未找到通用按钮 {ButtonPrefabPath},无法生成。");
            return;
        }

        // ---------- 根(浅紫背景 + CanvasGroup + YOTOUIShow + CodexPanel)----------
        var root = NewUI("CodexPanel", out var rootRt);
        Stretch(rootRt);
        root.AddComponent<Image>().color = new Color(0.91f, 0.90f, 0.95f, 1f); // 浅紫底
        var cg = root.AddComponent<CanvasGroup>();
        root.AddComponent<YOTOUIShow>();

        // ---------- 顶部:返回(左上)----------
        var backBtn = BuildButton("Btn_Back", "返回", root.transform, 44);
        var backRt = (RectTransform)backBtn.transform;
        backRt.anchorMin = backRt.anchorMax = new Vector2(0, 1); backRt.pivot = new Vector2(0, 1);
        backRt.anchoredPosition = new Vector2(40, -40); backRt.sizeDelta = new Vector2(200, 90);

        // ---------- 顶部:资源金币(绿色胶囊,返回按钮右侧)----------
        var coinGo = NewUI("Coin", out var coinRt, root.transform);
        coinRt.anchorMin = coinRt.anchorMax = new Vector2(0, 1); coinRt.pivot = new Vector2(0, 1);
        coinRt.anchoredPosition = new Vector2(280, -48); coinRt.sizeDelta = new Vector2(820, 96); // 加宽:要容下「金币 + 图鉴 x/y」两段,原 340 文字会溢出盖住返回键
        var coinBg = coinGo.AddComponent<Image>();
        coinBg.sprite = BuiltinSprite("UI/Skin/UISprite.psd");
        coinBg.type = Image.Type.Sliced;
        coinBg.color = new Color(0.56f, 0.78f, 0.30f, 1f); // 绿色胶囊
        var coinText = NewChildText(coinGo, "Value", "0", 40, TextAlignmentOptions.Right);
        ((RectTransform)coinText.transform).offsetMax = new Vector2(-28, 0); // 右侧留边距
        UICurrencyPill.AddIconLeft(coinGo, (RectTransform)coinText.transform, UICurrencyPill.IconGold); // 金币用图标,不写文字

        // ---------- 中部:内容面板(2 列网格容器)----------
        var panelGo = NewUI("Panel", out var panelRt, root.transform);
        panelRt.anchorMin = Vector2.zero; panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = new Vector2(50, 360); panelRt.offsetMax = new Vector2(-50, -170);
        var panelBg = panelGo.AddComponent<Image>();
        panelBg.sprite = BuiltinSprite("UI/Skin/UISprite.psd");
        panelBg.type = Image.Type.Sliced;
        panelBg.color = new Color(0.86f, 0.84f, 0.93f, 1f); // 内容面板浅紫

        var gridGo = NewUI("Grid", out var gridRt, panelGo.transform);
        gridRt.anchorMin = Vector2.zero; gridRt.anchorMax = Vector2.one;
        gridRt.offsetMin = new Vector2(30, 30); gridRt.offsetMax = new Vector2(-30, -30);
        var glg = gridGo.AddComponent<GridLayoutGroup>();
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 2;
        glg.cellSize = new Vector2(820, 880); // 加高:3 行(880*3+间距+padding≈2780)填满面板,消除底部空地
        glg.spacing = new Vector2(40, 40);
        glg.padding = new RectOffset(30, 30, 30, 30);
        glg.childAlignment = TextAnchor.UpperCenter;

        // ---------- 底部:分页条(首页 / 上一页 / 页码 / 下一页 / 末页)----------
        var pager = NewUI("Pager", out var pagerRt, root.transform);
        pagerRt.anchorMin = new Vector2(0, 0); pagerRt.anchorMax = new Vector2(1, 0); pagerRt.pivot = new Vector2(0.5f, 0);
        pagerRt.offsetMin = new Vector2(50, 140); pagerRt.offsetMax = new Vector2(-50, 300);
        var pagerHlg = pager.AddComponent<HorizontalLayoutGroup>();
        pagerHlg.spacing = 24;
        pagerHlg.childAlignment = TextAnchor.MiddleCenter;
        pagerHlg.childControlWidth = true; pagerHlg.childControlHeight = true;
        pagerHlg.childForceExpandWidth = false; pagerHlg.childForceExpandHeight = false;

        var btnFirst = BuildNavButton("Btn_First", "|<", pager.transform);
        var btnPrev = BuildNavButton("Btn_Prev", "<", pager.transform);
        var pageText = BuildPageText(pager.transform);
        var btnNext = BuildNavButton("Btn_Next", ">", pager.transform);
        var btnLast = BuildNavButton("Btn_Last", ">|", pager.transform);

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

    /// <summary>分页方向按钮:固定首选尺寸的小方按钮。</summary>
    private static Button BuildNavButton(string name, string label, Transform parent)
    {
        var btn = BuildButton(name, label, parent, 48);
        var le = btn.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 150; le.preferredHeight = 110;
        return btn;
    }

    /// <summary>页码文本:夹在方向按钮中间,深色显示在浅底上。</summary>
    private static TextMeshProUGUI BuildPageText(Transform parent)
    {
        var go = NewUI("PageText", out var rt, parent);
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = 260; le.preferredHeight = 110;
        return NewText(go, "1 / 1", 52, TextAlignmentOptions.Center, new Color(0.25f, 0.24f, 0.30f, 1f));
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
