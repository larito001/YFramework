using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YOTO;

/// <summary>
/// 排行榜界面(<see cref="UIEnum.LeaderboardPanel"/>):从主界面「排行榜」进入。
///   顶部:返回(关闭) + 标题「排行榜」
///   中部:竖向滚动榜单,每行 = 名次 + 昵称 + 分数;当前玩家行高亮(金底)。
///        若自己未进前 N,会在列表末尾追加一条"我的排名"高亮行。
/// 数据来自 <see cref="ILeaderboardService"/>(TapTap 排行榜;编辑器无 SDK 时走模拟榜单)。
/// 打开时优先尝试调起渠道内置排行榜原生界面;不可用(编辑器/未接入)则用本自绘列表兜底。
/// 预制体外壳由 <c>Tools/UI/Build LeaderboardPanel Prefab</c> 生成。
/// </summary>
public class LeaderboardPanel : UIPageBase
{
    private const int TopCount = 50; // 拉取/展示的名次上限

    [Header("顶部")]
    public Button backBtn;

    [Header("列表(ScrollRect 的 VerticalLayoutGroup 容器)")]
    public RectTransform content;

    [Header("状态(加载中/空榜/未接入 提示,居中)")]
    public TextMeshProUGUI statusText;

    private static readonly Color RowNormal = new Color(0.90f, 0.89f, 0.93f, 1f);  // 普通行底
    private static readonly Color RowSelf = new Color(0.98f, 0.84f, 0.36f, 1f);    // 自己行:金底高亮
    private static readonly Color RankTop = new Color(0.95f, 0.55f, 0.15f, 1f);    // 前三名次:橙
    private static readonly Color TextDark = new Color(0.22f, 0.22f, 0.28f, 1f);

    private ILeaderboardService leaderboard;
    private TMP_FontAsset font;

    public override void OnLoad()
    {
        Context.TryGet<ILeaderboardService>(out leaderboard);
        if (statusText != null) font = statusText.font; // 复用外壳字体给运行时行

        if (backBtn != null) backBtn.onClick.AddListener(CloseSelf);
    }

    public override void OnShow()
    {
        // 优先调起渠道内置排行榜原生界面;真机+SDK 可用时直接关掉本面板(交给原生 UI)。
        if (leaderboard != null && leaderboard.TryOpenNative())
        {
            CloseSelf();
            return;
        }

        ClearList();
        SetStatus("加载中…");
        if (leaderboard == null) { SetStatus("排行榜未接入"); return; }
        leaderboard.LoadTop(TopCount, OnLoaded);
    }

    public override void OnHide() { }
    public override void OnResize() { }

    private void OnLoaded(LeaderboardResult result)
    {
        if (this == null || content == null) return; // 面板可能已被关闭
        ClearList();

        if (result == null || !result.success)
        {
            SetStatus(result?.error != null ? $"加载失败:{result.error}" : "加载失败");
            return;
        }
        if (result.entries.Count == 0)
        {
            SetStatus("暂无排行数据");
            return;
        }

        SetStatus(null); // 隐藏状态提示
        bool selfShown = false;
        foreach (var e in result.entries)
        {
            BuildRow(e);
            if (e.isSelf) selfShown = true;
        }

        // 自己未进前 N:列表末尾补一条"我的排名"高亮行。
        if (!selfShown && result.self != null) BuildRow(result.self);
    }

    // ---------------- 行 ----------------

    private void BuildRow(LeaderboardEntry e)
    {
        var row = NewChild(content, $"Row_{e.rank}", out _);
        var bg = row.AddComponent<Image>();
        bg.color = e.isSelf ? RowSelf : RowNormal;
        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 120;

        // 名次(左)
        var rankGo = NewChild(row.transform, "Rank", out var rankRt);
        rankRt.anchorMin = new Vector2(0, 0); rankRt.anchorMax = new Vector2(0, 1); rankRt.pivot = new Vector2(0, 0.5f);
        rankRt.anchoredPosition = new Vector2(24, 0); rankRt.sizeDelta = new Vector2(150, 0);
        var rankColor = e.rank <= 3 ? RankTop : TextDark;
        NewText(rankGo, e.rank.ToString(), 48, rankColor, TextAlignmentOptions.Center);

        // 昵称(中,左对齐,过长省略)
        var nameGo = NewChild(row.transform, "Name", out var nameRt);
        nameRt.anchorMin = new Vector2(0, 0); nameRt.anchorMax = new Vector2(1, 1); nameRt.pivot = new Vector2(0, 0.5f);
        nameRt.offsetMin = new Vector2(190, 0); nameRt.offsetMax = new Vector2(-260, 0);
        var nameText = NewText(nameGo, e.isSelf ? $"{e.name}(我)" : e.name, 42, TextDark, TextAlignmentOptions.Left);
        nameText.overflowMode = TextOverflowModes.Ellipsis;

        // 分数(右)
        var scoreGo = NewChild(row.transform, "Score", out var scoreRt);
        scoreRt.anchorMin = new Vector2(1, 0); scoreRt.anchorMax = new Vector2(1, 1); scoreRt.pivot = new Vector2(1, 0.5f);
        scoreRt.anchoredPosition = new Vector2(-24, 0); scoreRt.sizeDelta = new Vector2(240, 0);
        NewText(scoreGo, e.score.ToString(), 44, TextDark, TextAlignmentOptions.Right);
    }

    private void ClearList()
    {
        if (content == null) return;
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
    }

    private void SetStatus(string msg)
    {
        if (statusText == null) return;
        statusText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
        statusText.text = msg ?? string.Empty;
    }

    // ---------------- 工具 ----------------

    private GameObject NewChild(Transform parent, string name, out RectTransform rt)
    {
        var go = new GameObject(name, typeof(RectTransform));
        rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return go;
    }

    private TextMeshProUGUI NewText(GameObject go, string text, float size, Color color, TextAlignmentOptions align)
    {
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = UITheme.Font(size); tmp.alignment = align; tmp.color = color;
        tmp.raycastTarget = false; tmp.enableWordWrapping = false;
        var f = font != null ? font : TMP_Settings.defaultFontAsset;
        if (f != null) tmp.font = f;
        return tmp;
    }
}
