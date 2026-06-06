#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成通用奖励领取弹窗预制体 RewardClaimPanel.prefab 到 Resources/UI/Common 下。
/// 结构:全屏轻遮罩(不挡点击)+ 居中窗口(美术整窗背景图 getReward.png,标题「恭喜获得」已烤进图)
/// + 透明 Title(仅作字体来源/绑定占位)+ 横排奖励容器(落在背景图白色内容框,HorizontalLayoutGroup,运行时填格)。
/// 奖励格(图标 + ×数量 + 名称)由 <see cref="RewardClaimPanel"/> 运行时按 RewardClaimParam 构建。
///
/// 菜单:Tools/UI/Build RewardClaimPanel Prefab
/// </summary>
public static class RewardClaimPanelBuilder
{
    private const string Dir = "Assets/Resources/UI/Common";
    private const string PrefabPath = Dir + "/RewardClaimPanel.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";
    private const string WindowSpritePath = "Assets/Art/UI/NewUI/Theme_Blue/Sprites/getReward.png"; // 整窗背景(标题「恭喜获得」+ 白色内容框已烤进图)
    private const string GoldIconPath = "Assets/Art/UI/NewUI/Theme_Blue/Sprites/getCoin.png";   // 金币奖励整图
    private const string EnergyIconPath = "Assets/Art/UI/NewUI/Theme_Blue/Sprites/getHeart.png"; // 体力奖励整图

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

        // 窗口(居中,美术整窗背景图;尺寸匹配 getReward.png 比例 1024×1536 → ×1.25)
        var window = NewUI("Window", out var winRt, root.transform);
        winRt.anchorMin = winRt.anchorMax = winRt.pivot = new Vector2(0.5f, 0.5f);
        winRt.sizeDelta = new Vector2(1280, 1920);
        var winImg = window.AddComponent<Image>();
        winImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WindowSpritePath);
        winImg.type = Image.Type.Simple; winImg.preserveAspect = true;
        winImg.color = Color.white;
        winImg.raycastTarget = false;

        // 标题已烤进背景图(「恭喜获得」)。这里保留一个透明 Title:仅作运行时字体来源 + 兼容 titleText 绑定,不重复显示。
        var titleGo = NewUI("Title", out var titleRt, window.transform);
        titleRt.anchorMin = new Vector2(0, 1); titleRt.anchorMax = new Vector2(1, 1); titleRt.pivot = new Vector2(0.5f, 1);
        titleRt.anchoredPosition = new Vector2(0, -30); titleRt.sizeDelta = new Vector2(-60, 110);
        var titleText = NewText(titleGo, "获得奖励", 60, TextAlignmentOptions.Center);
        titleText.color = new Color(1, 1, 1, 0); // 透明:标题由背景图提供,避免与烤入文字重叠

        // 奖励容器(落进背景图的白色内容框;横排居中,运行时填入奖励格)
        var containerGo = NewUI("RewardContainer", out var containerRt, window.transform);
        containerRt.anchorMin = containerRt.anchorMax = containerRt.pivot = new Vector2(0.5f, 0.5f);
        containerRt.anchoredPosition = new Vector2(0, -184); containerRt.sizeDelta = new Vector2(1080, 520);
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
        // 金币/体力奖励整图直接接入(getCoin/getHeart 在 Art 下,非 Resources,故烘入引用);其它币种仍运行时按配表 IconPath 加载
        panel.goldRewardIcon = AssetDatabase.LoadAssetAtPath<Sprite>(GoldIconPath);
        panel.energyRewardIcon = AssetDatabase.LoadAssetAtPath<Sprite>(EnergyIconPath);

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
