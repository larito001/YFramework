using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YFramework.Config;
using YOTO;

/// <summary>
/// 任务界面(<see cref="UIEnum.TaskPanel"/>):从主界面「任务」进入。
///   顶部:返回(关闭) / 资源金币
///   页签:每日任务 / 常规任务(绿=当前分类)
///   中部:可滚动任务卡列表,每张卡 = 标题 + 进度(当前/目标) + 奖励(物品图标×数量 / 金币×数量) +「前往」按钮
/// 目录走配表系统:任务读 <see cref="ConfigManager.taskConfig"/>(task.xlsx → Task.bytes),按 category 分两类;
/// 奖励物品图标/名称查 <see cref="ConfigManager.itemConfig"/>。
/// 当前进度暂为占位 0(对齐设计稿);接入任务进度系统后由 <see cref="CurrentProgress"/> 一处替换即可。
/// 预制体外壳由 <c>Tools/UI/Build TaskPanel Prefab</c> 生成。
/// </summary>
public class TaskPanel : UIPageBase
{
    // 配表 category 取值:与 task.xlsx 第 3 列约定一致
    private const uint CategoryDaily = 1;   // 每日任务
    private const uint CategoryRegular = 2; // 常规任务

    [Header("顶部")]
    public Button backBtn;
    public TextMeshProUGUI coinText;

    [Header("页签")]
    public Button tabDaily;
    public Button tabRegular;

    [Header("列表(ScrollRect 的 VerticalLayoutGroup 容器)")]
    public RectTransform content;

    private static readonly Color TabOn = new Color(0.56f, 0.78f, 0.30f, 1f);      // 绿:当前分类
    private static readonly Color TabOff = new Color(0.72f, 0.70f, 0.80f, 1f);     // 灰紫:未选
    private static readonly Color CardFrame = new Color(0.90f, 0.89f, 0.93f, 1f);  // 卡片底框
    private static readonly Color IconBox = new Color(0.78f, 0.80f, 0.86f, 1f);    // 物品图标缺失时的占位底
    private static readonly Color CoinIcon = new Color(0.45f, 0.74f, 0.36f, 1f);   // 金币图标(绿色块占位)
    private static readonly Color GotoBtn = new Color(0.96f, 0.66f, 0.18f, 1f);    // 「前往」橙钮
    private static readonly Color TitleText = new Color(0.25f, 0.24f, 0.30f, 1f);

    private ConfigManager config;
    private CurrencySystem currency;
    private ResMgr resMgr;
    private EventMgr eventMgr;
    private TMP_FontAsset font;

    private readonly List<Task> entries = new List<Task>();
    private uint currentCategory = CategoryDaily;

    public override void OnLoad()
    {
        config = GetService<ConfigManager>();
        currency = GetService<CurrencySystem>();
        resMgr = GetService<ResMgr>();
        eventMgr = GetService<EventMgr>();
        if (coinText != null) font = coinText.font; // 复用外壳字体给运行时卡片

        if (backBtn != null) backBtn.onClick.AddListener(CloseSelf);
        if (tabDaily != null) tabDaily.onClick.AddListener(() => SelectCategory(CategoryDaily));
        if (tabRegular != null) tabRegular.onClick.AddListener(() => SelectCategory(CategoryRegular));
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
        SetTabColor(tabDaily, category == CategoryDaily);
        SetTabColor(tabRegular, category == CategoryRegular);
        ReloadEntries();
        RebuildList();
    }

    private static void SetTabColor(Button btn, bool on)
    {
        if (btn == null) return;
        if (btn.targetGraphic is Image img) img.color = on ? TabOn : TabOff;
    }

    /// <summary>从配表拉取当前分类任务,按 SortPriority 升序。</summary>
    private void ReloadEntries()
    {
        entries.Clear();
        if (config == null) return;
        foreach (var kv in config.taskConfig.items)
            if (kv.Value.Category == currentCategory) entries.Add(kv.Value);
        entries.Sort((a, b) => a.SortPriority.CompareTo(b.SortPriority));
    }

    /// <summary>
    /// 当前进度:暂为占位 0(对齐设计稿)。接入任务进度系统后,这里改为读玩家进度
    /// (例如某个 TaskProgress service 的 GetProgress(taskId))。
    /// </summary>
    private int CurrentProgress(uint taskId) => 0;

    // ---------------- 列表 ----------------

    private void RebuildList()
    {
        if (content == null) return;
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
        foreach (var task in entries) BuildCard(task);
    }

