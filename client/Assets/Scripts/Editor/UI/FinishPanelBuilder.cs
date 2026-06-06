#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成打猎结算界面预制体 FinishPanel.prefab 到 Resources/UI/Main 下(覆盖旧占位)。
/// 外壳:半透明遮罩(挡住身后 HUD/场景) + 居中卡片(标题 + 明细滚动列表 + 总积分 + 「返回大厅」)。
/// 逐种击杀明细由 <see cref="FinishPanel"/> 运行时按传入的 <c>HuntResult</c> 构建。
///
/// 尺寸按"画布宽恒为 1920 单位"(CanvasScaler match=width)给。
/// 菜单:Tools/UI/Build FinishPanel Prefab
/// </summary>
public static class FinishPanelBuilder
{
    private const string Dir = "Assets/Resources/UI/Main";
    private const string PrefabPath = Dir + "/FinishPanel.prefab";
    private const string ButtonPrefabPath = "Assets/Resources/UI/Common/CommonButton.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";

    private static TMP_FontAsset _font;
    private static GameObject _btnPrefab;

    [MenuItem("Tools/UI/Build FinishPanel Prefab")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        _btnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabPath);
        if (_btnPrefab == null)
        {
            Debug.LogError($"[FinishPanelBuilder] 未找到通用按钮 {ButtonPrefabPath},无法生成。");
            return;
        }

        // ---------- 根(半透明遮罩,挡住身后并拦截点击)----------
        var root = NewUI("FinishPanel", out var rootRt);
        Stretch(rootRt);
        root.AddComponent<Image>().color = UITheme.Scrim; // 统一弹窗遮罩
        var cg = root.AddComponent<CanvasGroup>();
        root.AddComponent<YOTOUIShow>();

        // ---------- 居中卡片 ----------
        var card = NewUI("Card", out var cardRt, root.transform);
        cardRt.anchorMin = cardRt.anchorMax = cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(1600, 2100); // 放大结算卡片(运行时 FinishPanel 也会同步成这个尺寸)
        var cardBg = card.AddComponent<Image>();
        cardBg.sprite = BuiltinSprite("UI/Skin/UISprite.psd");
        cardBg.type = Image.Type.Sliced;
        cardBg.color = new Color(0.94f, 0.93f, 0.97f, 1f);

        // ---------- 标题(卡片顶部)----------
        var titleGo = NewUI("Title", out var titleRt, card.transform);
        titleRt.anchorMin = new Vector2(0, 1); titleRt.anchorMax = new Vector2(1, 1); titleRt.pivot = new Vector2(0.5f, 1);
        titleRt.anchoredPosition = new Vector2(0, -50); titleRt.sizeDelta = new Vector2(0, 140);
        var titleText = NewText(titleGo, "打猎结算", 76, TextAlignmentOptions.Center, new Color(0.25f, 0.24f, 0.30f, 1f));

        // ---------- 明细滚动列表 ----------
        var scrollGo = NewUI("Scroll", out var scrollRt, card.transform);
        scrollRt.anchorMin = new Vector2(0, 0); scrollRt.anchorMax = new Vector2(1, 1);
        scrollRt.offsetMin = new Vector2(60, 340); scrollRt.offsetMax = new Vector2(-60, -230);
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

        var content = NewUI("Content", out var contentRt, viewport.transform);
        contentRt.anchorMin = new Vector2(0, 1); contentRt.anchorMax = new Vector2(1, 1); contentRt.pivot = new Vector2(0.5f, 1);
        contentRt.anchoredPosition = Vector2.zero; contentRt.sizeDelta = Vector2.zero;
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 16;
        vlg.padding = new RectOffset(20, 20, 20, 20);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
        var csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewportRt;
        scroll.content = contentRt;

        // ---------- 总积分(列表下方)----------
        var totalGo = NewUI("Total", out var totalRt, card.transform);
        totalRt.anchorMin = new Vector2(0, 0); totalRt.anchorMax = new Vector2(1, 0); totalRt.pivot = new Vector2(0.5f, 0);
        totalRt.anchoredPosition = new Vector2(0, 190); totalRt.sizeDelta = new Vector2(-100, 100);
        var totalText = NewText(totalGo, "总积分：0", 52, TextAlignmentOptions.Right, new Color(0.25f, 0.24f, 0.30f, 1f));

        // ---------- 返回大厅按钮(卡片底部)----------
        var confirmBtn = UIButtonFactory.Build(_btnPrefab, "Btn_Confirm", "返回大厅", card.transform, fontSize: 50);
        var confirmRt = (RectTransform)confirmBtn.transform;
        confirmRt.anchorMin = confirmRt.anchorMax = new Vector2(0.5f, 0); confirmRt.pivot = new Vector2(0.5f, 0);
        confirmRt.anchoredPosition = new Vector2(0, 50); confirmRt.sizeDelta = new Vector2(420, 120);

        // ---------- 接脚本字段 ----------
        var panel = root.AddComponent<FinishPanel>();
        panel.canvasGroup = cg;
        panel.uiType = UIEnum.FinishPanel;
        panel.titleText = titleText;
        panel.totalText = totalText;
        panel.content = contentRt;
        panel.confirmBtn = confirmBtn;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[FinishPanelBuilder] Built {PrefabPath}");
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
