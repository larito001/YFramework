using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 网格背包里的单个物品控件:按形状(可不规则多边形)自建一组「格块」子物体显示,图标盖在包围盒上,
/// 左上角显示名称、可叠加物品右下角显示数量。只有占格的格块带 raycast,所以点击/拖拽是**形状精确**的。
/// 交互全部转交 <see cref="BagPanel"/>:
///   - 左键点击 = 使用
///   - 右键点击 = 打开右键菜单(使用/旋转/拆分/丢弃)
///   - 拖拽     = 移动 / 合并 / 交换
///   - 悬停     = 显示 tooltip(名称/描述/价值)
///
/// 预制体只需一个挂了本组件的空 RectTransform(BagPrefabBuilder 生成);格块/图标/数量运行时构建,
/// 旋转或数量变化时由面板调 <see cref="Build"/> 重建。
/// </summary>
public class BagItemWidget : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    public RectTransform Rect { get; private set; }
    public int InstanceId { get; private set; }
    public int ItemId { get; private set; }

    private BagPanel panel;
    private Sprite sprite;
    private Color blockColor = Color.white;
    private string displayName = string.Empty;
    private readonly List<GameObject> blocks = new List<GameObject>();
    private Image icon;
    private TextMeshProUGUI nameLabel;
    private TextMeshProUGUI countLabel;

    private void Awake()
    {
        Rect = (RectTransform)transform;
    }

    /// <summary>绑定数据(图标/配色/名称),不含形状;形状与数量由 <see cref="Build"/> 给。</summary>
    public void Init(BagPanel owner, int instanceId, int itemId, Sprite spr, Color color, string name)
    {
        panel = owner;
        InstanceId = instanceId;
        ItemId = itemId;
        sprite = spr;
        blockColor = color;
        displayName = name ?? string.Empty;
    }

    /// <summary>按占格集合(重)建外观;count&gt;1 时右下角显示数量。</summary>
    public void Build(Vector2Int[] cells, float cellSize, float gap, int count)
    {
        for (int i = 0; i < blocks.Count; i++)
            if (blocks[i] != null) Destroy(blocks[i]);
        blocks.Clear();

        int ew = 0, eh = 0;
        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i].x + 1 > ew) ew = cells[i].x + 1;
            if (cells[i].y + 1 > eh) eh = cells[i].y + 1;
        }
        if (ew < 1) ew = 1;
        if (eh < 1) eh = 1;

        TopLeft(Rect);
        Rect.sizeDelta = new Vector2(ew * cellSize, eh * cellSize);

        for (int i = 0; i < cells.Length; i++)
        {
            var c = cells[i];
            var go = new GameObject("Block", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(Rect, false);
            TopLeft(rt);
            rt.sizeDelta = new Vector2(cellSize - gap, cellSize - gap);
            rt.anchoredPosition = new Vector2(c.x * cellSize + gap * 0.5f, -(c.y * cellSize) - gap * 0.5f);
            var img = go.GetComponent<Image>();
            img.color = blockColor;
            img.raycastTarget = true; // 形状精确点击:只有占格挡射线
            blocks.Add(go);
        }

        EnsureIcon(gap);
        icon.transform.SetAsLastSibling();
        icon.enabled = sprite != null;
        icon.sprite = sprite;

        EnsureNameLabel();
        nameLabel.transform.SetAsLastSibling(); // 盖在图标之上
        nameLabel.text = displayName;

        EnsureCountLabel();
        countLabel.transform.SetAsLastSibling();
        bool showCount = count > 1;
        countLabel.gameObject.SetActive(showCount);
        if (showCount) countLabel.text = count.ToString();
    }

    private void EnsureIcon(float gap)
    {
        if (icon != null) return;
        var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(Rect, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(gap * 0.5f, gap * 0.5f);
        rt.offsetMax = new Vector2(-gap * 0.5f, -gap * 0.5f);
        icon = go.GetComponent<Image>();
        icon.raycastTarget = false;
        icon.preserveAspect = true;
    }

    private void EnsureNameLabel()
    {
        if (nameLabel != null) return;
        var go = new GameObject("Name", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(Rect, false);
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0, 1);
        rt.offsetMin = new Vector2(4, -24);   // 顶部高 24,左右各留 4
        rt.offsetMax = new Vector2(-4, -2);
        nameLabel = go.AddComponent<TextMeshProUGUI>();
        nameLabel.fontSize = 16;
        nameLabel.alignment = TextAlignmentOptions.TopLeft;
        nameLabel.color = Color.white;
        nameLabel.raycastTarget = false;
        nameLabel.enableWordWrapping = false;
        nameLabel.overflowMode = TextOverflowModes.Ellipsis; // 名字过长省略
        var font = panel != null ? panel.UiFont : null;
        if (font == null) font = TMP_Settings.defaultFontAsset;
        if (font != null) nameLabel.font = font;
    }

    private void EnsureCountLabel()
    {
        if (countLabel != null) return;
        var go = new GameObject("Count", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(Rect, false);
        rt.anchorMin = new Vector2(1, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(1, 0);
        rt.anchoredPosition = new Vector2(-4, 2);
        rt.sizeDelta = new Vector2(60, 26);
        countLabel = go.AddComponent<TextMeshProUGUI>();
        countLabel.fontSize = 20;
        countLabel.alignment = TextAlignmentOptions.BottomRight;
        countLabel.color = Color.white;
        countLabel.fontStyle = FontStyles.Bold;
        countLabel.raycastTarget = false;
        var font = panel != null ? panel.UiFont : null;
        if (font == null) font = TMP_Settings.defaultFontAsset;
        if (font != null) countLabel.font = font;
    }

    private static void TopLeft(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
    }

    public void OnBeginDrag(PointerEventData e) => panel?.OnWidgetBeginDrag(this, e);
    public void OnDrag(PointerEventData e) => panel?.OnWidgetDrag(this, e);
    public void OnEndDrag(PointerEventData e) => panel?.OnWidgetEndDrag(this, e);

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Right)
            panel?.OnWidgetContextMenu(this, e);
        else
            panel?.OnWidgetClick(this);
    }

    public void OnPointerEnter(PointerEventData e) => panel?.OnWidgetHoverEnter(this, e);
    public void OnPointerExit(PointerEventData e) => panel?.OnWidgetHoverExit(this);
}
