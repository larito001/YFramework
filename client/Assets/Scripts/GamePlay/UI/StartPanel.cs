using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YOTO;

/// <summary>
/// 主界面(大厅):竖屏手机布局。
///   左上:头像框 + 等级    右上:资源/金币(<see cref="CurrencySystem"/>)
///   左侧:设置(方形按钮)  底部一排:商店 / 准备(居中主按钮) / 图鉴
/// 预制体由 <c>Tools/UI/Build StartPanel Prefab</c> 程序化生成,脚本字段在那里接好。
///
/// 行为:设置→<see cref="SettingPanel"/>;商店→<see cref="ShopPanel"/>;
/// 准备(底部居中主按钮)→打开装备界面 <see cref="EquipPanel"/>(在那里选好装备再「出发」进游戏)。图鉴面板尚未实现,先占位。
/// </summary>
public class StartPanel : UIPageBase
{
    [Header("顶部信息")]
    public Image avatarFrame;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI coinText;

    [Header("按钮")]
    public Button btn_setting;
    public Button btn_shop;
    public Button btn_prepare;
    public Button btn_codex;

    private CurrencySystem currency;
    private EventMgr eventMgr;

    public override void OnLoad()
    {
        currency = GetService<CurrencySystem>();
        eventMgr = GetService<EventMgr>();
        if (btn_setting != null) btn_setting.onClick.AddListener(OnSettingClick);
        if (btn_shop != null) btn_shop.onClick.AddListener(OnShopClick);
        if (btn_prepare != null) btn_prepare.onClick.AddListener(OnPrepareClick);
        if (btn_codex != null) btn_codex.onClick.AddListener(OnCodexClick);
    }

    public override void OnShow()
    {
        eventMgr?.Add(YOTOEventType.RefreshCurrency, RefreshHeader); // 货币变化即刷新顶部金币
        RefreshHeader();
    }

    public override void OnHide()
    {
        eventMgr?.Remove(YOTOEventType.RefreshCurrency, RefreshHeader);
    }

    public override void OnResize()
    {
    }

    // ---------------- 顶部信息 ----------------

    /// <summary>刷新头像 / 等级 / 金币显示。金币取自 <see cref="CurrencySystem"/>;等级数值系统未接入,先占位。</summary>
    private void RefreshHeader()
    {
        if (levelText != null) levelText.text = "Lv.1";
        if (coinText != null) coinText.text = currency != null ? currency.Get(CurrencyType.Gold).ToString() : "0";
    }

    // ---------------- 按钮 ----------------

    // 准备(底部居中主按钮):打开装备界面,在那里选好出战装备后点「出发」进游戏。
    private void OnPrepareClick()
    {
        Show<EquipPanel>();
    }

    private void OnSettingClick()
    {
        Show<SettingPanel>();
    }

    private void OnShopClick()
    {
        Show<ShopPanel>();
    }

    // 以下面板尚未实现,先占位。接入后把 Debug.Log 换成对应 Show<TPage>()。

    private void OnCodexClick()
    {
        Debug.Log("[StartPanel] 图鉴暂未开放");
    }
}
