using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YOTO;

/// <summary>
/// 网格空间背包面板(<see cref="UIPageBase"/>,注册为 <see cref="UIEnum.BagPanel"/>)。
/// 把 <see cref="BagSystem"/> 的网格背包画成 W×H 格盘,物品按形状(可 L/T 多边形)成块显示,可叠加物品显示数量。
/// 交互:左键=使用,右键=打开菜单(使用/旋转/拆分/丢弃),拖拽=移动/合并/交换,悬停按 R=旋转(拖拽中按 R 旋转 ghost)。
/// 拖到同种可叠加物品上=合并;拖到异物上=交换。拖拽时绿/红落点高亮。监听 RefreshBagList 整体重绘。
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

    [Header("右键菜单")]
    public GameObject contextMenu;          // 根(全屏 blocker),点空白处关闭
    public RectTransform contextMenuPanel;  // 小菜单本体(移到鼠标处)
    public Button ctxUseBtn;
    public Button ctxRotateBtn;
    public Button ctxSplitBtn;
    public Button ctxDiscardBtn;

    [Header("拆分弹窗")]
    public GameObject splitDialog;          // 根(全屏 dim)
    public Slider splitSlider;
    public TextMeshProUGUI splitAmountText;
    public Button splitConfirmBtn;
    public Button splitCancelBtn;

    [Header("布局")]
    public float cellSize = 80f;
    public float cellGap = 4f;
    public Color cellColor = new Color(1f, 1f, 1f, 0.06f);

    [Header("交互")]
    public KeyCode rotateKey = KeyCode.R;
    public Color validColor = new Color(0.2f, 1f, 0.2f, 0.4f);
    public Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.4f);

    [Header("字体")]
    [Tooltip("UI 字体(SIMHEI SDF),由 BagPrefabBuilder 序列化注入;运行时数量标签用它")]
    public TMP_FontAsset uiFont;

    /// <summary>UI 字体(供 BagItemWidget 运行时创建数量标签用)。</summary>
    public TMP_FontAsset UiFont => uiFont;

    private BagSystem bagSystem;
    private ResMgr resMgr;
    private EventMgr eventMgr;
    private Canvas canvas;

    private readonly Dictionary<int, BagItemWidget> widgets = new Dictionary<int, BagItemWidget>();
    private readonly Dictionary<string, Sprite> iconCache = new Dictionary<string, Sprite>();

    private RectTransform highlightRoot;
    private readonly List<Image> highlightPool = new List<Image>();

    private bool gridBuilt;

    // 拖拽态
    private BagItemWidget dragWidget;
    private int dragRotation;
    private bool isDragging;

    // 悬停 + 菜单/拆分上下文
    private BagItemWidget hoveredWidget;
    private int contextInstanceId;
    private int splitInstanceId;

    public override void OnLoad()
    {
        bagSystem = GetService<BagSystem>();
        resMgr = GetService<ResMgr>();
        eventMgr = GetService<EventMgr>();

        if (sortBtn != null) sortBtn.onClick.AddListener(OnClickSort);
        if (closeBtn != null) closeBtn.onClick.AddListener(CloseSelf);

        if (contextMenu != null)
        {
            var blockerBtn = contextMenu.GetComponent<Button>();
            if (blockerBtn != null) blockerBtn.onClick.AddListener(HideContextMenu);
            if (ctxUseBtn != null) ctxUseBtn.onClick.AddListener(OnCtxUse);
            if (ctxRotateBtn != null) ctxRotateBtn.onClick.AddListener(OnCtxRotate);
            if (ctxSplitBtn != null) ctxSplitBtn.onClick.AddListener(OnCtxSplit);
            if (ctxDiscardBtn != null) ctxDiscardBtn.onClick.AddListener(OnCtxDiscard);
            contextMenu.SetActive(false);
        }

        if (splitDialog != null)
        {
            if (splitSlider != null) splitSlider.onValueChanged.AddListener(OnSplitSliderChanged);
            if (splitConfirmBtn != null) splitConfirmBtn.onClick.AddListener(OnSplitConfirm);
            if (splitCancelBtn != null) splitCancelBtn.onClick.AddListener(HideSplitDialog);
            splitDialog.SetActive(false);
        }
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
        HideContextMenu();
        HideSplitDialog();
        ReleaseIcons();
    }

    private void ReleaseIcons()
    {
        foreach (var kv in iconCache)
            if (kv.Value != null) resMgr.Release<Sprite>(kv.Key);
        iconCache.Clear();
    }

    public override void OnResize() { }

    private void Update()
    {
        if (!Input.GetKeyDown(rotateKey)) return;

        // 拖拽中按 R:旋转 ghost
        if (isDragging && dragWidget != null)
        {
            dragRotation = (dragRotation + 1) & 3;
            var cells = bagSystem.Bag.LocalCells(dragWidget.ItemId, dragRotation);
            int cnt = bagSystem.Bag.GetByInstance(dragWidget.InstanceId)?.count ?? 1;
            dragWidget.Build(cells, cellSize, cellGap, cnt);
            UpdateHighlight();
            return;
        }

        // 悬停按 R:原地旋转该物品
        if (hoveredWidget != null)
        {
            bagSystem.RotateItem(hoveredWidget.InstanceId); // 成功会 OnChanged→Refresh
            hoveredWidget = null; // 重建后引用失效,等鼠标移动重新 enter
        }
    }

    public void SetHoveredWidget(BagItemWidget w) => hoveredWidget = w;
    public void ClearHoveredWidget(BagItemWidget w) { if (hoveredWidget == w) hoveredWidget = null; }

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
            }

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
        hoveredWidget = null; // 旧引用随重建失效

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

        // 高亮层始终保持在所有物品之上,确保拖拽落点可见
        if (highlightRoot != null) highlightRoot.SetAsLastSibling();
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
        widget.Build(cells, cellSize, cellGap, item.count);
        LayoutAt(widget, item.x, item.y);
        widgets[item.instanceId] = widget;
    }

    private void LayoutAt(BagItemWidget widget, int x, int y)
    {
        widget.Rect.anchoredPosition = new Vector2(x * cellSize + cellGap * 0.5f, -(y * cellSize) - cellGap * 0.5f);
    }

    // ---------------- 拖拽 ----------------

    public void OnWidgetBeginDrag(BagItemWidget widget, PointerEventData e)
    {
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        widget.Rect.SetAsLastSibling();
        if (highlightRoot != null) highlightRoot.SetAsLastSibling();
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
        // 时序:PlaceOrSwap 成功会同步 OnChanged→Refresh,Refresh 会 Destroy 本 widget。
        // 因此先取值、先清拖拽态,调用后不要再解引用 widget。
        GetTargetAnchor(widget, out int tx, out int ty);
        int instanceId = widget.InstanceId;
        EndDragState();
        bool ok = bagSystem.PlaceOrSwap(instanceId, tx, ty, dragRotation);
        if (!ok) Refresh(); // 失败:模型未变,手动重绘把 widget 贴回原位/原朝向
    }

    public void OnWidgetClick(BagItemWidget widget) => bagSystem.UseItem(widget.InstanceId);

    private void EndDragState()
    {
        isDragging = false;
        dragWidget = null;
        HideHighlight();
    }

    // ---------------- 右键菜单 ----------------

    public void OnWidgetContextMenu(BagItemWidget widget, PointerEventData e)
    {
        if (contextMenu == null) return;
        contextInstanceId = widget.InstanceId;

        var item = bagSystem.Bag.GetByInstance(contextInstanceId);
        bool canSplit = item != null && bagSystem.IsStackable(item.itemId) && item.count > 1;
        if (ctxSplitBtn != null) ctxSplitBtn.interactable = canSplit;

        contextMenu.SetActive(true);
        if (contextMenuPanel != null) contextMenuPanel.position = e.position; // 移到鼠标处(Overlay 画布)
    }

    private void HideContextMenu() { if (contextMenu != null) contextMenu.SetActive(false); }

    private void OnCtxUse()
    {
        HideContextMenu();
        bagSystem.UseItem(contextInstanceId);
    }

    private void OnCtxRotate()
    {
        HideContextMenu();
        bagSystem.RotateItem(contextInstanceId);
    }

    private void OnCtxDiscard()
    {
        HideContextMenu();
        bagSystem.Discard(contextInstanceId);
    }

    private void OnCtxSplit()
    {
        HideContextMenu();
        ShowSplitDialog(contextInstanceId);
    }

    // ---------------- 拆分弹窗 ----------------

    private void ShowSplitDialog(int instanceId)
    {
        if (splitDialog == null) return;
        var item = bagSystem.Bag.GetByInstance(instanceId);
        if (item == null || item.count <= 1) return;

        splitInstanceId = instanceId;
        splitDialog.SetActive(true);
        if (splitSlider != null)
        {
            splitSlider.wholeNumbers = true;
            splitSlider.minValue = 1;
            splitSlider.maxValue = item.count - 1; // 至少给原堆留 1
            splitSlider.value = item.count / 2;    // 默认对半
        }
        UpdateSplitText();
    }

    private void HideSplitDialog() { if (splitDialog != null) splitDialog.SetActive(false); }

    private void OnSplitSliderChanged(float _) => UpdateSplitText();

    private void UpdateSplitText()
    {
        if (splitAmountText == null) return;
        int amount = splitSlider != null ? Mathf.RoundToInt(splitSlider.value) : 1;
        var item = bagSystem.Bag.GetByInstance(splitInstanceId);
        int rest = item != null ? item.count - amount : 0;
        splitAmountText.text = $"拆出 {amount}  /  留 {rest}";
    }

    private void OnSplitConfirm()
    {
        int amount = splitSlider != null ? Mathf.RoundToInt(splitSlider.value) : 1;
        HideSplitDialog();
        bagSystem.SplitStack(splitInstanceId, amount); // 成功 OnChanged→Refresh
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
