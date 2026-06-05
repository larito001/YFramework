using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YOTO;

/// <summary>
/// 主界面(大厅):竖屏手机布局。
///   左上:设置(方形按钮)        右上:体力 + 金币两项资源(<see cref="CurrencySystem"/>),体力后带「广告补充」按钮
///   底部:左侧竖排 任务/商店 · 中间 准备(主按钮) · 右侧竖排 图鉴/排行榜
/// 预制体由 <c>Tools/UI/Build StartPanel Prefab</c> 程序化生成,脚本字段在那里接好。
///
/// 行为:设置→<see cref="SettingPanel"/>;商店→<see cref="ShopPanel"/>;任务→<see cref="TaskPanel"/>;图鉴→<see cref="CodexPanel"/>;排行榜→<see cref="LeaderboardPanel"/>;
/// 准备(中间主按钮)→选关 <see cref="MapSelectPanel"/>(选好关卡再进装备界面「出发」)。
/// 体力广告按钮→请求激励广告(<see cref="IAdService"/>,未接入则不发奖,见 <see cref="OnEnergyAdClick"/>)。
/// </summary>
public class StartPanel : UIPageBase
{
    [Header("顶部资源")]
    public TextMeshProUGUI energyText; // 体力
    public TextMeshProUGUI goldText;   // 金币

    [Header("按钮")]
    public Button btn_setting;
    public Button btn_energyAd; // 体力后的「广告补充体力」按钮(预留接口)
    public Button btn_shop;
    public Button btn_task;
    public Button btn_prepare;
    public Button btn_codex;
    public Button btn_leaderboard;

    private const int EnergyAdReward = 1;                  // 看完一次激励广告补 1 点体力
    private const string AdPlacementEnergy = "energy_refill"; // 体力广告位标识

    private CurrencySystem currency;
    private EventMgr eventMgr;
    private DailyAdEnergySystem dailyAd; // 每日广告补体力次数(走 StoreMgr → 本地+云)

    public override void OnLoad()
    {
        currency = GetService<CurrencySystem>();
        eventMgr = GetService<EventMgr>();
        dailyAd = GetService<DailyAdEnergySystem>();
        if (btn_setting != null) btn_setting.onClick.AddListener(OnSettingClick);
        if (btn_energyAd != null) btn_energyAd.onClick.AddListener(OnEnergyAdClick);
        if (btn_shop != null) btn_shop.onClick.AddListener(OnShopClick);
        if (btn_prepare != null) btn_prepare.onClick.AddListener(OnPrepareClick);
        if (btn_codex != null) btn_codex.onClick.AddListener(OnCodexClick);
        if (btn_task != null) btn_task.onClick.AddListener(OnTaskClick);
        if (btn_leaderboard != null) btn_leaderboard.onClick.AddListener(OnLeaderboardClick);
        CurrencyIcon.Bind(goldText, CurrencyType.Gold);     // 资源胶囊图标:运行时从 Resources 动态加载(方便换图)
        CurrencyIcon.Bind(energyText, CurrencyType.Energy);
    }

    public override void OnShow()
    {
        eventMgr?.Add(YOTOEventType.RefreshCurrency, RefreshHeader); // 货币变化即刷新顶部资源
        RefreshHeader();
        RefreshEnergyAdButton();
    }

    public override void OnHide()
    {
        eventMgr?.Remove(YOTOEventType.RefreshCurrency, RefreshHeader);
    }

    public override void OnResize()
    {
    }

    // ---------------- 顶部资源 ----------------

    /// <summary>刷新体力 / 金币显示:胶囊已用图标表示币种(预制体烘焙),这里只填数值,不再写「体力/金币」文字。</summary>
    private void RefreshHeader()
    {
        if (energyText != null)
            energyText.text = currency != null ? currency.Get(CurrencyType.Energy).ToString() : "0";
        if (goldText != null)
            goldText.text = currency != null ? currency.Get(CurrencyType.Gold).ToString() : "0";
    }

    // ---------------- 按钮 ----------------

    // 准备(中间主按钮):先打开选择关卡界面,选好关卡后再进装备界面,最后「出发」进游戏。
    private void OnPrepareClick()
    {
        Show<MapSelectPanel>();
    }

    private void OnSettingClick()
    {
        Show<SettingPanel>();
    }

    private void OnShopClick()
    {
        Show<ShopPanel>();
    }

    private void OnCodexClick()
    {
        Show<CodexPanel>();
    }

    private void OnTaskClick()
    {
        Show<TaskPanel>();
    }

    // 排行榜:打开排行榜界面(界面内优先调起 TapTap 内置榜单,不可用则自绘列表)。
    private void OnLeaderboardClick()
    {
        Show<LeaderboardPanel>();
    }

    /// <summary>
    /// 体力「广告补充」按钮:预留激励广告接口。已注册 <see cref="IAdService"/> 时请求播放,看完回调里补体力并写盘;
    /// 未接入广告(接口未注册)时只记日志、不发奖,避免免费刷体力。
    /// </summary>
    private void OnEnergyAdClick()
    {
        // 每天最多补 DailyAdEnergySystem.DailyLimit 次(次数随存档槽落盘 → 本地+云)
        if (dailyAd != null && !dailyAd.CanWatch)
        {
            GetService<FlyTextMgr>()?.AddTextAtScreenCenter($"每日最多恢复{dailyAd.DailyLimit}次体力");
            RefreshEnergyAdButton();
            return;
        }

        if (Context.TryGet<IAdService>(out var ad))
        {
            ad.ShowRewardedAd(AdPlacementEnergy, rewarded =>
            {
                if (!rewarded || currency == null) return;
                currency.Add(CurrencyType.Energy, EnergyAdReward); // 补 1 点体力
                currency.Save(); // 关键节点主动写盘,保证补的体力落地
                dailyAd?.Record(); // 记一次今日已用(只有真看完才计),内部立即落盘
                Context.TryGet<TaskProgressSystem>(out var tp); tp?.AddAdWatch(1); // 计入"观看广告"类任务
                // 通用奖励领取弹窗(Top 层,1 秒自动消失):体力已入账,这里仅展示
                Show<RewardClaimPanel, RewardClaimParam>(new RewardClaimParam
                {
                    title = "体力补充",
                    rewards = { RewardEntry.Currency(CurrencyType.Energy, EnergyAdReward) },
                });
                RefreshHeader();
                RefreshEnergyAdButton();
            });
        }
        else
        {
            Debug.Log("[StartPanel] 广告接口(IAdService)未接入,体力广告按钮暂不发奖。接入广告 SDK 后注册 IAdService 即可生效。");
        }
    }

    /// <summary>广告补体力按钮保持可点:达上限时点击给「每日最多恢复 N 次体力」提示,而不是置灰不可点(置灰就收不到点击、弹不出提示)。</summary>
    private void RefreshEnergyAdButton()
    {
        if (btn_energyAd != null) btn_energyAd.interactable = true;
    }
}
