using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YFramework.Config;
using YOTO;

/// <summary>
/// 动物图鉴(<see cref="UIEnum.CodexPanel"/>):从主界面「图鉴」进入。只做一种图鉴——动物图鉴。
///   顶部:返回(关闭) / 资源金币 + 已解锁进度
///   中部:2 列网格,每页 6 张卡;**被杀死过的动物**解锁(显示名称 + 击杀积分),未击杀显示「？/未解锁」
///   底部:首页 / 上一页 / 「当前/总页」/ 下一页 / 末页 分页
/// 目录读 animal 配表(<see cref="ConfigManager.animalConfig"/>);解锁集合来自 <see cref="CodexSystem"/>(击杀即发现,随存档槽存档)。
/// 预制体外壳由 <c>Tools/UI/Build CodexPanel Prefab</c> 生成(原装饰公仔/荣誉卡片两个页签已停用并隐藏)。
/// </summary>
public class CodexPanel : UIPageBase
{
    private const int PageSize = 6; // 2 列 × 3 行

    [Header("顶部")]
    public Button backBtn;
    public TextMeshProUGUI coinText;

    [Header("页签(动物图鉴单类,停用并隐藏)")]
    public Button tabDoll;
    public Button tabCard;

    [Header("网格(GridLayoutGroup 容器)")]
    public RectTransform grid;

    [Header("分页")]
    public TextMeshProUGUI pageText;
    public Button btnFirst;
    public Button btnPrev;
    public Button btnNext;
    public Button btnLast;

    private static readonly Color CardFrame = new Color(0.97f, 0.97f, 1f, 1f);     // 卡片底框
    private static readonly Color LockedTint = new Color(0.62f, 0.62f, 0.66f, 1f); // 未解锁:灰
    private static readonly Color UnlockedTint = new Color(0.20f, 0.22f, 0.28f, 1f); // 已解锁:深色名字
    private static readonly Color BadgeLocked = new Color(0.60f, 0.58f, 0.68f, 1f); // 未解锁徽标底
    private static readonly Color BadgeUnlocked = new Color(0.56f, 0.78f, 0.30f, 1f); // 已解锁名底
    private static readonly Color BadgeText = Color.white;

    private ConfigManager config;
    private CurrencySystem currency;
    private CodexSystem codex;
    private EventMgr eventMgr;
    private TMP_FontAsset font;

    private readonly List<Animal> entries = new List<Animal>(); // 全部动物(按 Id 升序),分页基于它
    private int page;

    public override void OnLoad()
    {
        config = GetService<ConfigManager>();
        currency = GetService<CurrencySystem>();
        codex = GetService<CodexSystem>();
        eventMgr = GetService<EventMgr>();
        if (coinText != null) font = coinText.font; // 复用外壳中文字体给运行时卡片

        if (backBtn != null) backBtn.onClick.AddListener(CloseSelf);
        // 动物图鉴只有一类:隐藏原来的两个分类页签
        if (tabDoll != null) tabDoll.gameObject.SetActive(false);
        if (tabCard != null) tabCard.gameObject.SetActive(false);
        if (btnFirst != null) btnFirst.onClick.AddListener(() => GoToPage(0));
        if (btnPrev != null) btnPrev.onClick.AddListener(() => GoToPage(page - 1));
        if (btnNext != null) btnNext.onClick.AddListener(() => GoToPage(page + 1));
        if (btnLast != null) btnLast.onClick.AddListener(() => GoToPage(int.MaxValue));
    }

    public override void OnShow()
    {
        eventMgr?.Add(YOTOEventType.RefreshCurrency, RefreshCoin);
        eventMgr?.Add(YOTOEventType.RefreshCodex, OnCodexChanged);
        ReloadEntries();
        GoToPage(0);
        RefreshCoin();
    }

    public override void OnHide()
    {
        eventMgr?.Remove(YOTOEventType.RefreshCurrency, RefreshCoin);
        eventMgr?.Remove(YOTOEventType.RefreshCodex, OnCodexChanged);
    }

    public override void OnResize() { }

    private void OnCodexChanged()
    {
        ReloadEntries();
        GoToPage(page); // 保持当前页刷新解锁态
    }

    // ---------------- 顶部 ----------------

