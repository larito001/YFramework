using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YFramework.Config;
using YOTO;

/// <summary>
/// 图鉴(<see cref="UIEnum.CodexPanel"/>):收藏品图鉴,从主界面「图鉴」进入。
///   顶部:返回(关闭) / 资源金币
///   页签:装饰公仔 / 荣誉卡片(绿=当前分类)
///   中部:2 列网格,每页 6 张卡(图片 + 解锁状态);未解锁显示灰图 +「未解锁」徽标
///   底部:首页 / 上一页 / 「当前/总页」/ 下一页 / 末页 分页
/// 目录走配表系统:条目读 <see cref="ConfigManager.codexConfig"/>(codex.xlsx → Codex.bytes),按 Category 分两类。
/// 解锁状态暂为占位(全部未锁,对齐设计稿);接入存档后由玩家进度驱动。
/// 预制体外壳由 <c>Tools/UI/Build CodexPanel Prefab</c> 生成。
/// </summary>
public class CodexPanel : UIPageBase
{
    // 配表 category 取值:与 codex.xlsx 第 3 列约定一致
    private const uint CategoryDoll = 1; // 装饰公仔
    private const uint CategoryCard = 2; // 荣誉卡片
    private const int PageSize = 6;      // 2 列 × 3 行

    [Header("顶部")]
    public Button backBtn;
    public TextMeshProUGUI coinText;

    [Header("页签")]
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

    private static readonly Color TabOn = new Color(0.56f, 0.78f, 0.30f, 1f);     // 绿:当前分类
    private static readonly Color TabOff = new Color(0.72f, 0.70f, 0.80f, 1f);    // 灰紫:未选
    private static readonly Color CardFrame = new Color(0.97f, 0.97f, 1f, 1f);    // 卡片底框
    private static readonly Color LockedTint = new Color(0.62f, 0.62f, 0.66f, 1f); // 未解锁:灰
    private static readonly Color UnlockedTint = Color.white;                     // 已解锁:原色
    private static readonly Color BadgeLocked = new Color(0.60f, 0.58f, 0.68f, 1f); // 未解锁徽标底
    private static readonly Color BadgeUnlocked = new Color(0.56f, 0.78f, 0.30f, 1f); // 已解锁名底
    private static readonly Color BadgeText = Color.white;

    private ConfigManager config;
    private CurrencySystem currency;
    private ResMgr resMgr;
    private EventMgr eventMgr;
    private TMP_FontAsset font;

    // 当前分类的条目(按 SortPriority 排好序),分页基于它。
    private readonly List<Codex> entries = new List<Codex>();
    private uint currentCategory = CategoryCard; // 默认荣誉卡片,对齐设计稿
    private int page;

    public override void OnLoad()
    {
        config = GetService<ConfigManager>();
        currency = GetService<CurrencySystem>();
        resMgr = GetService<ResMgr>();
        eventMgr = GetService<EventMgr>();
        if (coinText != null) font = coinText.font; // 复用外壳中文字体给运行时卡片

        if (backBtn != null) backBtn.onClick.AddListener(CloseSelf);
        if (tabDoll != null) tabDoll.onClick.AddListener(() => SelectCategory(CategoryDoll));
        if (tabCard != null) tabCard.onClick.AddListener(() => SelectCategory(CategoryCard));
        if (btnFirst != null) btnFirst.onClick.AddListener(() => GoToPage(0));
        if (btnPrev != null) btnPrev.onClick.AddListener(() => GoToPage(page - 1));
        if (btnNext != null) btnNext.onClick.AddListener(() => GoToPage(page + 1));
        if (btnLast != null) btnLast.onClick.AddListener(() => GoToPage(int.MaxValue));
    }

    public override void OnShow()
    {
        eventMgr?.Add(YOTOEventType.RefreshCurrency, RefreshCoin);
        SelectCategory(currentCategory);
        RefreshCoin();
    }

