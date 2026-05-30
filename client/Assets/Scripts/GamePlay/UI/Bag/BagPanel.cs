using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YOTO;

/// <summary>
/// 网格空间背包面板(<see cref="UIPageBase"/>,注册为 <see cref="UIEnum.BagPanel"/>)。
/// 把 <see cref="BagSystem"/> 的网格背包画成 W×H 格盘,物品按形状(可 L/T 多边形)成块显示。
/// 交互:左键=使用,右键=原地旋转,拖拽=移动;拖拽中按 <see cref="rotateKey"/>(默认 R)旋转;
/// 拖到别的物品上=快速交换。拖拽时显示绿(可放)/红(不可放)落点高亮。监听 RefreshBagList 整体重绘。
///
/// 坐标:gridRoot 轴心左上(0,1)居中于窗口;第 (x,y) 格左上 anchoredPosition=(x*cell+gap/2, -(y*cell)-gap/2)。
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

    [Header("交互")]
    public KeyCode rotateKey = KeyCode.R;
    public Color validColor = new Color(0.2f, 1f, 0.2f, 0.4f);
    public Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.4f);

    private BagSystem bagSystem;
    private ResMgr resMgr;
    private EventMgr eventMgr;
    private Canvas canvas;

    private readonly List<GameObject> cellBgs = new List<GameObject>();
    private readonly Dictionary<int, BagItemWidget> widgets = new Dictionary<int, BagItemWidget>();
    private readonly Dictionary<string, Sprite> iconCache = new Dictionary<string, Sprite>();

    private RectTransform highlightRoot;
    private readonly List<Image> highlightPool = new List<Image>();

    private bool gridBuilt;

    // 拖拽态
    private BagItemWidget dragWidget;
    private int dragRotation;
    private bool isDragging;

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
        EndDragState();
    }

    public override void OnResize() { }

    private void Update()
    {
        // 拖拽中按旋转键:改朝向 + 重建被拖控件 + 刷新高亮
        if (isDragging && dragWidget != null && Input.GetKeyDown(rotateKey))
        {
            dragRotation = (dragRotation + 1) & 3;
            var cells = bagSystem.Bag.LocalCells(dragWidget.ItemId, dragRotation);
            dragWidget.Build(cells, cellSize, cellGap);
            UpdateHighlight();
        }
    }

    // ---------------- 格盘背景 ----------------

    private void BuildGrid()
    {
        if (gridBuilt || bagSystem?.Bag == null || gridRoot == null) return;
        var bag = bagSystem.Bag;

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

        // 高亮层:在格背景之上、物品之下(物品在 Refresh 时后加入,渲染更晚)
        var hr = new GameObject("HighlightRoot", typeof(RectTransform));
        highlightRoot = (RectTransform)hr.transform;
        highlightRoot.SetParent(gridRoot, false);
        TopLeft(highlightRoot);
        highlightRoot.sizeDelta = new Vector2(gw, gh);
        highlightRoot.anchoredPosition = Vector2.zero;

        gridBuilt = true;
    }

    // ---------------- 刷新物品 ----------------

    private void Refresh()
    {
        if (bagSystem?.Bag == null || gridRoot == null) return;
        if (!gridBuilt) BuildGrid();

        foreach (var w in widgets.Values)
            if (w != null) Destroy(w.gameObject);
        widgets.Clear();

        var bag = bagSystem.Bag;
        int used = 0;
        var items = bag.Items;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            used += bag.LocalCells(item.itemId, item.rotation).Length;
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
        widget.Init(this, item.instanceId, item.itemId, sprite, BlockColor(item.itemId));

        var cells = bagSystem.Bag.LocalCells(item.itemId, item.rotation);
        widget.Build(cells, cellSize, cellGap);
        LayoutAt(widget, item.x, item.y);
        widgets[item.instanceId] = widget;
    }

    private void LayoutAt(BagItemWidget widget, int x, int y)
    {
        widget.Rect.anchoredPosition = new Vector2(x * cellSize + cellGap * 0.5f, -(y * cellSize) - cellGap * 0.5f);
    }

    // ---------------- 拖拽(由 BagItemWidget 转发)----------------

    public void OnWidgetBeginDrag(BagItemWidget widget, PointerEventData e)
    {
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        widget.Rect.SetAsLastSibling();
        dragWidget = widget;
        isDragging = true;
        var item = bagSystem.Bag.GetByInstance(widget.InstanceId);
        dragRotation = item != null ? item.rotation : 0;
        UpdateHighlight();
    }

    public void OnWidgetDrag(BagItemWidget widget, PointerEventData e)
    {
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        if (scale <= 0f) scale = 1f;
        widget.Rect.anchoredPosition += e.delta / scale;
        UpdateHighlight();
    }

    public void OnWidgetEndDrag(BagItemWidget widget, PointerEventData e)
    {
        GetTargetAnchor(widget, out int tx, out int ty);
        bool ok = bagSystem.PlaceOrSwap(widget.InstanceId, tx, ty, dragRotation);
        EndDragState();
        if (!ok) Refresh(); // 失败:从模型重绘,贴回原位/原朝向(成功时 OnChanged 已触发 Refresh)
    }

    public void OnWidgetClick(BagItemWidget widget) => bagSystem.UseItem(widget.InstanceId);
    public void OnWidgetRotate(BagItemWidget widget) => bagSystem.RotateItem(widget.InstanceId);

    private void EndDragState()
    {
        isDragging = false;
        dragWidget = null;
        HideHighlight();
    }

    // ---------------- 落点高亮 ----------------

    private void UpdateHighlight()
    {
        if (!isDragging || dragWidget == null) { HideHighlight(); return; }
        var bag = bagSystem.Bag;

        GetTargetAnchor(dragWidget, out int tx, out int ty);
        var cells = bag.LocalCells(dragWidget.ItemId, dragRotation);
        bool valid = bag.CanPlace(dragWidget.ItemId, tx, ty, dragRotation, dragWidget.InstanceId);
        var color = valid ? validColor : invalidColor;

        EnsureHighlight(cells.Length);
        for (int i = 0; i < highlightPool.Count; i++)
        {
            if (i < cells.Length)
            {
                var c = cells[i];
                var rt = (RectTransform)highlightPool[i].transform;
                rt.anchoredPosition = CellPos(tx + c.x, ty + c.y);
                highlightPool[i].color = color;
                highlightPool[i].gameObject.SetActive(true);
            }
            else highlightPool[i].gameObject.SetActive(false);
        }
    }

    private void HideHighlight()
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

    /// <summary>由被拖控件当前位置反算落点锚点,并按朝向包围盒夹回界内。</summary>
    private void GetTargetAnchor(BagItemWidget widget, out int tx, out int ty)
    {
        var pos = widget.Rect.anchoredPosition;
        tx = Mathf.RoundToInt((pos.x - cellGap * 0.5f) / cellSize);
        ty = Mathf.RoundToInt((-pos.y - cellGap * 0.5f) / cellSize);
        var bag = bagSystem.Bag;
        int ew = bag.EffW(widget.ItemId, dragRotation);
        int eh = bag.EffH(widget.ItemId, dragRotation);
        tx = Mathf.Clamp(tx, 0, Mathf.Max(0, bag.Width - ew));
        ty = Mathf.Clamp(ty, 0, Mathf.Max(0, bag.Height - eh));
    }

    // ---------------- 按钮 ----------------

    private void OnClickSort() => bagSystem.SortBag();

    // ---------------- 工具 ----------------

    private static void TopLeft(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
    }

    private Vector2 CellPos(int x, int y) =>
        new Vector2(x * cellSize + cellGap * 0.5f, -(y * cellSize) - cellGap * 0.5f);

    /// <summary>按物品类型给个区分色(占位美术;有正式图标后图标会盖在上面)。</summary>
    private Color BlockColor(int itemId)
    {
        switch (bagSystem.GetItemType(itemId))
        {
            case ItemType.Consumable: return new Color(0.3f, 0.6f, 0.35f, 0.9f);
            case ItemType.Equipment:  return new Color(0.35f, 0.45f, 0.7f, 0.9f);
            case ItemType.Material:   return new Color(0.6f, 0.5f, 0.3f, 0.9f);
            case ItemType.QuestItem:  return new Color(0.6f, 0.4f, 0.65f, 0.9f);
            case ItemType.Currency:   return new Color(0.7f, 0.65f, 0.3f, 0.9f);
            default:                  return new Color(0.4f, 0.4f, 0.45f, 0.9f);
        }
    }

    private Sprite LoadIcon(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (iconCache.TryGetValue(path, out var cached)) return cached;
        var sprite = resMgr.Load<Sprite>(path);
        iconCache[path] = sprite;
        return sprite;
    }
}
