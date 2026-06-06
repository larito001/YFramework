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
    private const uint CategoryDaily = 1;   // 日常任务
    private const uint CategoryRegular = 2; // 成就任务

    [Header("顶部")]
    public Button backBtn;
    public TextMeshProUGUI coinText;
    public TextMeshProUGUI energyText; // 体力数值(与主页一致:顶部同时显示金币与体力)

    [Header("页签(通用绿色按钮:选中绿/未选灰)")]
    public Button tabDaily;
    public Button tabRegular;

    [Header("列表(ScrollRect 的 VerticalLayoutGroup 容器)")]
    public RectTransform content;

    private static readonly Color TabOn = new Color(0.4666667f, 0.89019614f, 0.20784315f, 1f);     // 选中:通用按钮原绿(Bg)
    private static readonly Color TabOnInner = new Color(0.7411765f, 0.98823535f, 0.29411766f, 1f); // 选中:原浅绿(InnerBorder)
    private static readonly Color TabOff = new Color(0.58f, 0.58f, 0.60f, 1f);     // 未选:灰(Bg)
    private static readonly Color TabOffInner = new Color(0.70f, 0.70f, 0.72f, 1f); // 未选:浅灰(InnerBorder)
    private static readonly Color IconBox = new Color(0.78f, 0.80f, 0.86f, 1f);    // 物品图标缺失时的占位底
    private static readonly Color CoinIcon = new Color(0.45f, 0.74f, 0.36f, 1f);   // 金币图标(绿色块占位)
    private static readonly Color EnergyIcon = new Color(0.36f, 0.62f, 0.86f, 1f); // 体力图标(蓝色块占位)
    private static readonly Color ClaimBtn = new Color(0.30f, 0.74f, 0.36f, 1f);      // 「领取」绿钮(可领取)
    private static readonly Color IncompleteBtn = new Color(0.98f, 0.80f, 0.20f, 1f); // 「未完成」黄钮
    private static readonly Color ClaimedBtn = new Color(0.52f, 0.52f, 0.55f, 1f);    // 「已领取」灰钮

    // 卡片样式已烘进 TaskCard 预制体(横向:目标圆图标 + 标题 + 绿色进度条 + 奖励 + 按钮)。

    private ConfigManager config;
    private CurrencySystem currency;
    private ResMgr resMgr;
    private EventMgr eventMgr;
    private TaskProgressSystem taskProgress;
    private TMP_FontAsset font;

    private readonly List<Task> entries = new List<Task>();
    private uint currentCategory = CategoryDaily;
    private GameObject cardPrefab; // 任务卡片预制体(Resources/UI/Task/TaskCard,TaskCardBuilder 生成)
    private ResourceHandle<GameObject> cardPrefabHandle; // 持模板句柄到页面销毁释放

    public override void OnLoad()
    {
        config = GetService<ConfigManager>();
        currency = GetService<CurrencySystem>();
        resMgr = GetService<ResMgr>();
        eventMgr = GetService<EventMgr>();
        taskProgress = GetService<TaskProgressSystem>();
        if (coinText != null) font = coinText.font; // 复用外壳字体给运行时卡片
        resMgr.LoadHandleAsync<GameObject>("UI/Task/TaskCard", h => // 卡片预制体(异步)
        {
            if (this == null) { h?.Release(); return; } // 页面已销毁:丢弃并配平
            cardPrefabHandle = h;
            cardPrefab = h?.Asset;
            if (cardPrefab == null) Debug.LogError("[TaskPanel] 未找到 TaskCard 预制体,请先执行 Tools/UI/Build TaskCard Prefab(或 Build ALL UI Prefabs)。");
            else RebuildList(); // 模板就绪后补建列表(OnShow 时模板未到则当时不填)
        });

        if (backBtn != null) backBtn.onClick.AddListener(CloseSelf);
        CurrencyIcon.Bind(coinText, CurrencyType.Gold); // 资源胶囊图标:运行时从 Resources 动态加载(方便换图)
        CurrencyIcon.Bind(energyText, CurrencyType.Energy);
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

    private void OnDestroy() => cardPrefabHandle?.Release(); // 释放卡片模板句柄

    // ---------------- 顶部 ----------------

    private void RefreshCoin()
    {
        if (currency == null) return;
        if (coinText != null) coinText.text = currency.Get(CurrencyType.Gold).ToString();
        if (energyText != null) energyText.text = currency.Get(CurrencyType.Energy).ToString();
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
        // 通用绿色按钮:染 Bg + InnerBorder1 + 文字(选中绿/白字,未选灰/深字)。
        var bg = btn.transform.Find("Bg")?.GetComponent<Image>();
        if (bg != null) bg.color = on ? TabOn : TabOff;
        var inner = btn.transform.Find("InnerBorder1")?.GetComponent<Image>();
        if (inner != null) inner.color = on ? TabOnInner : TabOffInner;
        var txt = btn.GetComponentInChildren<TextMeshProUGUI>(true);
        if (txt != null) txt.color = on ? Color.white : new Color(0.36f, 0.36f, 0.38f, 1f);
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

        // 进度条(Slider value = 比例) + X/Y
        int cur = CurrentProgress(task.Id);
        int target = Mathf.Max(1, task.TargetAmount);
        if (view.progressSlider != null) view.progressSlider.value = Mathf.Clamp01((float)cur / target);
        if (view.progressText != null) view.progressText.text = $"{cur}/{task.TargetAmount}";

        // 左侧目标图标(配表 iconPath;暂用白图占位,填了就换)
        if (view.objectiveIcon != null && !string.IsNullOrEmpty(task.IconPath))
            ResUI.SetSpriteAsync(resMgr, view.objectiveIcon, task.IconPath, IconBox);

        // 奖励(单个,主奖励:物品 > 金币 > 体力)
        BindReward(view, task);

        // 右下按钮:未完成=前往;已完成未领=领取;已领取=置灰不可点
        bool canClaim = taskProgress != null && taskProgress.CanClaim(task.Id);
        bool claimed = taskProgress != null && taskProgress.IsClaimed(task.Id);
        view.actionBg.color = claimed ? ClaimedBtn : (canClaim ? ClaimBtn : IncompleteBtn);
        view.actionButton.interactable = canClaim; // 只有可领取才可点
        view.actionButton.onClick.AddListener(() => OnActionClick(task));
        view.actionLabel.text = claimed ? "已领取" : (canClaim ? "领取" : "未完成");
        view.actionLabel.color = claimed ? new Color(0.32f, 0.32f, 0.34f, 1f) : Color.white;
    }

    /// <summary>绑定单个奖励图标 + ×数量(主奖励优先级:物品 > 金币 > 体力)。</summary>
    private void BindReward(TaskCardView view, Task task)
    {
        if (view.rewardIcon == null) return;
        string path; Color fallback; int count;
        if (task.RewardItemId > 0 && task.RewardItemCount > 0) { path = ItemIconPath(task.RewardItemId); fallback = IconBox; count = task.RewardItemCount; }
        else if (task.RewardCoin > 0) { path = CurrencyIcon.ResPath(CurrencyType.Gold); fallback = CoinIcon; count = task.RewardCoin; }
        else if (task.RewardEnergy > 0) { path = CurrencyIcon.ResPath(CurrencyType.Energy); fallback = EnergyIcon; count = task.RewardEnergy; }
        else { view.rewardIcon.enabled = false; if (view.rewardCount != null) view.rewardCount.text = ""; return; }
        view.rewardIcon.enabled = true; view.rewardIcon.preserveAspect = true;
        ResUI.SetSpriteAsync(resMgr, view.rewardIcon, path, fallback);
        if (view.rewardCount != null) view.rewardCount.text = $"×{count}";
    }

    /// <summary>取奖励物品的图标路径(配表 iconPath);取不到返回 null,由调用方用色块占位。</summary>
    private string ItemIconPath(uint itemId)
    {
        if (itemId == 0 || config == null) return null;
        var item = config.itemConfig.Get(itemId);
        return item == null || string.IsNullOrEmpty(item.IconPath) ? null : item.IconPath;
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
