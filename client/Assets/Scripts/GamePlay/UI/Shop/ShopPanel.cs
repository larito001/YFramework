using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YFramework.Config;
using YOTO;

/// <summary>
/// 商店面板(<see cref="UIEnum.ShopPanel"/>):分类页签 + 3 列卡片网格。
///   顶部:返回(关闭) / 资源金币 / 商人头像
///   中部:当前分类的卡片网格(图片 + 名称 + 价格 + 购买按钮;点击购买弹确认页,确认后购买,结果用飘字反馈)
///   底部:武器 / 瞄准镜 / 子弹 三个分类页签
/// 目录与撮合在 <see cref="ShopSystem"/>(按 <see cref="ShopCategory"/> 过滤);卡片运行时按目录构建。
/// 余额变化(RefreshCurrency)时刷新金币与各卡买得起/买不起。预制体外壳由 <c>Tools/UI/Build ShopPanel Prefab</c> 生成。
/// </summary>
public class ShopPanel : UIPageBase
{
    [Header("顶部")]
    public Button backBtn;
    public TextMeshProUGUI coinText;   // 金币数值(图标在预制体胶囊里)
    public TextMeshProUGUI energyText; // 体力数值(图标在预制体胶囊里)
    public Image merchantAvatar;

    [Header("网格")]
    public RectTransform grid; // GridLayoutGroup 容器(滚动视图内)

    [Header("底部页签")]
    public Button tabWeapon;
    public Button tabScope;
    public Button tabBullet;

    [Header("看广告领金币(预制体里摆好,展示区左下角)")]
    public Button adGoldButton;        // 看广告按钮(达上限置灰)
    public TextMeshProUGUI adGoldLabel; // 按钮文字

    private static readonly Color Affordable = new Color(0.30f, 0.78f, 0.36f, 1f); // 绿:买得起
    private static readonly Color Unaffordable = new Color(0.32f, 0.34f, 0.40f, 1f); // 灰:买不起
    private static readonly Color TabOn = new Color(0.30f, 0.55f, 0.85f, 1f);
    private static readonly Color TabOff = new Color(0.25f, 0.27f, 0.33f, 1f);

    private ShopSystem shop;
    private CurrencySystem currency;
    private LoadoutSystem loadout; // 判断装备是否已拥有(已拥有的按钮置灰)
    private ResMgr resMgr;
    private EventMgr eventMgr;

    private ShopCategory current = ShopCategory.Weapon;
    private WeaponModelPreview modelPreview;  // 模型转台(占原商人头像位,扩大到约 2/5 屏);三类(枪/镜/弹)点卡切换展示
    private int selectedId;                   // 当前展示/选中的物品 id(随分类切换)
    private readonly List<(int id, GameObject card)> previewCards = new(); // 当前分类带模型的卡(就地切换描边,避免点击时重建销毁自身)
    private GameObject cardPrefab; // 卡片预制体(Resources/UI/Shop/ShopCard,ShopCardBuilder 生成),运行时 instantiate

    private const string AdPlacementShopGold = "shop_gold"; // 商店看广告领金币的广告位标识(统计用)
    private const long AdGoldReward = 1000;                 // 看完一次发放的金币
    private bool adBusy;                                    // 广告进行中:防重复点击
    private DailyAdGoldSystem dailyAdGold;                  // 看广告领金币的每日次数限制(默认 3 次/天)

    public override void OnLoad()
    {
        shop = GetService<ShopSystem>();
        currency = GetService<CurrencySystem>();
        loadout = GetService<LoadoutSystem>();
        resMgr = GetService<ResMgr>();
        eventMgr = GetService<EventMgr>();
        cardPrefab = resMgr.Load<GameObject>("UI/Shop/ShopCard"); // 卡片预制体
        if (cardPrefab == null) Debug.LogError("[ShopPanel] 未找到 ShopCard 预制体,请先执行 Tools/UI/Build ShopCard Prefab(或 Build ALL UI Prefabs)。");
        Context.TryGet<DailyAdGoldSystem>(out dailyAdGold); // 看广告领金币每日次数限制

        if (backBtn != null) backBtn.onClick.AddListener(CloseSelf);
        if (tabWeapon != null) tabWeapon.onClick.AddListener(() => SelectCategory(ShopCategory.Weapon));
        if (tabScope != null) tabScope.onClick.AddListener(() => SelectCategory(ShopCategory.Scope));
        if (tabBullet != null) tabBullet.onClick.AddListener(() => SelectCategory(ShopCategory.Bullet));

        modelPreview = new WeaponModelPreview(CreatePreviewHost(), resMgr);
        SetupAdRewardButton(); // 武器展示区左下角的「看广告 +1000」按钮(预制体里摆好,这里接事件)
    }

