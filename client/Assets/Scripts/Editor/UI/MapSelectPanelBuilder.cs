#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成选择关卡界面预制体 MapSelectPanel.prefab 到 Resources/UI/Map 下。
/// 本构建器对齐美术返工后的现预制体外壳:
///   根 MapSelectPanel:浅紫底色 Image + CanvasGroup + YOTOUIShow + MapSelectPanel
///     ├ bg      (Resources/UI/bg.prefab,art-kit 实例)   全屏背景图,压在浅紫底色之上
///     ├ backBtn (Resources/UI/backBtn.prefab,art-kit 实例) 返回(左上)
///     ├ Coin    资源金币胶囊(绿色底 + 左侧货币图标 + 右对齐数值)
///     ├ Title   「关卡列表」标题
///     └ Scroll  竖向滚动的 2 列卡片网格容器(Viewport/Grid)
///
/// 关卡卡(预览图 + 第N关名称 + 锁罩)由 <see cref="MapSelectPanel"/> 运行时按配表 <c>mapConfig</c> 动态构建,不进本外壳。
/// 货币图标不烤进预制体:由 <see cref="UICurrencyPill.AddIconLeft"/> 挂 CurrencyIconBinder,运行时按币种从 Resources 加载。
///
/// 尺寸按"画布宽恒为 1920 单位"(CanvasScaler match=width)给。
/// 菜单:Tools/UI/Build MapSelectPanel Prefab
/// </summary>
public static class MapSelectPanelBuilder
{
    private const string Dir = "Assets/Resources/UI/Map";
    private const string PrefabPath = Dir + "/MapSelectPanel.prefab";
    private const string BgPrefabPath = "Assets/Resources/UI/bg.prefab";
    private const string BackBtnPrefabPath = "Assets/Resources/UI/backBtn.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";

    private static TMP_FontAsset _font;
    private static GameObject _bgPrefab, _backBtnPrefab;

    [MenuItem("Tools/UI/Build MapSelectPanel Prefab")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        _bgPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BgPrefabPath);
        _backBtnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BackBtnPrefabPath);
        if (_bgPrefab == null) { Debug.LogError($"[MapSelectPanelBuilder] 未找到背景 {BgPrefabPath},无法生成。"); return; }
        if (_backBtnPrefab == null) { Debug.LogError($"[MapSelectPanelBuilder] 未找到返回按钮 {BackBtnPrefabPath},无法生成。"); return; }

        // ---------- 根(浅紫底色 + CanvasGroup + YOTOUIShow + MapSelectPanel)----------
        var root = NewUI("MapSelectPanel", out var rootRt);
        Stretch(rootRt);
        root.AddComponent<Image>().color = new Color(0.91f, 0.90f, 0.95f, 1f);
        var cg = root.AddComponent<CanvasGroup>();
        root.AddComponent<YOTOUIShow>();

        // ---------- 全屏背景图(art-kit:bg.prefab,首位子物体,压住浅紫底色)----------
        var bg = (GameObject)PrefabUtility.InstantiatePrefab(_bgPrefab, root.transform);
        bg.name = "bg";
        var bgRt = (RectTransform)bg.transform;
        bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.anchoredPosition = Vector2.zero;
        bgRt.sizeDelta = new Vector2(5504.974f, 3669.982f);

        // ---------- 返回(art-kit:backBtn.prefab,左上)----------
        var backGo = (GameObject)PrefabUtility.InstantiatePrefab(_backBtnPrefab, root.transform);
        backGo.name = "backBtn";
        var backRt = (RectTransform)backGo.transform;
        backRt.anchorMin = backRt.anchorMax = new Vector2(0, 1); backRt.pivot = new Vector2(0.5f, 0.5f);
        backRt.anchoredPosition = new Vector2(155.2f, -111.9f); backRt.sizeDelta = new Vector2(243.246f, 201.326f);
        var backBtn = backGo.GetComponent<Button>();

        // ---------- 顶部:资源金币(统一资源胶囊;图标走 CurrencyIconBinder 运行时加载)----------
        var coinGo = NewUI("Coin", out var coinRt, root.transform);
        coinRt.anchorMin = coinRt.anchorMax = new Vector2(0, 1); coinRt.pivot = new Vector2(0, 1);
        coinRt.anchoredPosition = new Vector2(280, -48); coinRt.sizeDelta = new Vector2(340, 96);
        var coinBg = coinGo.AddComponent<Image>();
        UICurrencyPill.ApplyBackground(coinBg);
        var coinText = NewChildText(coinGo, "Value", "0", 48, TextAlignmentOptions.Right);
        ((RectTransform)coinText.transform).offsetMax = new Vector2(-28, 0);
        UICurrencyPill.AddIconLeft(coinGo, (RectTransform)coinText.transform, UICurrencyPill.IconGold); // 金币用图标,不写文字

        // ---------- 标题:关卡列表 ----------
        var titleGo = NewUI("Title", out var titleRt, root.transform);
        titleRt.anchorMin = new Vector2(0, 1); titleRt.anchorMax = new Vector2(1, 1); titleRt.pivot = new Vector2(0.5f, 1);
        titleRt.anchoredPosition = new Vector2(0, -190); titleRt.sizeDelta = new Vector2(0, 110);
        NewText(titleGo, "关卡列表", 64, TextAlignmentOptions.Center, new Color(0.25f, 0.24f, 0.30f, 1f));

        // ---------- 中部:竖向滚动的双列网格 ----------
        var scrollGo = NewUI("Scroll", out var scrollRt, root.transform);
        scrollRt.anchorMin = Vector2.zero; scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = new Vector2(50, 140); scrollRt.offsetMax = new Vector2(-50, -340);
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 40f;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        var viewport = NewUI("Viewport", out var viewportRt, scrollGo.transform);
        Stretch(viewportRt);
        var vpImg = viewport.AddComponent<Image>();
        vpImg.sprite = BuiltinSprite("UI/Skin/UISprite.psd");
        vpImg.type = Image.Type.Sliced;
        vpImg.color = new Color(0f, 0f, 0f, 0.5294118f); // 半透明黑底(对齐美术返工后的预制体)
        viewport.AddComponent<RectMask2D>();

        var grid = NewUI("Grid", out var gridRt, viewport.transform);
        gridRt.anchorMin = new Vector2(0, 1); gridRt.anchorMax = new Vector2(1, 1); gridRt.pivot = new Vector2(0.5f, 1);
        gridRt.anchoredPosition = Vector2.zero; gridRt.sizeDelta = Vector2.zero;
        var glg = grid.AddComponent<GridLayoutGroup>();
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 2;
        glg.cellSize = new Vector2(810, 540);
        glg.spacing = new Vector2(40, 40);
        glg.padding = new RectOffset(30, 30, 30, 30);
        glg.childAlignment = TextAnchor.UpperCenter;
        var csf = grid.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewportRt;
        scroll.content = gridRt;

        // ---------- 接脚本字段 ----------
        var panel = root.AddComponent<MapSelectPanel>();
        panel.canvasGroup = cg;
        panel.uiType = UIEnum.MapSelectPanel;
        panel.backBtn = backBtn;
        panel.coinText = coinText;
        panel.grid = gridRt;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[MapSelectPanelBuilder] Built {PrefabPath}");
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
