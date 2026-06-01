using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YFramework.Config;
using YOTO;

/// <summary>
/// 游戏内打猎 HUD(<see cref="UIEnum.GameMainPanel"/>):出发进入对局后显示,常驻覆盖在场景之上。
///   左上:当前地图名称 + 本局积分     右上:资源金币
///   中部:瞄准镜准星 + 镜外黑边遮罩(点「瞄准」显示,相机同时变焦放大)
///   底部:瞄准(左) / 射击(右)圆钮 + 剩余子弹
/// 玩法流程:点瞄准 → 拖屏移动准星(<see cref="CameraSwipeLook"/>)→ 点射击。射击从屏幕中心打射线
/// (<see cref="ScopeAimController.FireRay"/>):命中动物则加该动物击杀积分并播死亡动画,没打中不加分;射击后自动关镜。
/// 弹药容量取自所选子弹装备(<see cref="LoadoutSystem"/> 选中子弹的 MaxStack)。
/// 预制体由 <c>Tools/UI/Build GameMainPanel Prefab</c> 生成。
/// </summary>
public class GameMainPanel : UIPageBase
{
    [Header("顶部")]
    public TextMeshProUGUI mapNameText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI coinText;

    [Header("中部")]
    public GameObject scope;     // 瞄准镜准星(瞄准时显示)
    public GameObject scopeMask; // 瞄准镜黑边遮罩(铺满屏幕,瞄准时显示;中间圆孔透出场景)

    [Header("底部")]
    public Button aimBtn;
    public Button shootBtn;
    public TextMeshProUGUI ammoText;

    private CurrencySystem currency;
    private LoadoutSystem loadout;
    private ConfigManager config;
    private EventMgr eventMgr;
    private ScopeAimController scopeAim; // 相机端瞄准机制(变焦 + 命中射线),挂在主相机上

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
        SetAiming(false); // 复位:收起准星/黑边遮罩,相机回到正常视野
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

    private void ToggleAim() => SetAiming(!aiming);

    /// <summary>进入/退出瞄准:切准星 + 黑边遮罩 + 相机变焦,并刷新射击钮可点状态。</summary>
    private void SetAiming(bool on)
    {
        aiming = on;
        if (scope != null) scope.SetActive(on);
        if (scopeMask != null) scopeMask.SetActive(on);
        ScopeAim()?.SetAiming(on);
        RefreshShootButton();
    }

    private void Shoot()
    {
        if (!aiming || ammo <= 0) return; // 只在瞄准且有子弹时开火
        ammo--;
        eventMgr?.Trigger(YOTOEventType.Shoot); // 触发相机抖动等开枪反馈

        // 从屏幕中心(准星处)打射线:命中动物才加它的击杀积分并播死亡动画,没打中不加分
        var hit = ScopeAim()?.FireRay();
        if (hit != null && !hit.IsDead)
        {
            hit.Kill();
            score += hit.score;
            RefreshScore();
        }

        SetAiming(false); // 射击后关闭瞄准镜
        RefreshAmmo();
    }

    /// <summary>懒取主相机上的瞄准机制组件(进对局时由 GameStartScene 挂上)。</summary>
    private ScopeAimController ScopeAim()
    {
        if (scopeAim == null && Camera.main != null)
            scopeAim = Camera.main.GetComponent<ScopeAimController>();
        return scopeAim;
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
        RefreshShootButton();
    }

    /// <summary>射击钮只在「已瞄准且有子弹」时可点(符合 瞄准→射击 的流程)。</summary>
    private void RefreshShootButton()
    {
        if (shootBtn != null) shootBtn.interactable = aiming && ammo > 0;
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
