using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YFramework.Config;
using YOTO;

/// <summary>
/// 游戏内打猎 HUD(<see cref="UIEnum.GameMainPanel"/>):出发进入对局后显示,常驻覆盖在场景之上。
///   左上:当前地图名称 + 本局积分     右上:资源金币
///   中部:瞄准镜准星 + 镜外黑边遮罩(点「瞄准」显示,相机同时变焦放大)
///   底部中:瞄准/射击 圆钮(同一个,文字随状态切换) + 剩余子弹;左下:结束打猎(→ 确认 → 结算)
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
    public Button actionBtn;            // 中间圆钮:未瞄准时=「瞄准」,瞄准时=「射击」
    public TextMeshProUGUI actionLabel; // 圆钮文字,随瞄准状态切换
    public TextMeshProUGUI ammoText;
    public Button endBtn;               // 结束打猎(左下):弹确认框 → 结算

    private CurrencySystem currency;
    private LoadoutSystem loadout;
    private ConfigManager config;
    private EventMgr eventMgr;
    private MapSystem maps;               // 当前选中关卡(显示关卡名)
    private ScopeAimController scopeAim; // 相机端瞄准机制(变焦 + 命中射线),挂在主相机上

    private int score;
    private int ammo;
    private bool aiming;
    private readonly Dictionary<int, int> kills = new(); // animalId → 本局击杀数,结束时组装结算明细

    private const float TapMoveThreshold = 20f; // 像素:按下到抬起位移小于此值算「点击」,否则算拖拽
    private bool pointerDown;
    private Vector2 pointerDownPos;

    public override void OnLoad()
    {
        currency = GetService<CurrencySystem>();
        loadout = GetService<LoadoutSystem>();
        config = GetService<ConfigManager>();
        eventMgr = GetService<EventMgr>();
        maps = GetService<MapSystem>();
        if (actionBtn != null) actionBtn.onClick.AddListener(OnActionClick);
        if (endBtn != null) endBtn.onClick.AddListener(OnEndHunt);
    }

    public override void OnShow()
    {
        eventMgr?.Add(YOTOEventType.RefreshCurrency, RefreshCoin);

        score = 0;
        kills.Clear();
        ammo = InitialAmmo();
        SetAiming(false); // 复位:收起准星/黑边遮罩,相机回到正常视野
        if (mapNameText != null) mapNameText.text = !string.IsNullOrEmpty(maps?.SelectedName) ? maps.SelectedName : "未知关卡";

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

    /// <summary>中间圆钮:未瞄准时点击=进入瞄准,瞄准时点击=开火。</summary>
    private void OnActionClick()
    {
        if (!aiming) SetAiming(true);
        else Shoot();
    }

    /// <summary>进入/退出瞄准:切准星 + 黑边遮罩 + 相机变焦,并把圆钮在「瞄准/射击」间切换。</summary>
    private void SetAiming(bool on)
    {
        aiming = on;
        if (scope != null) scope.SetActive(on);
        if (scopeMask != null) scopeMask.SetActive(on);
        ScopeAim()?.SetAiming(on);
        RefreshActionButton();
    }

    /// <summary>瞄准时:点击(非拖拽)镜外任意区域 → 退出瞄准回正常视角。拖拽是环视(交给 CameraSwipeLook),点按钮交给按钮。</summary>
    private void Update()
    {
        if (!aiming) return;

        if (Input.GetMouseButtonDown(0))
        {
            pointerDown = true;
            pointerDownPos = Input.mousePosition;
        }
        else if (pointerDown && Input.GetMouseButtonUp(0))
        {
            pointerDown = false;
            bool isTap = ((Vector2)Input.mousePosition - pointerDownPos).sqrMagnitude <= TapMoveThreshold * TapMoveThreshold;
            if (isTap && !IsPointerOverUI()) SetAiming(false); // 点击镜外(非 UI)空白 → 退出瞄准
        }
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
            kills.TryGetValue(hit.animalId, out var c);
            kills[hit.animalId] = c + 1; // 记一笔,供结算逐种统计
            RefreshScore();
        }

        SetAiming(false); // 射击后关闭瞄准镜
        RefreshAmmo();
    }

    /// <summary>结束打猎:先弹确认框,确认后退出瞄准并打开结算界面。</summary>
    private void OnEndHunt()
    {
        SetAiming(false); // 收起瞄准镜再弹窗
        Show<ConfirmPanel, ConfirmParam>(new ConfirmParam
        {
            title = "结束打猎",
            message = "确定结束本次打猎并查看结算？",
            onConfirm = ShowResult,
        });
    }

    /// <summary>把本局逐种击杀 + 总分组装成 <see cref="HuntResult"/> 交给结算界面。</summary>
    private void ShowResult()
    {
        var result = new HuntResult { totalScore = score };
        foreach (var kv in kills)
        {
            var def = config?.animalConfig.Get((uint)kv.Key);
            result.entries.Add(new HuntResultEntry
            {
                animalName = def != null ? def.Name : $"动物 {kv.Key}",
                count = kv.Value,
                score = kv.Value * (def != null ? def.Score : 0),
            });
        }
        result.entries.Sort((a, b) => b.score.CompareTo(a.score)); // 分高的在前
        Show<FinishPanel, HuntResult>(result);
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
        coinText.text = $"{currency.DisplayName(CurrencyType.Gold)} {currency.Get(CurrencyType.Gold)}\n{currency.DisplayName(CurrencyType.Energy)} {currency.Get(CurrencyType.Energy)}";
    }

    private void RefreshScore()
    {
        if (scoreText != null) scoreText.text = $"当前对局获得积分：{score}";
    }

    private void RefreshAmmo()
    {
        if (ammoText != null) ammoText.text = $"剩余子弹：{ammo}";
        RefreshActionButton();
    }

    /// <summary>圆钮文字随状态切换;瞄准且没子弹时禁用(不能开火,但仍可点镜外退出)。</summary>
    private void RefreshActionButton()
    {
        if (actionLabel != null) actionLabel.text = aiming ? "射击" : "瞄准";
        if (actionBtn != null) actionBtn.interactable = !aiming || ammo > 0;
    }

    private static bool IsPointerOverUI()
    {
        var es = EventSystem.current;
        if (es == null) return false;
        if (Input.touchCount > 0) return es.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        return es.IsPointerOverGameObject();
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