    /// <summary>模型展示框:填满「武器展示区」(Merchant)。展示区/页签/列表三段位置已由 ShopPanelBuilder
    /// 在预制体里摆好(展示区 → 页签 → 列表),这里只把占位头像让位、并铺一个转台容器进去,不再运行时重排布局。</summary>
    private RectTransform CreatePreviewHost()
    {
        if (merchantAvatar != null)
        {
            merchantAvatar.enabled = false; // 让位给模型展示(展示区位置由 Builder 决定)
            var go = new GameObject("ModelPreview", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(merchantAvatar.transform, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; // 填满展示区
            return rt;
        }

        // 兜底:顶部一大块(与 Builder 的展示区区间一致)
        var host = new GameObject("ModelPreview", typeof(RectTransform));
        host.transform.SetParent(transform, false);
        var hr = (RectTransform)host.transform;
        hr.anchorMin = new Vector2(0.06f, 0.53f);
        hr.anchorMax = new Vector2(0.94f, 0.93f);
        hr.offsetMin = Vector2.zero; hr.offsetMax = Vector2.zero;
        return hr;
    }

    public override void OnShow()
    {
        eventMgr?.Add(YOTOEventType.RefreshCurrency, OnCurrencyChanged);
        eventMgr?.Add(YOTOEventType.RefreshLoadout, OnLoadoutChanged); // 购买装备解锁后刷新「已拥有」灰按钮
        SelectCategory(ShopCategory.Weapon);
        RefreshCoin();
        RefreshAdGoldButton(); // 跨天/重进商店时按今日剩余次数刷新看广告按钮
    }

    public override void OnHide()
    {
        eventMgr?.Remove(YOTOEventType.RefreshCurrency, OnCurrencyChanged);
        eventMgr?.Remove(YOTOEventType.RefreshLoadout, OnLoadoutChanged);
        // 彻底回收离屏相机 + RenderTexture(面板默认关闭 10s 后销毁,只 SetActive 会让 rig/rt 成为孤儿泄漏)。
        // Dispose 后重新打开时 Show()→EnsureRig() 会自动重建,功能不受影响。
        modelPreview?.Dispose();
    }

    public override void OnResize() { }

    private void OnCurrencyChanged()
    {
        RefreshCoin();
        RebuildGrid(); // 重建以刷新各卡绿/灰(买得起/买不起)
    }

    /// <summary>装备拥有变化(购买解锁):重建以把已拥有的按钮刷成灰「已拥有」。
    /// 购买流程里 TrySpend 先触发 RefreshCurrency(此时尚未 Grant,仍显示「购买」),Grant 后的本事件才是正确态。</summary>
    private void OnLoadoutChanged() => RebuildGrid();

    // ---------------- 顶部 ----------------

    private void RefreshCoin()
    {
        if (currency == null) return;
        if (coinText != null) coinText.text = currency.Get(CurrencyType.Gold).ToString();     // 只填数值,图标在胶囊里
        if (energyText != null) energyText.text = currency.Get(CurrencyType.Energy).ToString();
    }

    // ---------------- 分类页签 ----------------

    private void SelectCategory(ShopCategory cat)
    {
        current = cat;
        SetTabColor(tabWeapon, cat == ShopCategory.Weapon);
        SetTabColor(tabScope, cat == ShopCategory.Scope);
        SetTabColor(tabBullet, cat == ShopCategory.Bullet);
        EnsureDefaultSelection(); // 切到新分类:默认选第一件有模型的
        RebuildGrid();
        UpdatePreview();
    }

    /// <summary>当前分类未选中(或选中项已不在本类目录)时,默认选第一件有模型的。</summary>
    private void EnsureDefaultSelection()
    {
        var list = shop.CatalogOf(current);
        for (int i = 0; i < list.Count; i++)
            if ((int)list[i].Id == selectedId) return; // 当前选中仍在本类目录中

        selectedId = 0;
        for (int i = 0; i < list.Count; i++)
            if (!string.IsNullOrEmpty(list[i].ModelPath)) { selectedId = (int)list[i].Id; break; }
    }

    /// <summary>点卡片:选中它并在上方展示其模型。**就地**切换描边——不重建网格,
    /// 否则会在卡片自身的 onClick 里把自己 Destroy 掉,破坏 EventSystem(点几下后点击失灵/描边卡死)。</summary>
    private void SelectPreview(int id)
    {
        selectedId = id;
        for (int i = 0; i < previewCards.Count; i++)
            ApplyOutline(previewCards[i].card, previewCards[i].id == id);
        UpdatePreview();
    }

    /// <summary>就地显隐卡片的金色选中框(预制体里已预置 SelectFrame,SetActive 即可,不再运行时拼边)。</summary>
    private static void ApplyOutline(GameObject card, bool on)
    {
        if (card == null) return;
        var view = card.GetComponent<ShopCardView>();
        if (view != null && view.selectFrame != null) view.selectFrame.SetActive(on);
    }

    /// <summary>上方转台展示当前分类选中物品的模型(三类通用:枪/镜/弹)。</summary>
    private void UpdatePreview()
    {
        if (modelPreview == null) return;

        string path = null;
        if (selectedId > 0)
        {
            var list = shop.CatalogOf(current);
            for (int i = 0; i < list.Count; i++)
                if ((int)list[i].Id == selectedId) { path = list[i].ModelPath; break; }
        }

        if (string.IsNullOrEmpty(path)) { modelPreview.SetActive(false); return; }
        modelPreview.SetActive(true);
        modelPreview.Show(path);
    }

    private static void SetTabColor(Button btn, bool on)
    {
        if (btn == null) return;
        // Tab_01 预制体用 "Focus" 子物体表示选中态(无 targetGraphic);旧式按钮则回退到 targetGraphic 变色。
        var focus = btn.transform.Find("Focus");
        if (focus != null) { focus.gameObject.SetActive(on); return; }
        var img = btn.targetGraphic as Image;
        if (img != null) img.color = on ? TabOn : TabOff;
    }

    // ---------------- 看广告领金币(武器展示区左下角)----------------

    /// <summary>「看广告 +1000」按钮已由 ShopPanelBuilder 摆进预制体(展示区左下角);这里只接点击事件并按今日次数置态。</summary>
    private void SetupAdRewardButton()
    {
        if (adGoldButton != null) adGoldButton.onClick.AddListener(OnAdGoldClick);
        RefreshAdGoldButton(); // 按今日剩余次数置态
    }

    /// <summary>按今日剩余次数刷新看广告按钮:还能看=金色可点「看广告 +N」;用完=置灰「今日已领完」。</summary>
    private void RefreshAdGoldButton()
    {
        bool can = dailyAdGold == null || dailyAdGold.CanWatch; // 无系统(理论不会)时不限制
        if (adGoldButton != null) adGoldButton.interactable = can;
        if (adGoldLabel != null) adGoldLabel.text = can ? $"看广告 +{AdGoldReward}" : "今日已领完";
    }

    /// <summary>点「看广告 +1000」:请求激励广告,看完(rewarded=true)发金币 + 计入看广告任务 + 弹奖励;
    /// 未接入广告 / 中途关闭不发奖。adBusy 防广告进行中重复点击。</summary>
    private void OnAdGoldClick()
    {
        if (adBusy) return;
        if (dailyAdGold != null && !dailyAdGold.CanWatch) // 今日次数已用完
        {
            FlyText($"今日看广告次数已用完({dailyAdGold.DailyLimit}次)");
            RefreshAdGoldButton();
            return;
        }
        if (!Context.TryGet<IAdService>(out var ad))
        {
            FlyText("广告未接入");
            return;
        }
        adBusy = true;
        ad.ShowRewardedAd(AdPlacementShopGold, rewarded =>
        {
            adBusy = false;
            if (!rewarded || currency == null) return; // 未看完 / 无填充:不发奖、不计次
            currency.Add(CurrencyType.Gold, AdGoldReward);
            currency.Save(); // 关键节点主动写盘
            dailyAdGold?.Record(); // 看完才计一次今日次数(立即落盘→本地+云)
            Context.TryGet<TaskProgressSystem>(out var tp); tp?.AddAdWatch(1); // 计入"观看广告"类任务
            ShowReward("观看奖励", RewardEntry.Currency(CurrencyType.Gold, AdGoldReward));
            RefreshAdGoldButton(); // 更新剩余次数(用完即置灰)
        });
    }

    // ---------------- 卡片网格 ----------------

    private void RebuildGrid()
    {
        if (grid == null) return;
        previewCards.Clear();
        for (int i = grid.childCount - 1; i >= 0; i--)
        {
            var c = grid.GetChild(i);
            c.SetParent(null, false); // 先脱离父级再销毁:避免本帧旧卡与新卡并存导致布局/计数错乱
            Destroy(c.gameObject);
        }

        var list = shop.CatalogOf(current);
        for (int i = 0; i < list.Count; i++) BuildCard(list[i]);
    }

    private void BuildCard(Item item)
    {
        if (cardPrefab == null) return;
        var go = Instantiate(cardPrefab);
        go.transform.SetParent(grid, false); // 尺寸由 GridLayoutGroup 决定
        go.name = $"Card_{item.Id}";
        var view = go.GetComponent<ShopCardView>();
        if (view == null) { Destroy(go); return; }

        // 图片:渲染出的 3D 道具侧视快照(全局共享缓存,跨面板复用),无模型回退 2D 图标。预制体里 pic 已 preserveAspect。
        var sprite = ModelSnapshotCache.CardSprite(item.ModelPath, resMgr)
                     ?? (!string.IsNullOrEmpty(item.IconPath) ? resMgr.Load<Sprite>(item.IconPath) : null);
        view.pic.sprite = sprite; view.pic.enabled = sprite != null;
        // 右上角小「i」按钮弹道具描述(已烤进 ShopCard 预制体,这里只填 itemId;点卡片/图标仍走选中预览,避免切换时误触描述)
        if (view.infoBadge != null) view.infoBadge.itemId = (int)item.Id;

        // 名称 / 价格
        view.nameText.text = item.Name;
        string priceDesc = $"{currency.DisplayName((CurrencyType)item.PriceType)} {item.Price}";
        view.priceText.text = priceDesc;

        // 带模型的卡(枪/镜/弹):整卡可点 = 选中并在上方展示模型;金色选中框就地显隐
        bool hasModel = !string.IsNullOrEmpty(item.ModelPath);
        view.bg.raycastTarget = hasModel;
        view.cardButton.enabled = hasModel;
        if (hasModel)
        {
            int sid = (int)item.Id;
            view.cardButton.onClick.AddListener(() => SelectPreview(sid));
            previewCards.Add((sid, go));
            view.selectFrame.SetActive(sid == selectedId);
        }
        else
        {
            view.selectFrame.SetActive(false);
        }

        // 购买按钮:已拥有=灰「已拥有」/ 买得起=绿 / 买不起=灰。始终可点,给对应飘字反馈。
        bool owned = loadout != null && item.ShopCategory != 0 && loadout.IsOwned((int)item.Id);
        bool affordable = !owned && shop.CanAfford(item);
        view.buyBg.color = affordable ? Affordable : Unaffordable;
        view.buyLabel.text = owned ? "已拥有" : "购买";

        int id = (int)item.Id;            // 闭包捕获副本
        string itemName = item.Name;
        var priceType = (CurrencyType)item.PriceType;
        if (owned)
            view.buyButton.onClick.AddListener(() => FlyText($"{itemName}已拥有"));
        else
            view.buyButton.onClick.AddListener(() => OnBuyClick(id, itemName, priceDesc, priceType, affordable));
    }

    /// <summary>
    /// 点击「购买」:买得起→弹确认页,确认后 <see cref="DoBuy"/> 购买;买不起→直接飘「xx 不足」,不弹确认。
    /// </summary>
    private void OnBuyClick(int id, string itemName, string priceDesc, CurrencyType priceType, bool affordable)
    {
        if (!affordable)
        {
            FlyText($"{currency.DisplayName(priceType)}不足");
            return;
        }
        Show<ConfirmPanel, ConfirmParam>(new ConfirmParam
        {
            title = "购买确认",
            message = $"确定花费 {priceDesc} 购买\n「{itemName}」吗？",
            confirmText = "购买",
            cancelText = "取消",
            onConfirm = () => DoBuy(id, itemName, priceType),
        });
    }

    /// <summary>真正购买并按结果飘字:成功「xx购买成功！」,失败按原因飘「金币不足 / 已购买 / 背包已满」。</summary>
    private void DoBuy(int id, string itemName, CurrencyType priceType)
    {
        switch (shop.Buy(id))
        {
            case BuyResult.Success:           ShowReward("购买成功", RewardEntry.Item(id, 1)); break;
            case BuyResult.AlreadyOwned:      FlyText($"{itemName}已购买"); break;
            case BuyResult.NotEnoughCurrency: FlyText($"{currency.DisplayName(priceType)}不足"); break;
            case BuyResult.BagFull:           FlyText("背包已满"); break;
            default:                          FlyText("无法购买"); break;
        }
    }

    /// <summary>屏幕中央飘字提示(复用框架飘字系统)。</summary>
    private void FlyText(string msg) => GetService<FlyTextMgr>()?.AddTextAtScreenCenter(msg);

    /// <summary>通用奖励领取弹窗(Top 层,1 秒自动消失)。奖励已由 <see cref="ShopSystem"/> 入账,这里仅展示。</summary>
    private void ShowReward(string title, params RewardEntry[] rewards)
        => Show<RewardClaimPanel, RewardClaimParam>(new RewardClaimParam
        {
            title = title,
            rewards = new System.Collections.Generic.List<RewardEntry>(rewards),
        });
}
