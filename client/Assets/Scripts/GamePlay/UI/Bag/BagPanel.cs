using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YOTO;

/// <summary>
/// 网格空间背包面板(<see cref="UIPageBase"/>,注册为 <see cref="UIEnum.BagPanel"/>)。
/// 把 <see cref="BagSystem"/> 的网格背包画成一张 W×H 的格盘:每个物品按有效占格尺寸显示成一块矩形。
/// 交互:左键点击=使用,右键点击=原地旋转,拖拽=移动。监听 <see cref="YOTOEventType.RefreshBagList"/> 整体重绘。
///
/// 坐标:gridRoot 轴心取左上(0,1)且居中于窗口;第 (x,y) 格左上角 anchoredPosition =
/// (x*cell + gap/2, -(y*cell) - gap/2)。拖放命中即用此式反算目标格。
///
/// 预制体(BagPrefabBuilder 生成):窗口下含 GridRoot(空 RectTransform)、整理/关闭按钮、容量文本;
/// 格背景与物品控件均在运行时按背包尺寸生成。
/// </summary>
public class BagPanel : UIPageBase
{
    [Header("引用")]
    public RectTransform gridRoot;
    public GameObject itemWidgetPrefab;
    public Button sortBtn;
    public Button closeBtn;
    public TextMeshProUGUI capacityText;

    [Header("布局")]
    public float cellSize = 80f;
    public float cellGap = 4f;
    public Color cellColor = new Color(1f, 1f, 1f, 0.06f);

    private BagSystem bagSystem;
    private ResMgr resMgr;
    private EventMgr eventMgr;

    private readonly List<GameObject> cellBgs = new List<GameObject>();
    private readonly Dictionary<int, BagItemWidget> widgets = new Dictionary<int, BagItemWidget>();
    private readonly Dictionary<string, Sprite> iconCache = new Dictionary<string, Sprite>();

    private bool gridBuilt;
    private Vector2 dragOffset; // 拖拽起点:控件锚点 - 指针在 gridRoot 的本地坐标

    public override void OnLoad()
    {
        bagSystem = GetService<BagSystem>();
        resMgr = GetService<ResMgr>();
        eventMgr = GetService<EventMgr>();

        if (sortBtn != null) sortBtn.onClick.AddListener(OnClickSort);
        if (closeBtn != null) closeBtn.onClick.AddListener(CloseSelf);
    }

    public override void OnShow()
    {
        eventMgr.Add(YOTOEventType.RefreshBagList, Refresh);
        BuildGrid();
        Refresh();
    }

    public override void OnHide()
    {
        eventMgr.Remove(YOTOEventType.RefreshBagList, Refresh);
    }

    public override void OnResize() { }

    // ---------------- 格盘背景 ----------------

    private void BuildGrid()
    {
        if (gridBuilt || bagSystem?.Bag == null || gridRoot == null) return;
        var bag = bagSystem.Bag;

        // gridRoot 轴心取左上(0,1)、居中于父级:布局坐标与拖拽 ScreenToLocal 坐标
        // 同以"左上角为原点、向右+x、向下-y",换算一致,无需在预制体里手调。
        float gw = bag.Width * cellSize;
        float gh = bag.Height * cellSize;
        gridRoot.anchorMin = gridRoot.anchorMax = new Vector2(0.5f, 0.5f);
        gridRoot.pivot = new Vector2(0f, 1f);
        gridRoot.sizeDelta = new Vector2(gw, gh);
        gridRoot.anchoredPosition = new Vector2(-gw * 0.5f, gh * 0.5f);

        for (int y = 0; y < bag.Height; y++)
            for (int x = 0; x < bag.Width; x++)
            {
                var go = new GameObject($"Cell_{x}_{y}", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(gridRoot, false);
                TopLeft(rt);
                rt.sizeDelta = new Vector2(cellSize - cellGap, cellSize - cellGap);
                rt.anchoredPosition = CellPos(x, y);
                var img = go.GetComponent<Image>();
                img.color = cellColor;
                img.raycastTarget = false;
                cellBgs.Add(go);
            }
        gridBuilt = true;
    }

    // ---------------- 刷新物品 ----------------

    private void Refresh()
    {
        if (bagSystem?.Bag == null || gridRoot == null) return;
        if (!gridBuilt) BuildGrid();

        // 物品数量级很小,直接清掉旧控件全量重建,逻辑最简也最不易出错
        foreach (var w in widgets.Values)
            if (w != null) Destroy(w.gameObject);
        widgets.Clear();

        var bag = bagSystem.Bag;
        int used = 0;
        var items = bag.Items;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            used += item.W * item.H;
            CreateWidget(item);
        }

        if (capacityText != null)
            capacityText.text = $"{used}/{bag.Width * bag.Height}";
    }

