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
/// 预制体外壳由 <c>Tools/UI/Build CodexPanel Prefab</c> 生成。
/// </summary>
public class CodexPanel : UIPageBase
{
    private const int PageSize = 6; // 2 列 × 3 行

    [Header("顶部")]
    public Button backBtn;
    public TextMeshProUGUI coinText;

    [Header("网格(GridLayoutGroup 容器)")]
    public RectTransform grid;

    [Header("分页")]
    public TextMeshProUGUI pageText;
    public Button btnFirst;
    public Button btnPrev;
    public Button btnNext;
    public Button btnLast;

    // 卡片底框/「？」灰/徽标文字色已烘进 CodexCard 预制体;面板只在绑定时按解锁态切徽标底色。
    private static readonly Color BadgeLocked = new Color(0.60f, 0.58f, 0.68f, 1f); // 未解锁徽标底
    private static readonly Color BadgeUnlocked = new Color(0.56f, 0.78f, 0.30f, 1f); // 已解锁名底

    private ConfigManager config;
    private CurrencySystem currency;
    private CodexSystem codex;
    private EventMgr eventMgr;
    private ResMgr resMgr;       // 已解锁卡片用它加载动物模型做 3D 旋转展示
    private TMP_FontAsset font;

    private readonly List<Animal> entries = new List<Animal>(); // 全部动物(按 Id 升序),分页基于它
    private readonly List<WeaponModelPreview> previews = new List<WeaponModelPreview>(); // 每张已解锁卡一个 3D 转台
    private int page;
    private GameObject cardPrefab; // 图鉴卡片预制体(Resources/UI/Codex/CodexCard,CodexCardBuilder 生成)
    private ResourceHandle<GameObject> cardPrefabHandle; // 持模板句柄到页面销毁释放

    public override void OnLoad()
    {
        config = GetService<ConfigManager>();
        currency = GetService<CurrencySystem>();
        codex = GetService<CodexSystem>();
        eventMgr = GetService<EventMgr>();
        resMgr = GetService<ResMgr>();
        if (coinText != null) font = coinText.font; // 复用外壳中文字体给运行时卡片
        resMgr.LoadHandleAsync<GameObject>("UI/Codex/CodexCard", h => // 卡片预制体(异步)
        {
            if (this == null) { h?.Release(); return; }
            cardPrefabHandle = h;
            cardPrefab = h?.Asset;
            if (cardPrefab == null) Debug.LogError("[CodexPanel] 未找到 CodexCard 预制体,请先执行 Tools/UI/Build CodexCard Prefab(或 Build ALL UI Prefabs)。");
            else GoToPage(page); // 模板就绪后补建当前页
        });

        if (backBtn != null) backBtn.onClick.AddListener(CloseSelf);
        CurrencyIcon.Bind(coinText, CurrencyType.Gold); // 资源胶囊图标:运行时从 Resources 动态加载(方便换图)
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
        DisposePreviews(); // 收起时回收所有 3D 转台(相机/RenderTexture)
    }

    private void DisposePreviews()
    {
        foreach (var p in previews) p?.Dispose();
        previews.Clear();
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

    private void OnDestroy() => cardPrefabHandle?.Release(); // 释放卡片模板句柄

    private void RebuildGrid()
    {
        if (grid == null) return;
        DisposePreviews(); // 重建前先回收上一页的转台
        for (int i = grid.childCount - 1; i >= 0; i--) Destroy(grid.GetChild(i).gameObject);

        int start = page * PageSize;
        int end = Mathf.Min(start + PageSize, entries.Count);
        for (int i = start; i < end; i++) BuildCard(entries[i]);
    }

    private void BuildCard(Animal entry)
    {
        if (cardPrefab == null) return;
        bool unlocked = codex != null && codex.IsDiscovered((int)entry.Id);
        var go = Instantiate(cardPrefab);
        go.transform.SetParent(grid, false);
        go.name = $"Card_{entry.Id}";
        var view = go.GetComponent<CodexCardView>();
        if (view == null) { Destroy(go); return; }

        // 上部:已解锁=动物 3D 模型(转台旋转,隐藏「？」);未解锁=显示「？」
        if (unlocked && resMgr != null && !string.IsNullOrEmpty(entry.Prefab))
        {
            view.lockedText.gameObject.SetActive(false);
            var preview = new WeaponModelPreview(view.picHost, resMgr); // 离屏渲染 + 自转,弱点高亮盒已在内部隐藏
            preview.Show(entry.Prefab);
            preview.SetActive(true);
            previews.Add(preview);
        }
        else
        {
            view.lockedText.gameObject.SetActive(true); // 未解锁(或缺模型):只显示问号
        }

        // 底部徽标:已解锁=绿底「积分 X」;未解锁=灰底「未解锁」
        view.badgeBg.color = unlocked ? BadgeUnlocked : BadgeLocked;
        view.badgeLabel.text = unlocked ? $"积分 {entry.Score}" : "未解锁";
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