    private void RefreshCoin()
    {
        if (coinText == null) return;
        string gold = currency != null ? currency.Get(CurrencyType.Gold).ToString() : "0";
        int unlocked = codex != null ? codex.DiscoveredCount : 0;
        coinText.text = $"{gold}   图鉴 {unlocked}/{entries.Count}";
    }

    /// <summary>拉取全部动物,按 Id 升序。</summary>
    private void ReloadEntries()
    {
        entries.Clear();
        if (config == null) return;
        foreach (var kv in config.animalConfig.items)
        {
            if (kv.Value != null) entries.Add(kv.Value);
        }
        entries.Sort((a, b) => a.Id.CompareTo(b.Id));
    }

    // ---------------- 分页 ----------------

    private int PageCount => Mathf.Max(1, Mathf.CeilToInt(entries.Count / (float)PageSize));

    private void GoToPage(int target)
    {
        page = Mathf.Clamp(target, 0, PageCount - 1);
        RebuildGrid();
        RefreshPager();
        RefreshCoin(); // 顺带更新「图鉴 x/y」
    }

    private void RefreshPager()
    {
        if (pageText != null) pageText.text = $"{page + 1} / {PageCount}";
        bool hasPrev = page > 0;
        bool hasNext = page < PageCount - 1;
        if (btnFirst != null) btnFirst.interactable = hasPrev;
        if (btnPrev != null) btnPrev.interactable = hasPrev;
        if (btnNext != null) btnNext.interactable = hasNext;
        if (btnLast != null) btnLast.interactable = hasNext;
    }

    // ---------------- 卡片网格 ----------------

    private void RebuildGrid()
    {
        if (grid == null) return;
        for (int i = grid.childCount - 1; i >= 0; i--) Destroy(grid.GetChild(i).gameObject);

        int start = page * PageSize;
        int end = Mathf.Min(start + PageSize, entries.Count);
        for (int i = start; i < end; i++) BuildCard(entries[i]);
    }

    private void BuildCard(Animal entry)
    {
        bool unlocked = codex != null && codex.IsDiscovered((int)entry.Id);

        // 卡片底框(尺寸由 GridLayoutGroup 决定)
        var card = new GameObject($"Card_{entry.Id}", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(grid, false);
        var bg = card.GetComponent<Image>();
        bg.color = CardFrame;
        bg.raycastTarget = false;

        // 上部:已解锁显示动物名,未解锁显示「？」(项目无 2D 动物图标,用文字表现)
        var picGo = NewChild(card.transform, "Pic", out var picRt);
        picRt.anchorMin = Vector2.zero; picRt.anchorMax = Vector2.one;
        picRt.offsetMin = new Vector2(16, 120); picRt.offsetMax = new Vector2(-16, -16);
        NewText(picGo, unlocked ? entry.Name : "？", 56, unlocked ? UnlockedTint : LockedTint);

        // 底部徽标:已解锁=绿底「积分 X」;未解锁=灰底「未解锁」
        var badge = NewChild(card.transform, "Badge", out var badgeRt);
        badgeRt.anchorMin = new Vector2(0.5f, 0); badgeRt.anchorMax = new Vector2(0.5f, 0); badgeRt.pivot = new Vector2(0.5f, 0);
        badgeRt.anchoredPosition = new Vector2(0, 24); badgeRt.sizeDelta = new Vector2(280, 76);
        var badgeImg = badge.AddComponent<Image>();
        badgeImg.color = unlocked ? BadgeUnlocked : BadgeLocked;
        badgeImg.raycastTarget = false;

        var label = NewChild(badge.transform, "Label", out var labelRt);
        labelRt.anchorMin = Vector2.zero; labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(8, 0); labelRt.offsetMax = new Vector2(-8, 0);
        NewText(label, unlocked ? $"积分 {entry.Score}" : "未解锁", 30, BadgeText);
    }

    // ---------------- 工具 ----------------

    private GameObject NewChild(Transform parent, string name, out RectTransform rt)
    {
        var go = new GameObject(name, typeof(RectTransform));
        rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return go;
    }

    private void NewText(GameObject go, string text, float size, Color color)
    {
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = UITheme.Font(size);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        var f = font != null ? font : TMP_Settings.defaultFontAsset;
        if (f != null) tmp.font = f;
    }
}
