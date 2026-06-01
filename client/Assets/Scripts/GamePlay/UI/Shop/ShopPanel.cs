using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YFramework.Config;
using YOTO;

/// <summary>
/// 商店面板(<see cref="UIEnum.ShopPanel"/>):分类页签 + 3 列卡片网格。
///   顶部:返回(关闭) / 资源金币 / 商人头像
///   中部:当前分类的卡片网格(图片 + 名称 + 价格;绿=买得起,灰=买不起,点击即购买)
///   底部:武器 / 瞄准镜 / 子弹 三个分类页签 + 准备(打开装备界面 <see cref="EquipPanel"/>)
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
    public Button prepareBtn;

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
        if (prepareBtn != null) prepareBtn.onClick.AddListener(OnPrepare);
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
        coinText.text = $"{currency.DisplayName(CurrencyType.Gold)} {currency.Get(CurrencyType.Gold)}\n{currency.DisplayName(CurrencyType.Diamond)} {currency.Get(CurrencyType.Diamond)}";
    }

    // ---------------- 分类页签 ----------------

    private void SelectCategory(ShopCategory cat)
    {
        current = cat;
        SetTabColor(tabWeapon, cat == ShopCategory.Weapon);
        SetTabColor(tabScope, cat == ShopCategory.Scope);
        SetTabColor(tabBullet, cat == ShopCategory.Bullet);
        RebuildGrid();
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
        // 整张卡 = 一个按钮(点击购买),背景色表示买得起/买不起
        var card = new GameObject($"Card_{item.Id}", typeof(RectTransform), typeof(Image), typeof(Button));
        card.transform.SetParent(grid, false); // 尺寸由 GridLayoutGroup 决定
        var bg = card.GetComponent<Image>();
        bg.color = shop.CanAfford(item) ? Affordable : Unaffordable;
        var btn = card.GetComponent<Button>();
        btn.targetGraphic = bg;
        int id = (int)item.Id; // 闭包捕获副本
        btn.onClick.AddListener(() => shop.Buy(id));

        // 图片(上部,占大半)
        var pic = NewChild(card.transform, "Pic", out var picRt);
        picRt.anchorMin = Vector2.zero; picRt.anchorMax = Vector2.one;
        picRt.offsetMin = new Vector2(20, 120); picRt.offsetMax = new Vector2(-20, -20);
        var picImg = pic.AddComponent<Image>();
        picImg.raycastTarget = false; picImg.preserveAspect = true;
        var sprite = !string.IsNullOrEmpty(item.IconPath) ? resMgr.Load<Sprite>(item.IconPath) : null;
        picImg.sprite = sprite; picImg.enabled = sprite != null;

        // 名称(中下)
        var name = NewChild(card.transform, "Name", out var nameRt);
        nameRt.anchorMin = new Vector2(0, 0); nameRt.anchorMax = new Vector2(1, 0); nameRt.pivot = new Vector2(0.5f, 0);
        nameRt.offsetMin = new Vector2(6, 64); nameRt.offsetMax = new Vector2(-6, 116);
        NewText(name, item.Name, 34, TextAlignmentOptions.Center, Color.white);

        // 价格(底部,带货币色)
        var price = NewChild(card.transform, "Price", out var priceRt);
        priceRt.anchorMin = new Vector2(0, 0); priceRt.anchorMax = new Vector2(1, 0); priceRt.pivot = new Vector2(0.5f, 0);
        priceRt.offsetMin = new Vector2(6, 10); priceRt.offsetMax = new Vector2(-6, 60);
        NewText(price, $"{currency.DisplayName((CurrencyType)item.PriceType)} {item.Price}", 30, TextAlignmentOptions.Center, new Color(1f, 0.83f, 0.47f, 1f));
    }

    // ---------------- 准备(打开装备界面)----------------

    private void OnPrepare()
    {
        CloseSelf();        // 打开装备界面前先关闭商店,避免两个全屏界面叠在一起
        Show<EquipPanel>();
    }

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
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = color;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        var f = font != null ? font : TMP_Settings.defaultFontAsset;
        if (f != null) tmp.font = f;
    }
}
