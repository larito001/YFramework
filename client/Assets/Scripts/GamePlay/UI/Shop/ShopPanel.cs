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
    public TextMeshProUGUI coinText;
    public Image merchantAvatar;

    [Header("网格")]
    public RectTransform grid; // GridLayoutGroup 容器(滚动视图内)

    [Header("底部页签")]
    public Button tabWeapon;
    public Button tabScope;
    public Button tabBullet;

    private static readonly Color Affordable = new Color(0.30f, 0.78f, 0.36f, 1f); // 绿:买得起
    private static readonly Color Unaffordable = new Color(0.32f, 0.34f, 0.40f, 1f); // 灰:买不起
    private static readonly Color TabOn = new Color(0.30f, 0.55f, 0.85f, 1f);
    private static readonly Color TabOff = new Color(0.25f, 0.27f, 0.33f, 1f);

    private ShopSystem shop;
    private CurrencySystem currency;
    private ResMgr resMgr;
    private EventMgr eventMgr;
    private TMP_FontAsset font;

    private ShopCategory current = ShopCategory.Weapon;
    private WeaponModelPreview weaponPreview; // 右上角武器模型转台(武器页签;点卡片图片切换)

    public override void OnLoad()
    {
        shop = GetService<ShopSystem>();
        currency = GetService<CurrencySystem>();
        resMgr = GetService<ResMgr>();
        eventMgr = GetService<EventMgr>();
        if (coinText != null) font = coinText.font; // 复用外壳的中文字体给运行时卡片

        if (backBtn != null) backBtn.onClick.AddListener(CloseSelf);
        if (tabWeapon != null) tabWeapon.onClick.AddListener(() => SelectCategory(ShopCategory.Weapon));
        if (tabScope != null) tabScope.onClick.AddListener(() => SelectCategory(ShopCategory.Scope));
        if (tabBullet != null) tabBullet.onClick.AddListener(() => SelectCategory(ShopCategory.Bullet));

        weaponPreview = new WeaponModelPreview(CreatePreviewHost(), resMgr);
    }

    /// <summary>在右上角放一个武器模型预览框(返回其宿主 RectTransform)。</summary>
    private RectTransform CreatePreviewHost()
    {
        var host = new GameObject("WeaponPreview", typeof(RectTransform));
        host.transform.SetParent(transform, false);
        var hr = (RectTransform)host.transform;
        hr.anchorMin = hr.anchorMax = hr.pivot = new Vector2(1f, 1f); // 右上角
        hr.sizeDelta = new Vector2(320f, 320f);
        hr.anchoredPosition = new Vector2(-30f, -190f);               // 让开顶部金币条
        return hr;
    }

    public override void OnShow()
    {
        eventMgr?.Add(YOTOEventType.RefreshCurrency, OnCurrencyChanged);
        SelectCategory(ShopCategory.Weapon);
        RefreshCoin();
    }

    public override void OnHide()
    {
        eventMgr?.Remove(YOTOEventType.RefreshCurrency, OnCurrencyChanged);
        weaponPreview?.SetActive(false);
    }

    public override void OnResize() { }

    private void OnCurrencyChanged()
    {
        RefreshCoin();
        RebuildGrid(); // 重建以刷新各卡绿/灰
    }

    // ---------------- 顶部 ----------------

    private void RefreshCoin()
    {
        if (coinText == null || currency == null) return;
        coinText.text = $"{currency.DisplayName(CurrencyType.Gold)} {currency.Get(CurrencyType.Gold)}\n{currency.DisplayName(CurrencyType.Energy)} {currency.Get(CurrencyType.Energy)}";
    }

    // ---------------- 分类页签 ----------------

    private void SelectCategory(ShopCategory cat)
    {
        current = cat;
        SetTabColor(tabWeapon, cat == ShopCategory.Weapon);
        SetTabColor(tabScope, cat == ShopCategory.Scope);
        SetTabColor(tabBullet, cat == ShopCategory.Bullet);
        RebuildGrid();
        UpdateWeaponPreview();
    }

    /// <summary>武器页签:转台展示该分类第一件武器的模型(点卡片图片可切换);其它页签收起转台。</summary>
    private void UpdateWeaponPreview()
    {
        if (weaponPreview == null) return;
        if (current != ShopCategory.Weapon) { weaponPreview.SetActive(false); return; }

        weaponPreview.SetActive(true);
        string path = null;
        var list = shop.CatalogOf(ShopCategory.Weapon);
        for (int i = 0; i < list.Count; i++)
            if (!string.IsNullOrEmpty(list[i].ModelPath)) { path = list[i].ModelPath; break; }
        weaponPreview.Show(path);
    }

    private static void SetTabColor(Button btn, bool on)
    {
        if (btn == null) return;
        var img = btn.targetGraphic as Image;
        if (img != null) img.color = on ? TabOn : TabOff;
    }

    // ---------------- 卡片网格 ----------------

    private void RebuildGrid()
    {
        if (grid == null) return;
        for (int i = grid.childCount - 1; i >= 0; i--) Destroy(grid.GetChild(i).gameObject);

        var list = shop.CatalogOf(current);
        for (int i = 0; i < list.Count; i++) BuildCard(list[i]);
    }

    private void BuildCard(Item item)
    {
        // 卡片容器:中性卡底(不再整卡点击购买;改为卡内「购买」按钮 → 确认弹窗 → 购买)。
        // 各区块按 cell(550×560)从下往上排,文本带高放大到能容下 ×2 字号(否则名称/价格会被裁掉看不见)。
        var card = new GameObject($"Card_{item.Id}", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(grid, false); // 尺寸由 GridLayoutGroup 决定
        var bg = card.GetComponent<Image>();
        bg.color = new Color(0.18f, 0.20f, 0.25f, 1f);
        bg.raycastTarget = false;

        // 图片(上部,占大半:y 270 → 顶)
        var pic = NewChild(card.transform, "Pic", out var picRt);
        picRt.anchorMin = Vector2.zero; picRt.anchorMax = Vector2.one;
        picRt.offsetMin = new Vector2(20, 270); picRt.offsetMax = new Vector2(-20, -20);
        var picImg = pic.AddComponent<Image>();
        picImg.raycastTarget = false; picImg.preserveAspect = true;
        var sprite = !string.IsNullOrEmpty(item.IconPath) ? resMgr.Load<Sprite>(item.IconPath) : null;
        picImg.sprite = sprite; picImg.enabled = sprite != null;

        // 武器卡:点图片在右上角转台预览该枪的模型
        if (current == ShopCategory.Weapon && !string.IsNullOrEmpty(item.ModelPath) && picImg.enabled)
        {
            picImg.raycastTarget = true;
            var picBtn = pic.AddComponent<Button>();
            picBtn.targetGraphic = picImg;
            string modelPath = item.ModelPath;
            picBtn.onClick.AddListener(() => weaponPreview?.Show(modelPath));
        }

        // 名称(y 196..266,带高 70 容下 30pt×2)
        var name = NewChild(card.transform, "Name", out var nameRt);
        nameRt.anchorMin = new Vector2(0, 0); nameRt.anchorMax = new Vector2(1, 0); nameRt.pivot = new Vector2(0.5f, 0);
        nameRt.offsetMin = new Vector2(6, 196); nameRt.offsetMax = new Vector2(-6, 266);
        NewText(name, item.Name, 30, TextAlignmentOptions.Center, Color.white);

        // 价格(y 130..190,带高 60 容下 26pt×2,带货币色)
        var price = NewChild(card.transform, "Price", out var priceRt);
        priceRt.anchorMin = new Vector2(0, 0); priceRt.anchorMax = new Vector2(1, 0); priceRt.pivot = new Vector2(0.5f, 0);
        priceRt.offsetMin = new Vector2(6, 130); priceRt.offsetMax = new Vector2(-6, 190);
        string priceDesc = $"{currency.DisplayName((CurrencyType)item.PriceType)} {item.Price}";
        NewText(price, priceDesc, 26, TextAlignmentOptions.Center, new Color(1f, 0.83f, 0.47f, 1f));

        // 购买按钮(底部 y 20..115):绿=买得起 / 灰=买不起。始终可点——买不起也要点出「金币不足」飘字反馈(见需求)。
        bool affordable = shop.CanAfford(item);
        var buyGo = NewChild(card.transform, "Buy", out var buyRt);
        buyRt.anchorMin = new Vector2(0, 0); buyRt.anchorMax = new Vector2(1, 0); buyRt.pivot = new Vector2(0.5f, 0);
        buyRt.offsetMin = new Vector2(20, 20); buyRt.offsetMax = new Vector2(-20, 115);
        var buyImg = buyGo.AddComponent<Image>();
        buyImg.color = affordable ? Affordable : Unaffordable;
        var buyBtn = buyGo.AddComponent<Button>();
        buyBtn.targetGraphic = buyImg;

        var label = NewChild(buyGo.transform, "Label", out var labelRt);
        labelRt.anchorMin = Vector2.zero; labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero; labelRt.offsetMax = Vector2.zero;
        NewText(label, "购买", 28, TextAlignmentOptions.Center, Color.white);

        int id = (int)item.Id;            // 闭包捕获副本
        string itemName = item.Name;
        var priceType = (CurrencyType)item.PriceType;
        buyBtn.onClick.AddListener(() => OnBuyClick(id, itemName, priceDesc, priceType, affordable));
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

    // ---------------- 工具 ----------------

    private GameObject NewChild(Transform parent, string name, out RectTransform rt)
    {
        var go = new GameObject(name, typeof(RectTransform));
        rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return go;
    }

    private void NewText(GameObject go, string text, float size, TextAlignmentOptions align, Color color)
    {
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = UITheme.Font(size);
        tmp.alignment = align;
        tmp.color = color;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        var f = font != null ? font : TMP_Settings.defaultFontAsset;
        if (f != null) tmp.font = f;
    }
}
