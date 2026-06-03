#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 生成图鉴卡片预制体 CodexCard.prefab 到 Resources/UI/Codex 下。<see cref="CodexPanel"/> 运行时 instantiate 它、
/// 取 <see cref="CodexCardView"/> 绑定:解锁→picHost 挂 3D 转台并隐藏问号;未解锁→显示问号。
/// 卡片长相在这里改。尺寸由 GridLayoutGroup 控制(此处给参考值)。菜单:Tools/UI/Build CodexCard Prefab
/// </summary>
public static class CodexCardBuilder
{
    private const string Dir = "Assets/Resources/UI/Codex";
    private const string PrefabPath = Dir + "/CodexCard.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";
    private static TMP_FontAsset _font;

    [MenuItem("Tools/UI/Build CodexCard Prefab")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        var root = NewUI("CodexCard", out var rootRt);
        rootRt.sizeDelta = new Vector2(360, 460); // 参考值,实际由网格 cellSize 控制
        var bg = root.AddComponent<Image>();
        bg.color = new Color(0.97f, 0.97f, 1f, 1f);
        bg.raycastTarget = false;
        var view = root.AddComponent<CodexCardView>();

        // 模型/问号 容器
        var pic = NewUI("Pic", out var picRt, root.transform);
        picRt.anchorMin = Vector2.zero; picRt.anchorMax = Vector2.one;
        picRt.offsetMin = new Vector2(16, 120); picRt.offsetMax = new Vector2(-16, -16);
        // 「？」(未解锁显示;解锁时面板把它 SetActive(false))
        var locked = NewUI("Locked", out var lockedRt, pic.transform);
        Stretch(lockedRt);
        var lockedTmp = NewText(locked, "？", 56, TextAlignmentOptions.Center, new Color(0.62f, 0.62f, 0.66f, 1f));

        // 底部徽标
        var badge = NewUI("Badge", out var badgeRt, root.transform);
        badgeRt.anchorMin = new Vector2(0.5f, 0); badgeRt.anchorMax = new Vector2(0.5f, 0); badgeRt.pivot = new Vector2(0.5f, 0);
        badgeRt.anchoredPosition = new Vector2(0, 24); badgeRt.sizeDelta = new Vector2(280, 76);
        var badgeImg = badge.AddComponent<Image>();
        badgeImg.color = new Color(0.56f, 0.78f, 0.30f, 1f); badgeImg.raycastTarget = false;
        var label = NewUI("Label", out var labelRt, badge.transform);
        labelRt.anchorMin = Vector2.zero; labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(8, 0); labelRt.offsetMax = new Vector2(-8, 0);
        var badgeLabelTmp = NewText(label, "积分", 30, TextAlignmentOptions.Center, Color.white);

        view.bg = bg; view.picHost = picRt; view.lockedText = lockedTmp; view.badgeBg = badgeImg; view.badgeLabel = badgeLabelTmp;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[CodexCardBuilder] Built {PrefabPath}");
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
        tmp.enableWordWrapping = false; tmp.overflowMode = TextOverflowModes.Ellipsis;
        var font = _font != null ? _font : TMP_Settings.defaultFontAsset;
        if (font != null) tmp.font = font;
        return tmp;
    }
}
#endif
