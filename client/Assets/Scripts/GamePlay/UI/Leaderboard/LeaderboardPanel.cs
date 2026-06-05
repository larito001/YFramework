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
    private GameObject rowPrefab; // 排行榜行预制体(Resources/UI/Leaderboard/LeaderboardRow,LeaderboardRowBuilder 生成)
    private ResourceHandle<GameObject> rowPrefabHandle; // 持模板句柄到页面销毁释放
    private LeaderboardResult pendingResult; // 榜单数据先到、模板未到时暂存,待模板就绪补渲
    private bool hasResult;

    public override void OnLoad()
    {
        Context.TryGet<ILeaderboardService>(out leaderboard);
        if (statusText != null) font = statusText.font; // 复用外壳字体给运行时行
        var resMgr = GetService<ResMgr>();
        if (resMgr != null)
            resMgr.LoadHandleAsync<GameObject>("UI/Leaderboard/LeaderboardRow", h => // 行预制体(异步)
            {
                if (this == null) { h?.Release(); return; }
                rowPrefabHandle = h;
                rowPrefab = h?.Asset;
                if (rowPrefab == null) Debug.LogError("[LeaderboardPanel] 未找到 LeaderboardRow 预制体,请先执行 Tools/UI/Build LeaderboardRow Prefab(或 Build ALL UI Prefabs)。");
                else if (hasResult) Render(pendingResult); // 模板后到:补渲已到的榜单数据
            });

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

    private void OnDestroy() => rowPrefabHandle?.Release(); // 释放行模板句柄

    private void OnLoaded(LeaderboardResult result)
    {
        if (this == null || content == null) return; // 面板可能已被关闭
        pendingResult = result;
        hasResult = true;
        if (rowPrefab == null) return; // 模板未到:保留"加载中…",待模板就绪在加载回调里补渲
        Render(result);
    }

    private void Render(LeaderboardResult result)
    {
        if (this == null || content == null) return;
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
        if (rowPrefab == null) return;
        var go = Instantiate(rowPrefab);
        go.transform.SetParent(content, false);
        go.name = $"Row_{e.rank}";
        var view = go.GetComponent<LeaderboardRowView>();
        if (view == null) { Destroy(go); return; }

        view.bg.color = e.isSelf ? RowSelf : RowNormal;
        view.rankText.text = e.rank.ToString();
        view.rankText.color = e.rank <= 3 ? RankTop : TextDark; // 前三橙,其余深色
        view.nameText.text = e.isSelf ? $"{e.name}(我)" : e.name;
        view.scoreText.text = e.score.ToString();
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
