using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YFramework.Config;
using YOTO;

/// <summary>
/// 网格容器页面基类:实现 <see cref="IGridHost"/>,集中处理拖拽(含**跨网格转移**)、tooltip、右键菜单、拆分弹窗。
/// 子类(<see cref="BagPanel"/> 纯背包)只需:
///   - 在 <see cref="OnShow"/> 里 <see cref="SetupGrid"/> 每个 <see cref="BagGridView"/> 并 Bind 对应 <see cref="GridBag"/>;
///   - 把本基类的公共 UI 引用(itemWidgetPrefab/菜单/拆分/tooltip)在预制体里接好。
///
/// 拖拽落点用「指针当前所在的那个 GridView」决定目标容器:同一网格内→移动/合并/交换;跨网格→<see cref="GridBag.Transfer"/>。
/// 拆分/丢弃/使用/旋转只作用于物品所在的源容器。
/// </summary>
public abstract class GridHostPanelBase : UIPageBase, IGridHost
{
    [Header("物品控件")]
    public GameObject itemWidgetPrefab;     // UI/Bag/BagItem

    [Header("布局")]
    public float cellSize = 80f;
    public float cellGap = 4f;
    public Color cellColor = new Color(1f, 1f, 1f, 0.06f);
    public Color validColor = new Color(0.2f, 1f, 0.2f, 0.4f);
    public Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.4f);

    [Header("右键菜单")]
    public GameObject contextMenu;
    public RectTransform contextMenuPanel;
    public Button ctxUseBtn;
    public Button ctxRotateBtn;
    public Button ctxSplitBtn;
    public Button ctxDiscardBtn;

    [Header("拆分弹窗")]
    public GameObject splitDialog;
    public Slider splitSlider;
    public TextMeshProUGUI splitAmountText;
    public Button splitConfirmBtn;
    public Button splitCancelBtn;

    [Header("Tooltip")]
    public GameObject tooltip;
    public RectTransform tooltipPanel;
    public TextMeshProUGUI tipNameText;
    public TextMeshProUGUI tipDescText;
    public TextMeshProUGUI tipValueText;

    [Header("字体")]
    public TMP_FontAsset uiFont;

    protected BagSystem bagSystem;
    protected ResMgr resMgr;
    protected EventMgr eventMgr;
    protected Canvas canvas;

    private readonly Dictionary<string, Sprite> iconCache = new Dictionary<string, Sprite>();
    private readonly List<BagGridView> grids = new List<BagGridView>();

    // 拖拽态
    private BagGridView dragSourceView;
    private BagItemWidget dragWidget;
    private int dragRotation;
    private bool isDragging;

    // 菜单/拆分上下文(记住源网格 + 实例)
    private BagGridView ctxView;
    private int ctxInstanceId;
    private BagGridView splitView;
    private int splitInstanceId;

    // ---------------- IGridHost ----------------

    public Item GetItemConfig(int itemId) => bagSystem.GetItem(itemId);
    public TMP_FontAsset UiFont => uiFont;

    public Sprite LoadIcon(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (iconCache.TryGetValue(path, out var cached)) return cached;
        var sprite = resMgr.Load<Sprite>(path);
        iconCache[path] = sprite;
        return sprite;
    }

    // ---------------- 生命周期(子类在 OnShow/OnHide 调用)----------------

    protected void InitServices()
    {
        bagSystem = GetService<BagSystem>();
        resMgr = GetService<ResMgr>();
        eventMgr = GetService<EventMgr>();
        WireCommonButtons();
    }

    /// <summary>配置一个网格视图(子类对每个 BagGridView 调一次)。返回它,便于链式 Bind。</summary>
    protected BagGridView SetupGrid(BagGridView view)
    {
        if (view == null) return null;
        view.Setup(this, itemWidgetPrefab, cellSize, cellGap, cellColor, validColor, invalidColor);
        if (!grids.Contains(view)) grids.Add(view);
        return view;
    }

    protected void CommonHide()
    {
        EndDragState();
        HideContextMenu();
        HideSplitDialog();
        HideTooltip();
        foreach (var g in grids) if (g != null) g.Unbind();
        ReleaseIcons();
        // 面板关闭是可靠的存档点(此后仍有帧让 StoreMgr 异步写盘协程跑完)。
        // 不能只靠 BagSystem.Shutdown 存档——退出时 GameLoop 正在销毁,协程不再恢复,会丢档。
        bagSystem?.Save();
    }

    private void ReleaseIcons()
    {
        foreach (var kv in iconCache)
            if (kv.Value != null) resMgr.Release<Sprite>(kv.Key);
        iconCache.Clear();
    }

    private void WireCommonButtons()
    {
        if (contextMenu != null)
        {
            var blocker = contextMenu.GetComponent<Button>();
            if (blocker != null) blocker.onClick.AddListener(HideContextMenu);
            if (ctxUseBtn != null) ctxUseBtn.onClick.AddListener(OnCtxUse);
            if (ctxRotateBtn != null) ctxRotateBtn.onClick.AddListener(OnCtxRotate);
            if (ctxSplitBtn != null) ctxSplitBtn.onClick.AddListener(OnCtxSplit);
            if (ctxDiscardBtn != null) ctxDiscardBtn.onClick.AddListener(OnCtxDiscard);
            contextMenu.SetActive(false);
        }
        if (splitDialog != null)
        {
            if (splitSlider != null) splitSlider.onValueChanged.AddListener(_ => UpdateSplitText());
            if (splitConfirmBtn != null) splitConfirmBtn.onClick.AddListener(OnSplitConfirm);
            if (splitCancelBtn != null) splitCancelBtn.onClick.AddListener(HideSplitDialog);
            splitDialog.SetActive(false);
        }
        if (tooltip != null) tooltip.SetActive(false);
    }

    // ---------------- 拖拽(IGridHost 回调)----------------

    public void OnItemBeginDrag(BagGridView view, BagItemWidget widget, PointerEventData e)
    {
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        HideTooltip();
        dragSourceView = view;
        dragWidget = widget;
        isDragging = true;
        var item = view.Bag.GetByInstance(widget.InstanceId);
        dragRotation = item != null ? item.rotation : 0;
        widget.Rect.SetParent((RectTransform)transform, true); // 拖到容器层,可跨网格移动且不被裁剪
        widget.Rect.SetAsLastSibling();
        UpdateDragHighlight(e);
    }

    public void OnItemDrag(BagGridView view, BagItemWidget widget, PointerEventData e)
    {
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        if (scale <= 0f) scale = 1f;
        widget.Rect.anchoredPosition += e.delta / scale;
        UpdateDragHighlight(e);
    }

    public void OnItemEndDrag(BagGridView view, BagItemWidget widget, PointerEventData e)
    {
        var target = GridUnderPointer(e);
        int instanceId = widget.InstanceId;
        var src = dragSourceView;
        EndDragState();

        if (target != null && src != null)
        {
            // 用物品控件左上角(而非光标)反算落点,与拖拽中看到的物品视觉一致
            target.WidgetToAnchor(widget.Rect, widget.ItemId, dragRotation, out int ax, out int ay);
            GridBag.Transfer(src.Bag, instanceId, target.Bag, ax, ay, dragRotation);
        }

        // 无论成败都从模型重绘两侧网格:成功→新布局;失败→源容器把控件贴回原位
        src?.Refresh();
        if (target != null && target != src) target.Refresh();
    }

    private void UpdateDragHighlight(PointerEventData e)
    {
        foreach (var g in grids) if (g != null) g.HideHighlight();
        if (!isDragging || dragWidget == null) return;
        var target = GridUnderPointer(e);
        if (target == null) return;
        // 用物品控件左上角反算锚点 → 高亮贴着物品视觉,不随抓取点偏移
        target.WidgetToAnchor(dragWidget.Rect, dragWidget.ItemId, dragRotation, out int ax, out int ay);
        bool valid;
        if (target == dragSourceView)
            valid = target.Bag.CanPlace(dragWidget.ItemId, ax, ay, dragRotation, dragWidget.InstanceId);
        else
            valid = target.Bag.CanPlace(dragWidget.ItemId, ax, ay, dragRotation, 0);
        target.ShowHighlight(dragWidget.ItemId, ax, ay, dragRotation, valid);
    }

    /// <summary>指针当前悬停在哪个网格上(都不在则 null → 落到容器外不处理)。</summary>
    private BagGridView GridUnderPointer(PointerEventData e)
    {
        for (int i = 0; i < grids.Count; i++)
            if (grids[i] != null && grids[i].ContainsScreenPoint(e.position, e.pressEventCamera))
                return grids[i];
        return null;
    }

    private void EndDragState()
    {
        isDragging = false;
        dragWidget = null;
        dragSourceView = null;
        foreach (var g in grids) if (g != null) g.HideHighlight();
    }

    // ---------------- 点击 / 使用 ----------------

    public void OnItemClick(BagGridView view, BagItemWidget widget)
    {
        // 左键单击:打开通用道具描述弹窗(名称 + 详细描述)。不「使用」物品——使用只走右键菜单的「使用」项。
        var placed = view != null && view.Bag != null ? view.Bag.GetByInstance(widget.InstanceId) : null;
        if (placed != null) Show<ItemDescPanel, ItemDescParam>(new ItemDescParam { itemId = placed.itemId });
    }

    /// <summary>双击:快速移到另一个网格。默认无另一网格(纯背包)→ 不处理;宝箱面板重写。</summary>
    public virtual void OnItemDoubleClick(BagGridView view, BagItemWidget widget) { }

    // ---------------- 右键菜单 ----------------

    public void OnItemRightClick(BagGridView view, BagItemWidget widget, PointerEventData e)
    {
        if (contextMenu == null) return;
        ctxView = view;
        ctxInstanceId = widget.InstanceId;

        var item = view.Bag.GetByInstance(ctxInstanceId);
        bool stackable = item != null && bagSystem.IsStackable(item.itemId) && item.count > 1;
        bool inBag = view.Bag == bagSystem.Bag;
        if (ctxSplitBtn != null) ctxSplitBtn.interactable = stackable;
        if (ctxUseBtn != null) ctxUseBtn.interactable = inBag;   // 宝箱物品不能直接用
        if (ctxDiscardBtn != null) ctxDiscardBtn.interactable = inBag; // 只丢背包物品

        contextMenu.SetActive(true);
        PlaceAtPointer(contextMenuPanel, e.position, 0f);
    }

    private void HideContextMenu() { if (contextMenu != null) contextMenu.SetActive(false); }

    private void OnCtxUse()
    {
        HideContextMenu();
        if (ctxView != null && ctxView.Bag == bagSystem.Bag) bagSystem.UseItem(ctxInstanceId);
    }

    private void OnCtxRotate()
    {
        HideContextMenu();
        if (ctxView != null) ctxView.Bag.RotateItem(ctxInstanceId);
    }

    private void OnCtxDiscard()
    {
        HideContextMenu();
        if (ctxView != null && ctxView.Bag == bagSystem.Bag) bagSystem.Discard(ctxInstanceId);
    }

    private void OnCtxSplit()
    {
        HideContextMenu();
        ShowSplitDialog(ctxView, ctxInstanceId);
    }

    // ---------------- 拆分弹窗 ----------------

    private void ShowSplitDialog(BagGridView view, int instanceId)
    {
        if (splitDialog == null || view == null) return;
        var item = view.Bag.GetByInstance(instanceId);
        if (item == null || item.count <= 1) return;

        splitView = view;
        splitInstanceId = instanceId;
        splitDialog.SetActive(true);
        if (splitSlider != null)
        {
            splitSlider.wholeNumbers = true;
            splitSlider.minValue = 1;
            splitSlider.maxValue = item.count - 1;
            splitSlider.value = item.count / 2;
        }
        UpdateSplitText();
    }

    private void HideSplitDialog() { if (splitDialog != null) splitDialog.SetActive(false); }

    private void UpdateSplitText()
    {
        if (splitAmountText == null || splitView == null) return;
        int amount = splitSlider != null ? Mathf.RoundToInt(splitSlider.value) : 1;
        var item = splitView.Bag.GetByInstance(splitInstanceId);
        int rest = item != null ? item.count - amount : 0;
        splitAmountText.text = $"拆出 {amount}  /  留 {rest}";
    }

    private void OnSplitConfirm()
    {
        HideSplitDialog();
        if (splitView == null) return;
        var item = splitView.Bag.GetByInstance(splitInstanceId);
        if (item == null || item.count <= 1) return;
        int amount = splitSlider != null ? Mathf.RoundToInt(splitSlider.value) : 1;
        amount = Mathf.Clamp(amount, 1, item.count - 1);
        splitView.Bag.SplitStack(splitInstanceId, amount); // OnChanged → 该 view 自刷新
    }

    // ---------------- Tooltip ----------------

    public void OnItemHoverEnter(BagGridView view, BagItemWidget widget, PointerEventData e)
    {
        if (tooltip == null || isDragging) return;
        var cfg = bagSystem.GetItem(widget.ItemId);
        if (cfg == null) return;
        if (tipNameText != null)
        {
            tipNameText.text = cfg.Name;
            tipNameText.color = ItemQualityPalette.Accent((ItemQuality)cfg.Quality);
        }
        if (tipDescText != null) tipDescText.text = cfg.Desc;
        if (tipValueText != null) tipValueText.text = $"预估价值: {cfg.Value}";
        tooltip.SetActive(true);
        PlaceAtPointer(tooltipPanel, e.position, 16f);
    }

    public void OnItemHoverExit(BagGridView view, BagItemWidget widget) => HideTooltip();

    /// <summary>隐藏 tooltip。子类在销毁悬停目标(如双击转移)后应主动调,否则 tooltip 会留在原地。</summary>
    protected void HideTooltip() { if (tooltip != null) tooltip.SetActive(false); }

    // ---------------- 工具 ----------------

    /// <summary>把一个 pivot=左上(0,1) 的浮层移到鼠标处并钳制屏幕内;off 为与光标的错开像素。</summary>
    private void PlaceAtPointer(RectTransform panel, Vector2 screenPos, float off)
    {
        if (panel == null) return;
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        if (scale <= 0f) scale = 1f;
        float w = panel.sizeDelta.x * scale;
        float h = panel.sizeDelta.y * scale;
        float px = Mathf.Clamp(screenPos.x + off, 0f, Mathf.Max(0f, Screen.width - w));
        float py = Mathf.Clamp(screenPos.y - off, h, Screen.height);
        panel.position = new Vector3(px, py, 0f);
    }
}
