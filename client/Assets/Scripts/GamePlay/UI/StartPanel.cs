using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YOTO;

/// <summary>
/// 主界面(大厅):竖屏手机布局。
///   左上:设置(方形按钮)        右上:体力 + 金币两项资源(<see cref="CurrencySystem"/>),体力后带「广告补充」按钮
///   底部:左侧竖排 任务/商店 · 中间 准备(主按钮) · 右侧 图鉴
/// 预制体由 <c>Tools/UI/Build StartPanel Prefab</c> 程序化生成,脚本字段在那里接好。
///
/// 行为:设置→<see cref="SettingPanel"/>;商店→<see cref="ShopPanel"/>;任务→<see cref="TaskPanel"/>;图鉴→<see cref="CodexPanel"/>;
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

    private const int EnergyAdReward = 5;                  // 看完一次激励广告补的体力点数(接入后可调)
    private const string AdPlacementEnergy = "energy_refill"; // 体力广告位标识

    private CurrencySystem currency;
    private EventMgr eventMgr;

    public override void OnLoad()
    {
        currency = GetService<CurrencySystem>();
        eventMgr = GetService<EventMgr>();
        if (btn_setting != null) btn_setting.onClick.AddListener(OnSettingClick);
        if (btn_energyAd != null) btn_energyAd.onClick.AddListener(OnEnergyAdClick);
        if (btn_shop != null) btn_shop.onClick.AddListener(OnShopClick);
        if (btn_prepare != null) btn_prepare.onClick.AddListener(OnPrepareClick);
        if (btn_codex != null) btn_codex.onClick.AddListener(OnCodexClick);
        if (btn_task != null) btn_task.onClick.AddListener(OnTaskClick);
    }

    public override void OnShow()
    {
        eventMgr?.Add(YOTOEventType.RefreshCurrency, RefreshHeader); // 货币变化即刷新顶部资源
        RefreshHeader();
    }

    public override void OnHide()
    {
        eventMgr?.Remove(YOTOEventType.RefreshCurrency, RefreshHeader);
    }

    public override void OnResize()
    {
    }

    // ---------------- 顶部资源 ----------------

    /// <summary>刷新体力 / 金币显示,数值取自 <see cref="CurrencySystem"/>。</summary>
    private void RefreshHeader()
    {
        if (energyText != null)
            energyText.text = currency != null ? $"{currency.DisplayName(CurrencyType.Energy)} {currency.Get(CurrencyType.Energy)}" : "0";
        if (goldText != null)
            goldText.text = currency != null ? $"{currency.DisplayName(CurrencyType.Gold)} {currency.Get(CurrencyType.Gold)}" : "0";
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

    /// <summary>
    /// 体力「广告补充」按钮:预留激励广告接口。已注册 <see cref="IAdService"/> 时请求播放,看完回调里补体力并写盘;
    /// 未接入广告(接口未注册)时只记日志、不发奖,避免免费刷体力。
    /// </summary>
    private void OnEnergyAdClick()
    {
        if (Context.TryGet<IAdService>(out var ad))
        {
            ad.ShowRewardedAd(AdPlacementEnergy, rewarded =>
            {
                if (!rewarded || currency == null) return;
                currency.Add(CurrencyType.Energy, EnergyAdReward);
                currency.Save(); // 关键节点主动写盘,保证补的体力落地
                Context.TryGet<TaskProgressSystem>(out var tp); tp?.AddAdWatch(1); // 计入"观看广告"类任务
            });
        }
        else
        {
            Debug.Log("[StartPanel] 广告接口(IAdService)未接入,体力广告按钮暂不发奖。接入广告 SDK 后注册 IAdService 即可生效。");
        }
    }
}
