using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YFramework.Config;

/// <summary>
/// 宿主接口:一个页面(背包 <see cref="BagPanel"/>)实现它,
/// 协调若干 <see cref="BagGridView"/> 的拖拽(含跨网格转移)、点击/右键/悬停,并提供共享查询。
/// </summary>
public interface IGridHost
{
    Item GetItemConfig(int itemId);
    Sprite LoadIcon(string path);
    TMP_FontAsset UiFont { get; }

    void OnItemBeginDrag(BagGridView view, BagItemWidget widget, PointerEventData e);
    void OnItemDrag(BagGridView view, BagItemWidget widget, PointerEventData e);
    void OnItemEndDrag(BagGridView view, BagItemWidget widget, PointerEventData e);
    void OnItemClick(BagGridView view, BagItemWidget widget);
    void OnItemDoubleClick(BagGridView view, BagItemWidget widget);
    void OnItemRightClick(BagGridView view, BagItemWidget widget, PointerEventData e);
    void OnItemHoverEnter(BagGridView view, BagItemWidget widget, PointerEventData e);
    void OnItemHoverExit(BagGridView view, BagItemWidget widget);
}

/// <summary>
/// 可复用的「单个网格」渲染视图:把一个 <see cref="GridBag"/> 画成 W×H 格盘 + 物品控件 + 落点高亮。
/// 自身 RectTransform 即格盘根,居中于父级(父级决定它在窗口里的位置——背包居中、宝箱左右分屏)。
/// 不含拖拽业务逻辑,只把控件的指针事件转发给 <see cref="IGridHost"/>;高亮/坐标换算供宿主调用。
/// 背包与宝箱共用此视图,实现「左侧复用背包」。
/// </summary>
public class BagGridView : MonoBehaviour
{
    public GridBag Bag { get; private set; }
    public RectTransform Root { get; private set; }

    private IGridHost host;
    private GameObject itemWidgetPrefab;
    private float cellSize, cellGap;
    private Color cellColor, validColor, invalidColor;

    private readonly Dictionary<int, BagItemWidget> widgets = new Dictionary<int, BagItemWidget>();
    private RectTransform highlightRoot;
    private readonly List<Image> highlightPool = new List<Image>();
    private bool gridBuilt;

    /// <summary>初始化(宿主在 OnShow 调)。itemWidgetPrefab = UI/Bag/BagItem。</summary>
    public void Setup(IGridHost host, GameObject itemWidgetPrefab,
        float cellSize, float cellGap, Color cellColor, Color validColor, Color invalidColor)
    {
        this.host = host;
        this.itemWidgetPrefab = itemWidgetPrefab;
        this.cellSize = cellSize;
        this.cellGap = cellGap;
        this.cellColor = cellColor;
        this.validColor = validColor;
        this.invalidColor = invalidColor;
        Root = (RectTransform)transform;
    }

    /// <summary>绑定(或换绑)要渲染的网格,并重建。自动订阅其 OnChanged 实现自刷新。</summary>
    public void Bind(GridBag bag)
    {
        if (Bag != null) Bag.OnChanged -= Refresh; // 解除旧订阅
        Bag = bag;
        if (Bag != null) Bag.OnChanged += Refresh;

        gridBuilt = false;
        // 清掉旧格背景/高亮(换绑不同尺寸网格时)
        for (int i = Root.childCount - 1; i >= 0; i--) Destroy(Root.GetChild(i).gameObject);
        widgets.Clear();
        highlightPool.Clear();
        highlightRoot = null;
        BuildGrid();
        Refresh();
    }

    /// <summary>解绑(页面隐藏时调,避免悬空订阅)。</summary>
    public void Unbind()
    {
        if (Bag != null) Bag.OnChanged -= Refresh;
        Bag = null;
    }

    private void OnDestroy()
    {
        if (Bag != null) Bag.OnChanged -= Refresh;
    }

    // ---------------- 格盘 ----------------

