using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YFramework.Config;
using YOTO;

/// <summary>
/// 选择关卡界面(<see cref="UIEnum.MapSelectPanel"/>):主界面点「准备」先进这里,选好关卡再进装备界面。
///   顶部:返回(关闭) / 资源金币
///   中部:关卡卡片网格(2 列),每张 = 预览图 + 「第N关 名称」;未解锁的盖灰罩「未解锁」且不可点
/// 关卡走配表系统:读 <see cref="ConfigManager.mapConfig"/>(map.xlsx → Map.bytes),按 SortPriority 排序;
/// 解锁与否由配表 unlocked 列决定。点已解锁关卡 → <see cref="MapSystem.Select"/> 记选中 → 打开装备界面。
/// 预制体外壳由 <c>Tools/UI/Build MapSelectPanel Prefab</c> 生成。
/// </summary>
public class MapSelectPanel : UIPageBase
{
    [Header("顶部")]
    public Button backBtn;
    public TextMeshProUGUI coinText;

    [Header("网格(GridLayoutGroup 容器)")]
    public RectTransform grid;

    private static readonly Color CardFrame = new Color(0.97f, 0.97f, 1f, 1f);
    private static readonly Color PreviewBox = new Color(0.62f, 0.66f, 0.74f, 1f);  // 预览图缺失时的占位底
    private static readonly Color TitleBar = new Color(0.96f, 0.96f, 0.99f, 0.9f);
    private static readonly Color TitleText = new Color(0.25f, 0.24f, 0.30f, 1f);
    private static readonly Color LockMask = new Color(0.18f, 0.18f, 0.20f, 0.72f);  // 未解锁灰罩
    private static readonly Color LockText = Color.white;

    private ConfigManager config;
    private CurrencySystem currency;
    private MapSystem maps;
    private ResMgr resMgr;
    private EventMgr eventMgr;
    private TMP_FontAsset font;

    private readonly List<Map> entries = new List<Map>();

    public override void OnLoad()
    {
        config = GetService<ConfigManager>();
        currency = GetService<CurrencySystem>();
        maps = GetService<MapSystem>();
        resMgr = GetService<ResMgr>();
        eventMgr = GetService<EventMgr>();
        if (coinText != null) font = coinText.font;

        if (backBtn != null) backBtn.onClick.AddListener(CloseSelf);
    }

    public override void OnShow()
    {
        eventMgr?.Add(YOTOEventType.RefreshCurrency, RefreshCoin);
        ReloadEntries();
        RebuildGrid();
        RefreshCoin();
    }

    public override void OnHide()
    {
        eventMgr?.Remove(YOTOEventType.RefreshCurrency, RefreshCoin);
    }

    public override void OnResize() { }

    private void RefreshCoin()
    {
        if (coinText == null || currency == null) return;
        coinText.text = currency.Get(CurrencyType.Gold).ToString();
    }

    private void ReloadEntries()
    {
        entries.Clear();
        if (config == null) return;
        foreach (var kv in config.mapConfig.items) entries.Add(kv.Value);
        entries.Sort((a, b) => a.SortPriority.CompareTo(b.SortPriority));
    }

    private void RebuildGrid()
    {
        if (grid == null) return;
        for (int i = grid.childCount - 1; i >= 0; i--) Destroy(grid.GetChild(i).gameObject);
        for (int i = 0; i < entries.Count; i++) BuildCard(entries[i], i + 1); // i+1 = 第N关
    }

    private void BuildCard(Map map, int index)
    {
        bool unlocked = map.Unlocked != 0;

        // 卡片底框(尺寸由 GridLayoutGroup 决定) + 点击按钮
        var card = new GameObject($"Map_{map.Id}", typeof(RectTransform), typeof(Image), typeof(Button));
        card.transform.SetParent(grid, false);
        var bg = card.GetComponent<Image>();
        bg.color = CardFrame;
        var btn = card.GetComponent<Button>();
        btn.targetGraphic = bg;
        btn.interactable = unlocked; // 未解锁不可点
        uint id = map.Id;
        btn.onClick.AddListener(() => OnSelect(id));

        // 预览图(铺满,底部留出标题条)
        var pic = NewChild(card.transform, "Preview", out var picRt);
        picRt.anchorMin = Vector2.zero; picRt.anchorMax = Vector2.one;
        picRt.offsetMin = new Vector2(12, 12); picRt.offsetMax = new Vector2(-12, -12);
        var picImg = pic.AddComponent<Image>();
        picImg.raycastTarget = false; picImg.preserveAspect = true;
        var sprite = !string.IsNullOrEmpty(map.IconPath) ? resMgr.Load<Sprite>(map.IconPath) : null;
        if (sprite != null) { picImg.sprite = sprite; picImg.color = Color.white; }
        else picImg.color = PreviewBox; // 缺预览图用色块占位

        // 标题条:第N关 名称(顶部)
        var bar = NewChild(card.transform, "TitleBar", out var barRt);
        barRt.anchorMin = new Vector2(0, 1); barRt.anchorMax = new Vector2(1, 1); barRt.pivot = new Vector2(0.5f, 1);
        barRt.anchoredPosition = new Vector2(0, -16); barRt.sizeDelta = new Vector2(-24, 70);
        var barImg = bar.AddComponent<Image>();
        barImg.color = TitleBar; barImg.raycastTarget = false;
        var label = NewChild(bar.transform, "Label", out var labelRt);
        labelRt.anchorMin = Vector2.zero; labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(16, 0); labelRt.offsetMax = new Vector2(-16, 0);
        NewText(label, $"第{index}关  {map.Name}", 34, TitleText, TextAlignmentOptions.Left);

        // 未解锁:灰罩 + 「未解锁」
        if (!unlocked)
        {
            var mask = NewChild(card.transform, "Lock", out var maskRt);
            maskRt.anchorMin = Vector2.zero; maskRt.anchorMax = Vector2.one; maskRt.offsetMin = Vector2.zero; maskRt.offsetMax = Vector2.zero;
            var maskImg = mask.AddComponent<Image>();
            maskImg.color = LockMask; maskImg.raycastTarget = false;
            // 文字要放在子物体上:遮罩自身已有 Image(Graphic),一个 GameObject 只能挂一个 Graphic
            var lockLabel = NewChild(mask.transform, "Label", out var lockLabelRt);
            lockLabelRt.anchorMin = Vector2.zero; lockLabelRt.anchorMax = Vector2.one;
            lockLabelRt.offsetMin = Vector2.zero; lockLabelRt.offsetMax = Vector2.zero;
            NewText(lockLabel, "未解锁", 44, LockText, TextAlignmentOptions.Center);
        }
    }

    /// <summary>选中关卡:记到 <see cref="MapSystem"/>,然后进入装备界面(返回时会回到本界面)。</summary>
    private void OnSelect(uint id)
    {
        maps?.Select(id);
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

    private void NewText(GameObject go, string text, float size, Color color, TextAlignmentOptions align)
    {
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.alignment = align; tmp.color = color;
        tmp.raycastTarget = false; tmp.enableWordWrapping = false; tmp.overflowMode = TextOverflowModes.Ellipsis;
        var f = font != null ? font : TMP_Settings.defaultFontAsset;
        if (f != null) tmp.font = f;
    }
}
