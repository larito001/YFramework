#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 生成排行榜行预制体 LeaderboardRow.prefab 到 Resources/UI/Leaderboard 下。<see cref="LeaderboardPanel"/> 运行时
/// instantiate + 绑定(<see cref="LeaderboardRowView"/>)。行长相在这里改。
/// 高 120(LayoutElement),宽由 VerticalLayoutGroup 撑满。菜单:Tools/UI/Build LeaderboardRow Prefab
/// </summary>
public static class LeaderboardRowBuilder
{
    private const string Dir = "Assets/Resources/UI/Leaderboard";
    private const string PrefabPath = Dir + "/LeaderboardRow.prefab";
    private const string FontPath = "Assets/Art/Fonts/SIMHEI SDF.asset";
    private static TMP_FontAsset _font;

    private static readonly Color RowNormal = new Color(0.90f, 0.89f, 0.93f, 1f);
    private static readonly Color TextDark = new Color(0.22f, 0.22f, 0.28f, 1f);

    [MenuItem("Tools/UI/Build LeaderboardRow Prefab")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        var root = NewUI("LeaderboardRow", out var rootRt);
        rootRt.sizeDelta = new Vector2(900, 120);
        var bg = root.AddComponent<Image>();
        bg.color = RowNormal;
        var le = root.AddComponent<LayoutElement>();
        le.preferredHeight = 120;
        var view = root.AddComponent<LeaderboardRowView>();

        // 名次(左)
        var rank = NewUI("Rank", out var rankRt, root.transform);
        rankRt.anchorMin = new Vector2(0, 0); rankRt.anchorMax = new Vector2(0, 1); rankRt.pivot = new Vector2(0, 0.5f);
        rankRt.anchoredPosition = new Vector2(24, 0); rankRt.sizeDelta = new Vector2(150, 0);
        var rankTmp = NewText(rank, "1", 48, TextDark, TextAlignmentOptions.Center);

        // 昵称(中,左对齐,过长省略)
        var name = NewUI("Name", out var nameRt, root.transform);
        nameRt.anchorMin = new Vector2(0, 0); nameRt.anchorMax = new Vector2(1, 1); nameRt.pivot = new Vector2(0, 0.5f);
        nameRt.offsetMin = new Vector2(190, 0); nameRt.offsetMax = new Vector2(-260, 0);
        var nameTmp = NewText(name, "昵称", 42, TextDark, TextAlignmentOptions.Left);
        nameTmp.overflowMode = TextOverflowModes.Ellipsis;

        // 分数(右)
        var score = NewUI("Score", out var scoreRt, root.transform);
        scoreRt.anchorMin = new Vector2(1, 0); scoreRt.anchorMax = new Vector2(1, 1); scoreRt.pivot = new Vector2(1, 0.5f);
        scoreRt.anchoredPosition = new Vector2(-24, 0); scoreRt.sizeDelta = new Vector2(240, 0);
        var scoreTmp = NewText(score, "0", 44, TextDark, TextAlignmentOptions.Right);

        view.bg = bg; view.rankText = rankTmp; view.nameText = nameTmp; view.scoreText = scoreTmp;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[LeaderboardRowBuilder] Built {PrefabPath}");
    }

    // ============================ 工具 ============================

    private static GameObject NewUI(string name, out RectTransform rt, Transform parent = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        rt = go.GetComponent<RectTransform>();
        if (parent != null) rt.SetParent(parent, false);
        return go;
    }

    private static TextMeshProUGUI NewText(GameObject go, string text, float size, Color color, TextAlignmentOptions align)
    {
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = UITheme.Font(size); tmp.alignment = align; tmp.color = color; tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        var font = _font != null ? _font : TMP_Settings.defaultFontAsset;
        if (font != null) tmp.font = font;
        return tmp;
    }
}
#endif
