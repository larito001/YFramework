using System.Collections.Generic;
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
    public TextMeshProUGUI coinText;   // 金币数值(图标在预制体胶囊里)
    public TextMeshProUGUI energyText; // 体力数值(图标在预制体胶囊里)

    [Header("分类行(横向滚动内容容器)")]
    public RectTransform weaponRow;
    public RectTransform scopeRow;
    public RectTransform bulletRow;

    [Header("底部")]
    public Button departBtn;

    private static readonly Color OwnedColor = new Color(0.30f, 0.78f, 0.36f, 1f); // 绿:已拥有
    private static readonly Color LockedColor = new Color(0.32f, 0.34f, 0.40f, 1f); // 灰:未拥有
    // 选中黄色描边颜色已烘进 EquipCard 预制体的 Outline,运行时只切 enabled。

    private LoadoutSystem loadout;
    private CurrencySystem currency;
    private StoreMgr store;
    private ResMgr resMgr;
    private EventMgr eventMgr;
    private TMP_FontAsset font;
    private bool busy;
    private bool loadoutDirty; // 装备变化标记:延到 LateUpdate 重建,避免在卡片自身 onClick 里把自己 Destroy 掉破坏 EventSystem

    // 卡片侧视快照已移到全局共享缓存 ModelSnapshotCache(进程级常驻),与商城页等共用同一份,渲过即复用。
    private GameObject cardPrefab; // 装备卡片预制体(Resources/UI/Equip/EquipCard,EquipCardBuilder 生成),运行时 instantiate
    private ResourceHandle<GameObject> cardPrefabHandle; // 持模板句柄到页面销毁释放

    public override void OnLoad()
    {
        loadout = GetService<LoadoutSystem>();
        currency = GetService<CurrencySystem>();
        store = GetService<StoreMgr>();
        resMgr = GetService<ResMgr>();
        eventMgr = GetService<EventMgr>();
        if (coinText != null) font = coinText.font;
        resMgr.LoadHandleAsync<GameObject>("UI/Equip/EquipCard", h => // 卡片预制体(异步)
        {
            if (this == null) { h?.Release(); return; }
            cardPrefabHandle = h;
            cardPrefab = h?.Asset;
            if (cardPrefab == null) Debug.LogError("[EquipPanel] 未找到 EquipCard 预制体,请先执行 Tools/UI/Build EquipCard Prefab(或 Build ALL UI Prefabs)。");
            else RebuildAll(); // 模板就绪后补建
        });

        if (backBtn != null) backBtn.onClick.AddListener(CloseSelf);
        CurrencyIcon.Bind(coinText, CurrencyType.Gold);     // 资源胶囊图标:运行时从 Resources 动态加载(方便换图)
        CurrencyIcon.Bind(energyText, CurrencyType.Energy);
        if (departBtn != null) departBtn.onClick.AddListener(OnDepart);
    }

    public override void OnShow()
    {
        busy = false;
        eventMgr?.Add(YOTOEventType.RefreshLoadout, MarkLoadoutDirty);
        eventMgr?.Add(YOTOEventType.RefreshCurrency, RefreshCoin);
        RebuildAll(); // 首次直接建(不在点击栈内,安全)
        RefreshCoin();
    }

    public override void OnHide()
    {
        eventMgr?.Remove(YOTOEventType.RefreshLoadout, MarkLoadoutDirty);
        eventMgr?.Remove(YOTOEventType.RefreshCurrency, RefreshCoin);
        // 卡片快照不在此释放:已移到全局共享缓存 ModelSnapshotCache(进程级常驻),装备页/商城页跨面板复用,渲过即留。
    }

    /// <summary>装备变化先打标记,延到 LateUpdate 再重建——避免点击卡片时同步重建把刚点的卡销毁、破坏 EventSystem。</summary>
    private void MarkLoadoutDirty() => loadoutDirty = true;

    private void LateUpdate()
    {
        if (!loadoutDirty) return;
        loadoutDirty = false;
        RebuildAll();
    }

    public override void OnResize() { }

    private void RefreshCoin()
    {
        if (currency == null) return;
        if (coinText != null) coinText.text = currency.Get(CurrencyType.Gold).ToString();     // 只填数值,图标在胶囊里
        if (energyText != null) energyText.text = currency.Get(CurrencyType.Energy).ToString();
    }

    // ---------------- 卡片网格 ----------------

    private void OnDestroy() => cardPrefabHandle?.Release(); // 释放卡片模板句柄

    private void RebuildAll()
    {
        // 不在这里清快照缓存:重建/选中只复用已渲好的贴图,避免每次点选重渲一遍
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
        if (cardPrefab == null) return;
        bool owned = loadout.IsOwned((int)item.Id);
        var go = Instantiate(cardPrefab);
        go.transform.SetParent(row, false);
        go.name = $"Card_{item.Id}";
        var view = go.GetComponent<EquipCardView>();
        if (view == null) { Destroy(go); return; }

        view.bg.color = owned ? OwnedColor : LockedColor;
        view.button.interactable = owned; // 未拥有不可选
        int id = (int)item.Id;
        view.button.onClick.AddListener(() => loadout.Select(cat, id));
        view.outline.enabled = isSelected; // 选中:黄色描边(预制体里预置好,切 enabled)

        // 图片:渲染出的 3D 道具侧视快照(全局共享缓存,跨面板复用),无模型回退 2D 图标(异步)。预制体里 pic 已 preserveAspect。
        ModelSnapshotCache.BindCardImageAsync(view.pic, item.ModelPath, item.IconPath, resMgr);
        if (view.qualityFrame != null) view.qualityFrame.color = ItemQualityPalette.FrameColor(item.Quality); // 品质框颜色 = icon 背景
        // 右上角小「i」按钮弹道具描述(点卡片/图标仍走选中出战,避免切换时误触描述)
        ItemIconDescButton.AttachInfoBadge((RectTransform)go.transform, (int)item.Id, font);

        view.nameText.text = item.Name;
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
        tmp.fontSize = UITheme.Font(size);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        var f = font != null ? font : TMP_Settings.defaultFontAsset;
        if (f != null) tmp.font = f;
    }
}
