#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成通用奖励领取弹窗预制体 RewardClaimPanel.prefab 到 Resources/UI/Common 下。
/// 结构:全屏轻遮罩(不挡点击)+ 居中窗口 + 标题 + 横排奖励容器(HorizontalLayoutGroup,运行时填格)。
/// 奖励格(图标 + ×数量 + 名称)由 <see cref="RewardClaimPanel"/> 运行时按 RewardClaimParam 构建。
///
/// 菜单:Tools/UI/Build RewardClaimPanel Prefab
/// </summary>
public static class RewardClaimPanelBuilder
{
    private const string Dir = "Assets/Resources/UI/Common";
    private const string PrefabPath = Dir + "/RewardClaimPanel.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";

    private static TMP_FontAsset _font;

    [MenuItem("Tools/UI/Build RewardClaimPanel Prefab")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        // 根:全屏,极轻遮罩且不挡点击(toast 式弹窗,1 秒自动消失,不应拦住下层操作)。
        var root = NewUI("RewardClaimPanel", out var rootRt);
        Stretch(rootRt);
        var rootImg = root.AddComponent<Image>();
        rootImg.color = new Color(0f, 0f, 0f, 0.25f);
        rootImg.raycastTarget = false;
        var cg = root.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false; cg.interactable = false;
        root.AddComponent<YOTOUIShow>();

        // 窗口(居中,大小同设置窗口 1400×1900)
        var window = NewUI("Window", out var winRt, root.transform);
        winRt.anchorMin = winRt.anchorMax = winRt.pivot = new Vector2(0.5f, 0.5f);
        winRt.sizeDelta = new Vector2(1400, 1900);
        var winImg = window.AddComponent<Image>();
        winImg.color = new Color(0.13f, 0.14f, 0.17f, 0.97f);
        winImg.raycastTarget = false;

        // 标题(顶部)
        var titleGo = NewUI("Title", out var titleRt, window.transform);
        titleRt.anchorMin = new Vector2(0, 1); titleRt.anchorMax = new Vector2(1, 1); titleRt.pivot = new Vector2(0.5f, 1);
        titleRt.anchoredPosition = new Vector2(0, -30); titleRt.sizeDelta = new Vector2(-60, 110);
        var titleText = NewText(titleGo, "获得奖励", 60, TextAlignmentOptions.Center);

        // 奖励容器(标题下方,横排居中;运行时由面板填入奖励格,左→右排列)
        var containerGo = NewUI("RewardContainer", out var containerRt, window.transform);
        containerRt.anchorMin = new Vector2(0, 0); containerRt.anchorMax = new Vector2(1, 1);
        containerRt.offsetMin = new Vector2(40, 40); containerRt.offsetMax = new Vector2(-40, -170);
        var hlg = containerGo.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 20;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

        // 接脚本字段
        var panel = root.AddComponent<RewardClaimPanel>();
        panel.canvasGroup = cg;
        panel.uiType = UIEnum.RewardClaimPanel;
        panel.titleText = titleText;
        panel.rewardContainer = containerRt;
        // 货币图标烘入(与各面板资源胶囊同款,Art 目录、非 Resources → 走序列化引用,运行时直接用)。
        panel.goldIcon = AssetDatabase.LoadAssetAtPath<Sprite>(UICurrencyPill.IconGold);
        panel.energyIcon = AssetDatabase.LoadAssetAtPath<Sprite>(UICurrencyPill.IconEnergy);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[RewardClaimPanelBuilder] Built {PrefabPath}");
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

    private static TextMeshProUGUI NewText(GameObject go, string text, float size, TextAlignmentOptions align)
    {
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = UITheme.Font(size);
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        var font = _font != null ? _font : TMP_Settings.defaultFontAsset;
        if (font != null) tmp.font = font;
        return tmp;
    }
}
#endif
