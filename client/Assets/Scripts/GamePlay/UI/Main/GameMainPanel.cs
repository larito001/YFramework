using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YFramework.Config;
using YOTO;

/// <summary>
/// 游戏内打猎 HUD(<see cref="UIEnum.GameMainPanel"/>):出发进入对局后显示,常驻覆盖在场景之上(无遮罩)。
///   左上:当前地图名称 + 本局积分     右上:资源金币
///   中部:瞄准镜准星(点「瞄准」显示/隐藏)
///   底部:瞄准(左) / 射击(右)圆钮 + 剩余子弹
/// 弹药容量取自所选子弹装备(<see cref="LoadoutSystem"/> 选中子弹的 MaxStack);积分为本局演示数值。
/// 预制体由 <c>Tools/UI/Build GameMainPanel Prefab</c> 生成。
/// </summary>
public class GameMainPanel : UIPageBase
{
    [Header("顶部")]
    public TextMeshProUGUI mapNameText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI coinText;

    [Header("中部")]
    public GameObject scope; // 瞄准镜准星(瞄准时显示)

    [Header("底部")]
    public Button aimBtn;
    public Button shootBtn;
    public TextMeshProUGUI ammoText;

    private CurrencySystem currency;
    private LoadoutSystem loadout;
    private ConfigManager config;
    private EventMgr eventMgr;

    private int score;
    private int ammo;
    private bool aiming;

    public override void OnLoad()
    {
        currency = GetService<CurrencySystem>();
        loadout = GetService<LoadoutSystem>();
        config = GetService<ConfigManager>();
        eventMgr = GetService<EventMgr>();
        if (aimBtn != null) aimBtn.onClick.AddListener(ToggleAim);
        if (shootBtn != null) shootBtn.onClick.AddListener(Shoot);
    }

    public override void OnShow()
    {
        eventMgr?.Add(YOTOEventType.RefreshCurrency, RefreshCoin);

        score = 0;
        ammo = InitialAmmo();
        aiming = false;
        if (scope != null) scope.SetActive(false);
        if (mapNameText != null) mapNameText.text = "湿地·黎明";

        RefreshCoin();
        RefreshScore();
        RefreshAmmo();
    }

    public override void OnHide()
    {
        eventMgr?.Remove(YOTOEventType.RefreshCurrency, RefreshCoin);
    }

    public override void OnResize() { }

    // ---------------- 操作 ----------------

    private void ToggleAim()
    {
        aiming = !aiming;
        if (scope != null) scope.SetActive(aiming);
    }

    private void Shoot()
    {
        if (ammo <= 0) return; // 子弹打光不再响应
        ammo--;
        eventMgr?.Trigger(YOTOEventType.Shoot); // 触发相机抖动等开枪反馈
        score += 100; // 命中演示:每发计 100 分(真正的命中判定接入打猎玩法后替换)
        RefreshAmmo();
        RefreshScore();
    }

    // ---------------- 刷新 ----------------

    private void RefreshCoin()
    {
        if (coinText == null || currency == null) return;
        coinText.text = $"{currency.DisplayName(CurrencyType.Gold)} {currency.Get(CurrencyType.Gold)}\n{currency.DisplayName(CurrencyType.Diamond)} {currency.Get(CurrencyType.Diamond)}";
    }

    private void RefreshScore()
    {
        if (scoreText != null) scoreText.text = $"当前对局获得积分：{score}";
    }

    private void RefreshAmmo()
    {
        if (ammoText != null) ammoText.text = $"剩余子弹：{ammo}";
        if (shootBtn != null) shootBtn.interactable = ammo > 0;
    }

    /// <summary>弹匣容量:选中子弹装备的 MaxStack;取不到则默认 10。</summary>
    private int InitialAmmo()
    {
        int bulletId = loadout != null ? loadout.GetSelected(ShopCategory.Bullet) : 0;
        if (bulletId > 0 && config != null)
        {
            var it = config.itemConfig.Get((uint)bulletId);
            if (it != null && it.MaxStack > 0) return it.MaxStack;
        }
        return 10;
    }
}