    public override void OnHide()
    {
        eventMgr?.Remove(YOTOEventType.RefreshCurrency, RefreshCoin);
    }

    public override void OnResize() { }

    // ---------------- 顶部 ----------------

    private void RefreshCoin()
    {
        if (coinText == null || currency == null) return;
        coinText.text = currency.Get(CurrencyType.Gold).ToString();
    }

    // ---------------- 页签 ----------------

    private void SelectCategory(uint category)
    {
        currentCategory = category;
        SetTabColor(tabDoll, category == CategoryDoll);
        SetTabColor(tabCard, category == CategoryCard);
        ReloadEntries();
        GoToPage(0);
    }

    private static void SetTabColor(Button btn, bool on)
    {
        if (btn == null) return;
        var img = btn.targetGraphic as Image;
        if (img != null) img.color = on ? TabOn : TabOff;
    }

    /// <summary>从配表拉取当前分类条目,按 SortPriority 升序。</summary>
    private void ReloadEntries()
    {
        entries.Clear();
        if (config == null) return;
        foreach (var kv in config.codexConfig.items)
        {
            if (kv.Value.Category == currentCategory) entries.Add(kv.Value);
        }
        entries.Sort((a, b) => a.SortPriority.CompareTo(b.SortPriority));
    }

    // ---------------- 分页 ----------------

    private int PageCount => Mathf.Max(1, Mathf.CeilToInt(entries.Count / (float)PageSize));

    private void GoToPage(int target)
    {
        page = Mathf.Clamp(target, 0, PageCount - 1);
        RebuildGrid();
        RefreshPager();
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

    private void BuildCard(Codex entry)
    {
        bool unlocked = IsUnlocked(entry.Id);

        // 卡片底框(尺寸由 GridLayoutGroup 决定)
        var card = new GameObject($"Card_{entry.Id}", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(grid, false);
        var bg = card.GetComponent<Image>();
        bg.color = CardFrame;
        bg.raycastTarget = false;

        // 图片(上部,留出底部徽标)
        var pic = NewChild(card.transform, "Pic", out var picRt);
        picRt.anchorMin = Vector2.zero; picRt.anchorMax = Vector2.one;
        picRt.offsetMin = new Vector2(20, 120); picRt.offsetMax = new Vector2(-20, -20);
        var picImg = pic.AddComponent<Image>();
        picImg.raycastTarget = false; picImg.preserveAspect = true;
        picImg.color = unlocked ? UnlockedTint : LockedTint; // 未解锁:灰
        var sprite = !string.IsNullOrEmpty(entry.IconPath) ? resMgr.Load<Sprite>(entry.IconPath) : null;
        picImg.sprite = sprite; picImg.enabled = sprite != null;

        // 底部徽标:未解锁=灰底「未解锁」;已解锁=绿底显示名称
        var badge = NewChild(card.transform, "Badge", out var badgeRt);
        badgeRt.anchorMin = new Vector2(0.5f, 0); badgeRt.anchorMax = new Vector2(0.5f, 0); badgeRt.pivot = new Vector2(0.5f, 0);
        badgeRt.anchoredPosition = new Vector2(0, 24); badgeRt.sizeDelta = new Vector2(280, 76);
        var badgeImg = badge.AddComponent<Image>();
        badgeImg.color = unlocked ? BadgeUnlocked : BadgeLocked;
        badgeImg.raycastTarget = false;

        var label = NewChild(badge.transform, "Label", out var labelRt);
        labelRt.anchorMin = Vector2.zero; labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(8, 0); labelRt.offsetMax = new Vector2(-8, 0);
        NewText(label, unlocked ? entry.Name : "未解锁", 34, BadgeText);
    }

    /// <summary>
    /// 解锁状态:暂全部未解锁(对齐设计稿)。接入存档进度后,改为读玩家已解锁集合
    /// (例如 StoreMgr 的图鉴解锁数据)。
    /// </summary>
    private bool IsUnlocked(uint id) => false;

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
