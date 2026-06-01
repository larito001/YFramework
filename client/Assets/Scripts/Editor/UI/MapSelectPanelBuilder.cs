#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成选择关卡界面预制体 MapSelectPanel.prefab 到 Resources/UI/Map 下。
/// 外壳:浅紫背景 + 返回 / 资源金币(绿色胶囊) + 「关卡列表」标题 + 竖向滚动的 2 列卡片网格容器。
/// 关卡卡(预览图 + 第N关名称 + 锁罩)由 <see cref="MapSelectPanel"/> 运行时按配表 <c>mapConfig</c> 构建。
///
/// 尺寸按"画布宽恒为 1920 单位"(CanvasScaler match=width)给。
/// 菜单:Tools/UI/Build MapSelectPanel Prefab
/// </summary>
public static class MapSelectPanelBuilder
{
    private const string Dir = "Assets/Resources/UI/Map";
    private const string PrefabPath = Dir + "/MapSelectPanel.prefab";
    private const string ButtonPrefabPath = "Assets/Resources/UI/Common/CommonButton.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";

    private static TMP_FontAsset _font;
    private static GameObject _btnPrefab;

    [MenuItem("Tools/UI/Build MapSelectPanel Prefab")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        _btnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabPath);
        if (_btnPrefab == null)
        {
            Debug.LogError($"[MapSelectPanelBuilder] 未找到通用按钮 {ButtonPrefabPath},无法生成。");
            return;
        }

        // ---------- 根(浅紫背景 + CanvasGroup + YOTOUIShow + MapSelectPanel)----------
        var root = NewUI("MapSelectPanel", out var rootRt);
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
        vpImg.color = new Color(0.86f, 0.84f, 0.93f, 1f);
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

    private static Button BuildButton(string name, string label, Transform parent, float fontSize)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(_btnPrefab, parent);
        go.name = name;
        go.SetActive(true);
        var text = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null) { text.text = label; text.fontSize = fontSize; }
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

    private static TextMeshProUGUI NewChildText(GameObject parent, string name, string text, float size, TextAlignmentOptions align)
    {
        var go = NewUI(name, out var rt, parent.transform);
        Stretch(rt);
        return NewText(go, text, size, align, Color.white);
    }

    private static TextMeshProUGUI NewText(GameObject go, string text, float size, TextAlignmentOptions align, Color color)
    {
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.alignment = align; tmp.color = color; tmp.raycastTarget = false;
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