    private void BuildGrid()
    {
        if (gridBuilt || Bag == null) return;

        float gw = Bag.Width * cellSize;
        float gh = Bag.Height * cellSize;
        Root.anchorMin = Root.anchorMax = new Vector2(0.5f, 0.5f);
        Root.pivot = new Vector2(0f, 1f);
        Root.sizeDelta = new Vector2(gw, gh);
        Root.anchoredPosition = new Vector2(-gw * 0.5f, gh * 0.5f); // 居中于父级

        for (int y = 0; y < Bag.Height; y++)
            for (int x = 0; x < Bag.Width; x++)
            {
                var go = new GameObject($"Cell_{x}_{y}", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(Root, false);
                TopLeft(rt);
                rt.sizeDelta = new Vector2(cellSize - cellGap, cellSize - cellGap);
                rt.anchoredPosition = CellPos(x, y);
                var img = go.GetComponent<Image>();
                img.color = cellColor;
                img.raycastTarget = false;
            }

        var hr = new GameObject("HighlightRoot", typeof(RectTransform));
        highlightRoot = (RectTransform)hr.transform;
        highlightRoot.SetParent(Root, false);
        TopLeft(highlightRoot);
        highlightRoot.sizeDelta = new Vector2(gw, gh);
        highlightRoot.anchoredPosition = Vector2.zero;

        gridBuilt = true;
    }

    /// <summary>从模型重绘所有物品控件。</summary>
    public void Refresh()
    {
        if (Bag == null) return;
        if (!gridBuilt) BuildGrid();

        foreach (var w in widgets.Values)
            if (w != null) Destroy(w.gameObject);
        widgets.Clear();

        var items = Bag.Items;
        for (int i = 0; i < items.Count; i++)
            CreateWidget(items[i]);

        if (highlightRoot != null) highlightRoot.SetAsLastSibling();
    }

    private void CreateWidget(PlacedItem item)
    {
        if (itemWidgetPrefab == null) return;
        var go = Instantiate(itemWidgetPrefab, Root);
        go.SetActive(true);
        var widget = go.GetComponent<BagItemWidget>();
        if (widget == null) { Destroy(go); return; }

        var cfg = host.GetItemConfig(item.itemId);
        var sprite = cfg != null ? host.LoadIcon(cfg.IconPath) : null;
        var color = ItemQualityPalette.Background(cfg != null ? (ItemQuality)cfg.Quality : ItemQuality.Common);
        widget.Init(this, item.instanceId, item.itemId, sprite, color, cfg != null ? cfg.Name : string.Empty, host.UiFont);

        widget.Build(Bag.LocalCells(item.itemId, item.rotation), cellSize, cellGap, item.count);
        LayoutAt(widget, item.x, item.y);
        widgets[item.instanceId] = widget;
    }

    /// <summary>把控件摆到第 (x,y) 格(用于拖拽失败贴回/拖中重建)。</summary>
    public void LayoutAt(BagItemWidget widget, int x, int y)
    {
        widget.Rect.SetParent(Root, false);
        widget.Rect.anchoredPosition = new Vector2(x * cellSize + cellGap * 0.5f, -(y * cellSize) - cellGap * 0.5f);
    }

    // ---------------- 控件事件转发(BagItemWidget 调)----------------

    public void WidgetBeginDrag(BagItemWidget w, PointerEventData e) => host?.OnItemBeginDrag(this, w, e);
    public void WidgetDrag(BagItemWidget w, PointerEventData e) => host?.OnItemDrag(this, w, e);
    public void WidgetEndDrag(BagItemWidget w, PointerEventData e) => host?.OnItemEndDrag(this, w, e);
    public void WidgetClick(BagItemWidget w) => host?.OnItemClick(this, w);
    public void WidgetDoubleClick(BagItemWidget w) => host?.OnItemDoubleClick(this, w);
    public void WidgetRightClick(BagItemWidget w, PointerEventData e) => host?.OnItemRightClick(this, w, e);
    public void WidgetHoverEnter(BagItemWidget w, PointerEventData e) => host?.OnItemHoverEnter(this, w, e);
    public void WidgetHoverExit(BagItemWidget w) => host?.OnItemHoverExit(this, w);

    // ---------------- 坐标 / 高亮(宿主调)----------------

    /// <summary>屏幕点是否落在本格盘范围内。</summary>
    public bool ContainsScreenPoint(Vector2 screen, Camera cam)
        => RectTransformUtility.RectangleContainsScreenPoint(Root, screen, cam);

    /// <summary>
    /// 拖拽中物品控件的「左上角」→ 格锚点。控件 pivot 为左上(0,1),其 <c>position</c> 即左上角世界坐标;
    /// 转到本网格 Root 的局部坐标(与格布局 anchoredPosition 同基准)再换算。
    /// 这样高亮/落点与物品**视觉位置**对齐,不受抓取点偏移影响(修复道具与绿框错位)。
    /// </summary>
    public void WidgetToAnchor(RectTransform widgetRect, int itemId, int rotation, out int ax, out int ay)
    {
        Vector2 local = Root.InverseTransformPoint(widgetRect.position);
        LocalToAnchor(local, itemId, rotation, out ax, out ay);
    }

    /// <summary>Root 局部坐标 → 夹回界内的格锚点(布局公式的逆运算)。</summary>
    private void LocalToAnchor(Vector2 local, int itemId, int rotation, out int ax, out int ay)
    {
        ax = Mathf.RoundToInt((local.x - cellGap * 0.5f) / cellSize);
        ay = Mathf.RoundToInt((-local.y - cellGap * 0.5f) / cellSize);
        int ew = Bag.EffW(itemId, rotation), eh = Bag.EffH(itemId, rotation);
        ax = Mathf.Clamp(ax, 0, Mathf.Max(0, Bag.Width - ew));
        ay = Mathf.Clamp(ay, 0, Mathf.Max(0, Bag.Height - eh));
    }

    public void ShowHighlight(int itemId, int ax, int ay, int rotation, bool valid)
    {
        if (highlightRoot == null) return;
        var cells = Bag.LocalCells(itemId, rotation);
        var color = valid ? validColor : invalidColor;
        EnsureHighlight(cells.Length);
        for (int i = 0; i < highlightPool.Count; i++)
        {
            if (i < cells.Length)
            {
                var c = cells[i];
                ((RectTransform)highlightPool[i].transform).anchoredPosition = CellPos(ax + c.x, ay + c.y);
                highlightPool[i].color = color;
                highlightPool[i].gameObject.SetActive(true);
            }
            else highlightPool[i].gameObject.SetActive(false);
        }
        highlightRoot.SetAsLastSibling();
    }

    public void HideHighlight()
    {
        for (int i = 0; i < highlightPool.Count; i++)
            if (highlightPool[i] != null) highlightPool[i].gameObject.SetActive(false);
    }

    private void EnsureHighlight(int count)
    {
        while (highlightPool.Count < count)
        {
            var go = new GameObject("HL", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(highlightRoot, false);
            TopLeft(rt);
            rt.sizeDelta = new Vector2(cellSize - cellGap, cellSize - cellGap);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            highlightPool.Add(img);
        }
    }

    // ---------------- 工具 ----------------

    private Vector2 CellPos(int x, int y) =>
        new Vector2(x * cellSize + cellGap * 0.5f, -(y * cellSize) - cellGap * 0.5f);

    private static void TopLeft(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
    }
}
