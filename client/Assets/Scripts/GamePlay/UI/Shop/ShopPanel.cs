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

    private static readonly Color Affordable = new Color(0.30f, 0.78f, 0.36f, 1f); // 绿:买得起
    private static readonly Color Unaffordable = new Color(0.32f, 0.34f, 0.40f, 1f); // 灰:买不起
    private static readonly Color TabOn = new Color(0.30f, 0.55f, 0.85f, 1f);
    private static readonly Color TabOff = new Color(0.25f, 0.27f, 0.33f, 1f);

    private ShopSystem shop;
    private CurrencySystem currency;
    private LoadoutSystem loadout; // 判断装备是否已拥有(已拥有的按钮置灰)
    private ResMgr resMgr;
    private EventMgr eventMgr;
    private TMP_FontAsset font;

    private ShopCategory current = ShopCategory.Weapon;
    private WeaponModelPreview modelPreview;  // 模型转台(占原商人头像位,扩大到约 2/5 屏);三类(枪/镜/弹)点卡切换展示
    private int selectedId;                   // 当前展示/选中的物品 id(随分类切换)
    private readonly List<(int id, GameObject card)> previewCards = new(); // 当前分类带模型的卡(就地切换描边,避免点击时重建销毁自身)

    public override void OnLoad()
    {
        shop = GetService<ShopSystem>();
        currency = GetService<CurrencySystem>();
        loadout = GetService<LoadoutSystem>();
        resMgr = GetService<ResMgr>();
        eventMgr = GetService<EventMgr>();
        if (coinText != null) font = coinText.font; // 复用外壳的中文字体给运行时卡片

        if (backBtn != null) backBtn.onClick.AddListener(CloseSelf);
        if (tabWeapon != null) tabWeapon.onClick.AddListener(() => SelectCategory(ShopCategory.Weapon));
        if (tabScope != null) tabScope.onClick.AddListener(() => SelectCategory(ShopCategory.Scope));
        if (tabBullet != null) tabBullet.onClick.AddListener(() => SelectCategory(ShopCategory.Bullet));

        modelPreview = new WeaponModelPreview(CreatePreviewHost(), resMgr);
    }

    /// <summary>模型展示框:占原「商人头像」位并扩大到约屏幕上方 2/5;同时把下方网格下压让出空间。</summary>
    private RectTransform CreatePreviewHost()
    {
        // 网格(Scroll)下压:只占下半屏(顶部让给展示区,底部留页签)
        if (grid != null && grid.parent != null && grid.parent.parent is RectTransform scroll)
        {
            scroll.anchorMin = new Vector2(0f, 0f);
            scroll.anchorMax = new Vector2(1f, 0.48f);
            scroll.offsetMin = new Vector2(30f, 240f);
            scroll.offsetMax = new Vector2(-30f, 0f);
        }

        if (merchantAvatar != null)
        {
            merchantAvatar.enabled = false; // 让位给模型展示
            var ar = (RectTransform)merchantAvatar.transform;
            ar.anchorMin = new Vector2(0.06f, 0.50f); // 上方 ~2/5 屏(50%~93% 高度)
            ar.anchorMax = new Vector2(0.94f, 0.93f);
            ar.offsetMin = Vector2.zero; ar.offsetMax = Vector2.zero;

            var go = new GameObject("ModelPreview", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(merchantAvatar.transform, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; // 填满展示区
            return rt;
        }

        // 兜底:顶部一大块
        var host = new GameObject("ModelPreview", typeof(RectTransform));
        host.transform.SetParent(transform, false);
        var hr = (RectTransform)host.transform;
        hr.anchorMin = new Vector2(0.06f, 0.50f);
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
    }

    public override void OnHide()
    {
        eventMgr?.Remove(YOTOEventType.RefreshCurrency, OnCurrencyChanged);
        eventMgr?.Remove(YOTOEventType.RefreshLoadout, OnLoadoutChanged);
        modelPreview?.SetActive(false);
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

    private const string SelectFrameName = "SelectFrame";

    /// <summary>给卡片加/去金色选中边框(就地,不销毁卡片)。
    /// 用 4 条细边拼出"框",而不是 Outline 组件——后者在本项目渲染下会糊成一整片金色底图,达不到"金色框"效果。</summary>
    private static void ApplyOutline(GameObject card, bool on)
    {
        if (card == null) return;
        var existing = card.transform.Find(SelectFrameName);
        if (!on)
        {
            if (existing != null) Destroy(existing.gameObject);
            return;
        }
        if (existing != null) return; // 已有边框

        var frameGo = new GameObject(SelectFrameName, typeof(RectTransform));
        var frame = (RectTransform)frameGo.transform;
        frame.SetParent(card.transform, false);
        frame.anchorMin = Vector2.zero; frame.anchorMax = Vector2.one;
        frame.offsetMin = Vector2.zero; frame.offsetMax = Vector2.zero;
        frame.SetAsLastSibling(); // 边框画在卡片内容之上

        const float t = 8f; // 边宽
        var gold = new Color(1f, 0.85f, 0.2f, 1f);
        AddEdge(frame, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -t), new Vector2(0, 0), gold); // 上
        AddEdge(frame, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, t), gold);  // 下
        AddEdge(frame, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0), new Vector2(t, 0), gold);  // 左
        AddEdge(frame, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-t, 0), new Vector2(0, 0), gold); // 右
    }

    /// <summary>在 frame 下加一条贴边的纯色 Image(选中框的一条边)。</summary>
    private static void AddEdge(RectTransform parent, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax, Color color)
    {
        var go = new GameObject("Edge", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.offsetMin = offMin; rt.offsetMax = offMax;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
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
        var img = btn.targetGraphic as Image;
        if (img != null) img.color = on ? TabOn : TabOff;
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
        // 卡片容器:中性卡底(不再整卡点击购买;改为卡内「购买」按钮 → 确认弹窗 → 购买)。
        // 各区块按 cell(550×560)从下往上排,文本带高放大到能容下 ×2 字号(否则名称/价格会被裁掉看不见)。
        var card = new GameObject($"Card_{item.Id}", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(grid, false); // 尺寸由 GridLayoutGroup 决定
        var bg = card.GetComponent<Image>();
        bg.color = new Color(0.18f, 0.20f, 0.25f, 1f);
        bg.raycastTarget = false;

        // 任意带模型的卡(枪/镜/弹):整卡可点 = 选中它并在上方展示其模型(底部「购买」按钮在上层,各点各的)
        if (!string.IsNullOrEmpty(item.ModelPath))
        {
            bg.raycastTarget = true;
            var cardBtn = card.AddComponent<Button>();
            cardBtn.transition = Selectable.Transition.None; // 不改卡底色
            cardBtn.targetGraphic = bg;
            int sid = (int)item.Id;
            cardBtn.onClick.AddListener(() => SelectPreview(sid));

            previewCards.Add((sid, card));
            ApplyOutline(card, sid == selectedId); // 当前选中:金色边框
        }

        // 图片(上部,占大半:y 270 → 顶):与装备页一致,用渲染出的 3D 道具侧视快照(全局共享缓存,跨面板复用已渲图),
        // 无模型才回退 2D 图标。统一走 Image + preserveAspect:正方形快照按比例居中,不被卡片图框拉伸。
        var pic = NewChild(card.transform, "Pic", out var picRt);
        picRt.anchorMin = Vector2.zero; picRt.anchorMax = Vector2.one;
        picRt.offsetMin = new Vector2(20, 270); picRt.offsetMax = new Vector2(-20, -20);
        var picImg = pic.AddComponent<Image>();
        picImg.raycastTarget = false; picImg.preserveAspect = true;
        var sprite = ModelSnapshotCache.CardSprite(item.ModelPath, resMgr)            // 全局共享缓存复用的侧视快照
                     ?? (!string.IsNullOrEmpty(item.IconPath) ? resMgr.Load<Sprite>(item.IconPath) : null); // 无模型回退 2D 图标
        picImg.sprite = sprite; picImg.enabled = sprite != null;

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

        // 购买按钮(底部 y 20..115):已拥有=灰「已拥有」/ 买得起=绿 / 买不起=灰。始终可点——给对应飘字反馈。
        bool owned = loadout != null && item.ShopCategory != 0 && loadout.IsOwned((int)item.Id);
        bool affordable = !owned && shop.CanAfford(item);
        var buyGo = NewChild(card.transform, "Buy", out var buyRt);
        buyRt.anchorMin = new Vector2(0, 0); buyRt.anchorMax = new Vector2(1, 0); buyRt.pivot = new Vector2(0.5f, 0);
        buyRt.offsetMin = new Vector2(20, 20); buyRt.offsetMax = new Vector2(-20, 115);
        var buyImg = buyGo.AddComponent<Image>();
        buyImg.color = affordable ? Affordable : Unaffordable; // 已拥有/买不起都用灰
        var buyBtn = buyGo.AddComponent<Button>();
        buyBtn.targetGraphic = buyImg;

        var label = NewChild(buyGo.transform, "Label", out var labelRt);
        labelRt.anchorMin = Vector2.zero; labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero; labelRt.offsetMax = Vector2.zero;
        NewText(label, owned ? "已拥有" : "购买", 28, TextAlignmentOptions.Center, Color.white);

        int id = (int)item.Id;            // 闭包捕获副本
        string itemName = item.Name;
        var priceType = (CurrencyType)item.PriceType;
        if (owned)
            buyBtn.onClick.AddListener(() => FlyText($"{itemName}已拥有"));
        else
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
