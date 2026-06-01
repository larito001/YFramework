using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YFramework.Config;
using YOTO;

/// <summary>
/// 装备界面(<see cref="UIEnum.EquipPanel"/>):从主界面/商店「准备」进入,选好出战装备后点「出发」进游戏。
///   顶部:返回(关闭) / 资源金币
///   中部:枪械 / 瞄准镜 / 子弹 三个横向滚动行,每行平铺该分类装备卡
///         绿=已拥有(可选),灰=未拥有(锁),当前选中项加黄色描边
///   底部:出发(读/建存档槽后进 Home)
/// 拥有/选中数据在 <see cref="LoadoutSystem"/>;卡片运行时按目录构建,RefreshLoadout 时整体刷新。
/// 预制体外壳由 <c>Tools/UI/Build EquipPanel Prefab</c> 生成。
/// </summary>
public class EquipPanel : UIPageBase
{
    [Header("顶部")]
    public Button backBtn;
    public TextMeshProUGUI coinText;

    [Header("分类行(横向滚动内容容器)")]
    public RectTransform weaponRow;
    public RectTransform scopeRow;
    public RectTransform bulletRow;

    [Header("底部")]
    public Button departBtn;

    private static readonly Color OwnedColor = new Color(0.30f, 0.78f, 0.36f, 1f); // 绿:已拥有
    private static readonly Color LockedColor = new Color(0.32f, 0.34f, 0.40f, 1f); // 灰:未拥有
    private static readonly Color SelectBorder = new Color(1f, 0.85f, 0.2f, 1f);    // 选中描边

    private LoadoutSystem loadout;
    private CurrencySystem currency;
    private StoreMgr store;
    private ResMgr resMgr;
    private EventMgr eventMgr;
    private TMP_FontAsset font;
    private bool busy;

    public override void OnLoad()
    {
        loadout = GetService<LoadoutSystem>();
        currency = GetService<CurrencySystem>();
        store = GetService<StoreMgr>();
        resMgr = GetService<ResMgr>();
        eventMgr = GetService<EventMgr>();
        if (coinText != null) font = coinText.font;

        if (backBtn != null) backBtn.onClick.AddListener(CloseSelf);
        if (departBtn != null) departBtn.onClick.AddListener(OnDepart);
    }

    public override void OnShow()
    {
        busy = false;
        eventMgr?.Add(YOTOEventType.RefreshLoadout, RebuildAll);
        eventMgr?.Add(YOTOEventType.RefreshCurrency, RefreshCoin);
        RebuildAll();
        RefreshCoin();
    }

    public override void OnHide()
    {
        eventMgr?.Remove(YOTOEventType.RefreshLoadout, RebuildAll);
        eventMgr?.Remove(YOTOEventType.RefreshCurrency, RefreshCoin);
    }

    public override void OnResize() { }

    private void RefreshCoin()
    {
        if (coinText == null || currency == null) return;
        coinText.text = $"{currency.DisplayName(CurrencyType.Gold)} {currency.Get(CurrencyType.Gold)}\n{currency.DisplayName(CurrencyType.Energy)} {currency.Get(CurrencyType.Energy)}";
    }

    // ---------------- 卡片网格 ----------------

    private void RebuildAll()
    {
        BuildRow(weaponRow, ShopCategory.Weapon);
        BuildRow(scopeRow, ShopCategory.Scope);
        BuildRow(bulletRow, ShopCategory.Bullet);
    }

    private void BuildRow(RectTransform row, ShopCategory cat)
    {
        if (row == null) return;
        for (int i = row.childCount - 1; i >= 0; i--) Destroy(row.GetChild(i).gameObject);

        var items = loadout.CategoryItems(cat);
        int sel = loadout.GetSelected(cat);
        for (int i = 0; i < items.Count; i++) BuildCard(row, items[i], cat, items[i].Id == (uint)sel);
    }