    private void BuildCard(Task task)
    {
        // 卡片底框(高度固定,宽度由 VerticalLayoutGroup 撑满)
        var card = NewChild(content, $"Task_{task.Id}", out var cardRt);
        var bg = card.AddComponent<Image>();
        bg.color = CardFrame;
        var le = card.AddComponent<LayoutElement>();
        le.preferredHeight = 240;

        // 标题(左上)
        var title = NewChild(card.transform, "Title", out var titleRt);
        titleRt.anchorMin = new Vector2(0, 1); titleRt.anchorMax = new Vector2(0, 1); titleRt.pivot = new Vector2(0, 1);
        titleRt.anchoredPosition = new Vector2(40, -28); titleRt.sizeDelta = new Vector2(560, 64);
        NewText(title, task.Name, 46, TitleText, TextAlignmentOptions.Left);

        // 进度(右上):进度 cur/need
        var prog = NewChild(card.transform, "Progress", out var progRt);
        progRt.anchorMin = new Vector2(1, 1); progRt.anchorMax = new Vector2(1, 1); progRt.pivot = new Vector2(1, 1);
        progRt.anchoredPosition = new Vector2(-260, -28); progRt.sizeDelta = new Vector2(360, 64);
        var progText = NewText(prog, "", 40, TitleText, TextAlignmentOptions.Right);
        progText.text = $"进度 <color=#D94C40>{CurrentProgress(task.Id)}</color> / {task.TargetAmount}";

        // 奖励(左下):物品图标×数量,金币图标×数量
        float x = 40;
        x = BuildReward(card.transform, ItemIcon(task.RewardItemId), IconBox, task.RewardItemCount, x);
        if (task.RewardCoin > 0)
            BuildReward(card.transform, null, CoinIcon, task.RewardCoin, x);

        // 「前往」按钮(右下)
        var go = NewChild(card.transform, "Goto", out var gotoRt);
        gotoRt.anchorMin = new Vector2(1, 0); gotoRt.anchorMax = new Vector2(1, 0); gotoRt.pivot = new Vector2(1, 0);
        gotoRt.anchoredPosition = new Vector2(-40, 40); gotoRt.sizeDelta = new Vector2(200, 96);
        var gotoImg = go.AddComponent<Image>();
        gotoImg.color = GotoBtn;
        var gotoBtn = go.AddComponent<Button>();
        gotoBtn.targetGraphic = gotoImg;
        gotoBtn.onClick.AddListener(() => OnGoto(task));
        var gotoLabel = NewChild(go.transform, "Label", out var gotoLabelRt);
        Stretch(gotoLabelRt);
        NewText(gotoLabel, "前往", 40, Color.white, TextAlignmentOptions.Center);
    }

    /// <summary>一格奖励:图标(无 sprite 时显示色块) + ×数量,返回下一格的起始 x。</summary>
    private float BuildReward(Transform parent, Sprite sprite, Color iconColor, int count, float x)
    {
        const float iconSize = 88f;
        var icon = NewChild(parent, "RewardIcon", out var iconRt);
        iconRt.anchorMin = new Vector2(0, 0); iconRt.anchorMax = new Vector2(0, 0); iconRt.pivot = new Vector2(0, 0);
        iconRt.anchoredPosition = new Vector2(x, 48); iconRt.sizeDelta = new Vector2(iconSize, iconSize);
        var img = icon.AddComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = false;
        if (sprite != null) { img.sprite = sprite; img.color = Color.white; }
        else img.color = iconColor; // 缺图标时用色块占位

        var cnt = NewChild(parent, "RewardCount", out var cntRt);
        cntRt.anchorMin = new Vector2(0, 0); cntRt.anchorMax = new Vector2(0, 0); cntRt.pivot = new Vector2(0, 0);
        cntRt.anchoredPosition = new Vector2(x + iconSize + 6, 48); cntRt.sizeDelta = new Vector2(140, iconSize);
        NewText(cnt, $"×{count}", 36, TitleText, TextAlignmentOptions.Left);

        return x + iconSize + 160; // 图标 + 数量 + 间距
    }

    /// <summary>取奖励物品的图标(配表 iconPath → Resources 精灵);取不到返回 null,由调用方用色块占位。</summary>
    private Sprite ItemIcon(uint itemId)
    {
        if (itemId == 0 || config == null || resMgr == null) return null;
        var item = config.itemConfig.Get(itemId);
        if (item == null || string.IsNullOrEmpty(item.IconPath)) return null;
        return resMgr.Load<Sprite>(item.IconPath);
    }

    /// <summary>前往:关闭任务界面,回到上层去执行任务。后续可按任务类型路由到具体玩法。</summary>
    private void OnGoto(Task task) => CloseSelf();

    // ---------------- 工具 ----------------

    private GameObject NewChild(Transform parent, string name, out RectTransform rt)
    {
        var go = new GameObject(name, typeof(RectTransform));
        rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return go;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private TextMeshProUGUI NewText(GameObject go, string text, float size, Color color, TextAlignmentOptions align)
    {
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = UITheme.Font(size); tmp.alignment = align; tmp.color = color;
        tmp.raycastTarget = false; tmp.enableWordWrapping = false; tmp.overflowMode = TextOverflowModes.Ellipsis;
        var f = font != null ? font : TMP_Settings.defaultFontAsset;
        if (f != null) tmp.font = f;
        return tmp;
    }
}
