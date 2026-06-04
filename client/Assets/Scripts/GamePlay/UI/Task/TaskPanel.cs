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
///   中部:可滚动任务卡列表,每张卡 = 标题 + 进度(当前/目标) + 奖励(物品/金币/体力 ×数量) + 前往/领取/已领取 按钮
/// 任务定义走配表(<see cref="ConfigManager.taskConfig"/>:task.xlsx → Task.bytes),按 category 分每日/常规两类;
/// 进度与领取状态来自 <see cref="TaskProgressSystem"/>(登录/连登/击杀/看广告/获武器按 objType 自动累计),完成可领,领取发奖并飘字。
/// 进度/领取变化由 <see cref="YOTOEventType.RefreshTask"/> 驱动列表重建。预制体外壳由 <c>Tools/UI/Build TaskPanel Prefab</c> 生成。
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
    private static readonly Color IconBox = new Color(0.78f, 0.80f, 0.86f, 1f);    // 物品图标缺失时的占位底
    private static readonly Color CoinIcon = new Color(0.45f, 0.74f, 0.36f, 1f);   // 金币图标(绿色块占位)
    private static readonly Color EnergyIcon = new Color(0.36f, 0.62f, 0.86f, 1f); // 体力图标(蓝色块占位)
    private static readonly Color GotoBtn = new Color(0.96f, 0.66f, 0.18f, 1f);    // 「前往」橙钮
    private static readonly Color ClaimBtn = new Color(0.30f, 0.74f, 0.36f, 1f);   // 「领取」绿钮(已完成可领)
    private static readonly Color ClaimedBtn = new Color(0.62f, 0.62f, 0.66f, 1f); // 「已领取」灰钮(不可点)
    // 卡片底框/标题文字色已烘进 TaskCard 预制体。

    private ConfigManager config;
    private CurrencySystem currency;
    private ResMgr resMgr;
    private EventMgr eventMgr;
    private TaskProgressSystem taskProgress;
    private TMP_FontAsset font;

    private readonly List<Task> entries = new List<Task>();
    private uint currentCategory = CategoryDaily;
    private GameObject cardPrefab; // 任务卡片预制体(Resources/UI/Task/TaskCard,TaskCardBuilder 生成)

    public override void OnLoad()
    {
        config = GetService<ConfigManager>();
        currency = GetService<CurrencySystem>();
        resMgr = GetService<ResMgr>();
        eventMgr = GetService<EventMgr>();
        taskProgress = GetService<TaskProgressSystem>();
        if (coinText != null) font = coinText.font; // 复用外壳字体给运行时卡片
        cardPrefab = resMgr.Load<GameObject>("UI/Task/TaskCard"); // 卡片预制体
        if (cardPrefab == null) Debug.LogError("[TaskPanel] 未找到 TaskCard 预制体,请先执行 Tools/UI/Build TaskCard Prefab(或 Build ALL UI Prefabs)。");

        if (backBtn != null) backBtn.onClick.AddListener(CloseSelf);
        if (tabDaily != null) tabDaily.onClick.AddListener(() => SelectCategory(CategoryDaily));
        if (tabRegular != null) tabRegular.onClick.AddListener(() => SelectCategory(CategoryRegular));
    }

    public override void OnShow()
    {
        eventMgr?.Add(YOTOEventType.RefreshCurrency, RefreshCoin);
        eventMgr?.Add(YOTOEventType.RefreshTask, RebuildList); // 任务进度/领取变化即重建当前列表
        SelectCategory(currentCategory);
        RefreshCoin();
        // 底部横幅广告(原生浮层)。未接入广告 / 非 Android 时取不到或空操作,自动跳过。
        if (Context.TryGet<IAdService>(out var ad)) ad.ShowBottomBanner("task");
    }

    public override void OnHide()
    {
        eventMgr?.Remove(YOTOEventType.RefreshCurrency, RefreshCoin);
        eventMgr?.Remove(YOTOEventType.RefreshTask, RebuildList);
        if (Context.TryGet<IAdService>(out var ad)) ad.HideBanner(); // 关闭即收掉横幅
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

    /// <summary>当前进度:取自 <see cref="TaskProgressSystem"/>(登录/连登/击杀/看广告/获武器按 objType 自动累计)。</summary>
    private int CurrentProgress(uint taskId) => taskProgress != null ? taskProgress.GetProgress(taskId) : 0;

    // ---------------- 列表 ----------------

    private void RebuildList()
    {
        if (content == null) return;
        SortEntriesByState(); // 可领取→未完成→已领取(组内按配表 SortPriority)
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
        foreach (var task in entries) BuildCard(task);
    }

    /// <summary>按状态排序:可领取(最顶)→ 未完成(中)→ 已领取(最底);同组内按配表 SortPriority 升序。
    /// 每次重建都重排——领取后 RefreshTask 触发本方法,刚领的任务会自动沉到底部。</summary>
    private void SortEntriesByState()
    {
        entries.Sort((a, b) =>
        {
            int ga = StateOrder(a.Id), gb = StateOrder(b.Id);
            if (ga != gb) return ga.CompareTo(gb);
            return a.SortPriority.CompareTo(b.SortPriority);
        });
    }

    /// <summary>状态排序键:可领取=0(顶),未完成=1(中),已领取=2(底)。</summary>
    private int StateOrder(uint id)
    {
        if (taskProgress == null) return 1;
        if (taskProgress.IsClaimed(id)) return 2;   // 已领取沉底
        if (taskProgress.CanClaim(id)) return 0;     // 可领取置顶
        return 1;                                     // 未完成居中
    }

    private void BuildCard(Task task)
    {
        if (cardPrefab == null) return;
        var go = Instantiate(cardPrefab);
        go.transform.SetParent(content, false);
        go.name = $"Task_{task.Id}";
        var view = go.GetComponent<TaskCardView>();
        if (view == null) { Destroy(go); return; }

        view.titleText.text = task.Name;
        view.progressText.text = $"进度 <color=#D94C40>{CurrentProgress(task.Id)}</color> / {task.TargetAmount}";

        // 奖励:按 物品→金币→体力 顺序填进预置的 3 个格,多余的隐藏
        for (int i = 0; i < view.rewardRoots.Length; i++) view.rewardRoots[i].SetActive(false);
        int slot = 0;
        if (task.RewardItemId > 0 && task.RewardItemCount > 0)
            FillReward(view, slot++, ItemIcon(task.RewardItemId), IconBox, task.RewardItemCount);
        if (task.RewardCoin > 0)
            FillReward(view, slot++, view.coinIcon, CoinIcon, task.RewardCoin);     // 烤进预制体的金币图标,缺失才退色块
        if (task.RewardEnergy > 0)
            FillReward(view, slot++, view.energyIcon, EnergyIcon, task.RewardEnergy); // 同上,体力图标

        // 右下按钮:未完成=前往;已完成未领=领取;已领取=置灰不可点
        bool canClaim = taskProgress != null && taskProgress.CanClaim(task.Id);
        bool claimed = taskProgress != null && taskProgress.IsClaimed(task.Id);
        view.actionBg.color = claimed ? ClaimedBtn : (canClaim ? ClaimBtn : GotoBtn);
        view.actionButton.interactable = !claimed;
        view.actionButton.onClick.AddListener(() => OnActionClick(task));
        view.actionLabel.text = claimed ? "已领取" : (canClaim ? "领取" : "前往");
    }

    /// <summary>填一格奖励:有 sprite 用精灵,否则用 <paramref name="iconColor"/> 色块占位;显示该格。</summary>
    private void FillReward(TaskCardView view, int slot, Sprite sprite, Color iconColor, int count)
    {
        if (slot < 0 || slot >= view.rewardRoots.Length) return;
        view.rewardRoots[slot].SetActive(true);
        var img = view.rewardIcons[slot];
        img.preserveAspect = true;
        if (sprite != null) { img.sprite = sprite; img.color = Color.white; }
        else { img.sprite = null; img.color = iconColor; } // 缺图标用色块占位
        view.rewardCounts[slot].text = $"×{count}";
    }

    /// <summary>取奖励物品的图标(配表 iconPath → Resources 精灵);取不到返回 null,由调用方用色块占位。</summary>
    private Sprite ItemIcon(uint itemId)
    {
        if (itemId == 0 || config == null || resMgr == null) return null;
        var item = config.itemConfig.Get(itemId);
        if (item == null || string.IsNullOrEmpty(item.IconPath)) return null;
        return resMgr.Load<Sprite>(item.IconPath);
    }

    /// <summary>右下按钮点击:可领取→领奖并飘字(列表由 RefreshTask 事件自动重建);否则前往(关闭界面去做)。</summary>
    private void OnActionClick(Task task)
    {
        if (taskProgress != null && taskProgress.CanClaim(task.Id))
        {
            if (taskProgress.Claim(task.Id))
                ShowRewardPopup(task); // 奖励已由 Claim 入账,这里弹通用领取弹窗展示
            return; // 领取后 RefreshTask 会重建列表,无需手动刷新
        }
        if (taskProgress != null && taskProgress.IsClaimed(task.Id)) return; // 已领取:无操作
        CloseSelf(); // 未完成:前往(回大厅去做任务)。后续可按任务类型路由到具体玩法。
    }

    /// <summary>领取成功后弹通用奖励领取弹窗(Top 层,1 秒自动消失):按任务配表的物品/金币/体力组奖励列表。</summary>
    private void ShowRewardPopup(Task task)
    {
        var rewards = new List<RewardEntry>();
        if (task.RewardItemId > 0 && task.RewardItemCount > 0)
            rewards.Add(RewardEntry.Item((int)task.RewardItemId, task.RewardItemCount));
        if (task.RewardCoin > 0) rewards.Add(RewardEntry.Currency(CurrencyType.Gold, task.RewardCoin));
        if (task.RewardEnergy > 0) rewards.Add(RewardEntry.Currency(CurrencyType.Energy, task.RewardEnergy));
        if (rewards.Count == 0) return;

        Show<RewardClaimPanel, RewardClaimParam>(new RewardClaimParam { title = "任务奖励", rewards = rewards });
    }

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