    private void BuildCard(RectTransform row, Item item, ShopCategory cat, bool isSelected)
    {
        bool owned = loadout.IsOwned((int)item.Id);

        var card = new GameObject($"Card_{item.Id}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        card.transform.SetParent(row, false);
        var le = card.GetComponent<LayoutElement>();
        le.preferredWidth = 320; le.preferredHeight = 340;
        var bg = card.GetComponent<Image>();
        bg.color = owned ? OwnedColor : LockedColor;
        var btn = card.GetComponent<Button>();
        btn.targetGraphic = bg;
        btn.interactable = owned; // 未拥有不可选
        int id = (int)item.Id;
        btn.onClick.AddListener(() => loadout.Select(cat, id));

        if (isSelected)
        {
            var ol = card.AddComponent<Outline>(); // 选中:黄色描边
            ol.effectColor = SelectBorder;
            ol.effectDistance = new Vector2(6, 6);
        }

        // 图片(上部)
        var pic = NewChild(card.transform, "Pic", out var picRt);
        picRt.anchorMin = Vector2.zero; picRt.anchorMax = Vector2.one;
        picRt.offsetMin = new Vector2(16, 90); picRt.offsetMax = new Vector2(-16, -16);
        var picImg = pic.AddComponent<Image>();
        picImg.raycastTarget = false; picImg.preserveAspect = true;
        var sprite = !string.IsNullOrEmpty(item.IconPath) ? resMgr.Load<Sprite>(item.IconPath) : null;
        picImg.sprite = sprite; picImg.enabled = sprite != null;

        // 名称(底部)
        var name = NewChild(card.transform, "Name", out var nameRt);
        nameRt.anchorMin = new Vector2(0, 0); nameRt.anchorMax = new Vector2(1, 0); nameRt.pivot = new Vector2(0.5f, 0);
        nameRt.offsetMin = new Vector2(6, 12); nameRt.offsetMax = new Vector2(-6, 78);
        NewText(name, item.Name, 32, Color.white);
    }

    // ---------------- 出发(进入游戏)----------------

    private void OnDepart()
    {
        if (busy) return;
        busy = true;
        if (store == null)
        {
            if (!TryConsumeEnergy()) { busy = false; return; }
            GetService<YSceneManager>().SwitchScene(YSceneType.Home);
            return;
        }
        store.WhenSlotsReady(() =>
        {
            if (store.Slots.Count == 0) store.CreateSlot();
            else if (store.ActiveSlot == 0) store.SetActiveSlot(store.Slots[store.Slots.Count - 1].id);
            // 体力在 LoadAll 之后扣:LoadAll 会用存档槽数据覆盖内存余额,先扣会被覆盖掉
            store.LoadAll(() =>
            {
                if (!TryConsumeEnergy()) { busy = false; return; } // 体力不足:留在装备界面,不进图
                GetService<YSceneManager>().SwitchScene(YSceneType.Home);
            });
        });
    }

    /// <summary>进图消耗 1 点体力:足够则扣减 + 立即写盘并返回 true;不足则飘字提示并返回 false(不进图)。</summary>
    private bool TryConsumeEnergy()
    {
        if (currency != null && currency.TrySpend(CurrencyType.Energy, 1))
        {
            currency.Save(); // 关键节点主动写盘,保证体力扣减落地(StoreMgr 协程异步写)
            return true;
        }
        GetService<FlyTextMgr>()?.AddTextAtScreenCenter("体力不足，无法出发");
        return false;
    }

    // ---------------- 工具 ----------------

    private GameObject NewChild(Transform parent, string name, out RectTransform rt)
    {
        var go = new GameObject(name, typeof(RectTransform));
        rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return go;
    }

    private void NewText(GameObject go, string text, float size, Color color)
    {
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        var f = font != null ? font : TMP_Settings.defaultFontAsset;
        if (f != null) tmp.font = f;
    }
}
