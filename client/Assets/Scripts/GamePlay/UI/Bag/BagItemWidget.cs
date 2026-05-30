using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 网格背包里的单个物品控件:按形状(可不规则多边形)自建一组「格块」子物体显示,图标盖在包围盒上。
/// 只有占格的格块带 raycast,所以点击/拖拽是**形状精确**的——L/T 凹缺处的点击会穿透到下层。
/// 交互全部转交 <see cref="BagPanel"/>:左键=使用,右键=旋转,拖拽=移动/交换。
///
/// 预制体只需一个挂了本组件的空 RectTransform(BagPrefabBuilder 生成);格块与图标运行时构建,
/// 旋转拖拽时由面板调 <see cref="Build"/> 重建。
/// </summary>
public class BagItemWidget : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    public RectTransform Rect { get; private set; }
    public int InstanceId { get; private set; }
    public int ItemId { get; private set; }

    private BagPanel panel;
    private Sprite sprite;
    private Color blockColor = Color.white;
    private readonly List<GameObject> blocks = new List<GameObject>();
    private Image icon;

    private void Awake()
    {
        Rect = (RectTransform)transform;
    }

    /// <summary>绑定数据(图标/配色),不含形状;形状由 <see cref="Build"/> 给。</summary>
    public void Init(BagPanel owner, int instanceId, int itemId, Sprite spr, Color color)
    {
        panel = owner;
        InstanceId = instanceId;
        ItemId = itemId;
        sprite = spr;
        blockColor = color;
    }

    /// <summary>按占格集合(重)建外观。旋转拖拽时面板用新朝向的 cells 再调一次即可。</summary>
    public void Build(Vector2Int[] cells, float cellSize, float gap)
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

        if (icon == null)
        {
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
        icon.transform.SetAsLastSibling(); // 盖在格块之上
        icon.enabled = sprite != null;
        icon.sprite = sprite;
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
            panel?.OnWidgetRotate(this);
        else
            panel?.OnWidgetClick(this);
    }
}