    private void CreateWidget(PlacedItem item)
    {
        if (itemWidgetPrefab == null) return;
        var go = Instantiate(itemWidgetPrefab, gridRoot);
        go.SetActive(true);
        var widget = go.GetComponent<BagItemWidget>();
        if (widget == null) { Destroy(go); return; }

        var cfg = bagSystem.GetItem(item.itemId);
        var sprite = cfg != null ? LoadIcon(cfg.IconPath) : null;
        widget.Setup(this, item, sprite, cfg != null ? cfg.Name : string.Empty);

        Layout(widget, item);
        widgets[item.instanceId] = widget;
    }

    /// <summary>把控件摆到物品当前位置,大小按有效占格 W×H。</summary>
    private void Layout(BagItemWidget widget, PlacedItem item)
    {
        var rt = widget.Rect;
        TopLeft(rt);
        rt.sizeDelta = new Vector2(item.W * cellSize - cellGap, item.H * cellSize - cellGap);
        rt.anchoredPosition = new Vector2(item.x * cellSize + cellGap * 0.5f, -(item.y * cellSize) - cellGap * 0.5f);
    }

    // ---------------- 拖拽(由 BagItemWidget 转发)----------------

    public void OnWidgetBeginDrag(BagItemWidget widget, PointerEventData e)
    {
        widget.Rect.SetAsLastSibling(); // 拖动中浮到最上层
        if (ScreenToGrid(e, out var p))
            dragOffset = widget.Rect.anchoredPosition - p;
    }

    public void OnWidgetDrag(BagItemWidget widget, PointerEventData e)
    {
        if (ScreenToGrid(e, out var p))
            widget.Rect.anchoredPosition = p + dragOffset;
    }

    public void OnWidgetEndDrag(BagItemWidget widget, PointerEventData e)
    {
        // 由控件当前左上角锚点反算目标格(CellPos 的逆运算)
        var pos = widget.Rect.anchoredPosition;
        int tx = Mathf.RoundToInt((pos.x - cellGap * 0.5f) / cellSize);
        int ty = Mathf.RoundToInt((-pos.y - cellGap * 0.5f) / cellSize);

        if (bagSystem.MoveItem(widget.InstanceId, tx, ty))
            return; // 成功:OnChanged → Refresh 重排,无需再动

        // 失败(越界/重叠/原地):贴回真实位置
        var item = bagSystem.Bag.GetByInstance(widget.InstanceId);
        if (item != null) Layout(widget, item);
    }

    public void OnWidgetClick(BagItemWidget widget)
    {
        bagSystem.UseItem(widget.InstanceId);
    }

    public void OnWidgetRotate(BagItemWidget widget)
    {
        // 旋转成功 → OnChanged → Refresh 自动重画;失败(放不下)什么都不做
        bagSystem.RotateItem(widget.InstanceId);
    }

    // ---------------- 按钮 ----------------

    private void OnClickSort() => bagSystem.SortBag();

    // ---------------- 工具 ----------------

    /// <summary>左上角锚点/轴心(0,1):anchoredPosition.x 向右为正,y 向下为负。</summary>
    private static void TopLeft(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
    }

    private Vector2 CellPos(int x, int y) =>
        new Vector2(x * cellSize + cellGap * 0.5f, -(y * cellSize) - cellGap * 0.5f);

    /// <summary>屏幕坐标 → gridRoot 本地坐标。Overlay 画布 pressEventCamera 为 null,API 兼容。</summary>
    private bool ScreenToGrid(PointerEventData e, out Vector2 local) =>
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            gridRoot, e.position, e.pressEventCamera, out local);

    private Sprite LoadIcon(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (iconCache.TryGetValue(path, out var cached)) return cached;
        var sprite = resMgr.Load<Sprite>(path);
        iconCache[path] = sprite;
        return sprite;
    }
}
