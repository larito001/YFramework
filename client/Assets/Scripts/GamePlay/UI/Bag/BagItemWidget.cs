using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 网格背包里的单个物品控件:按有效占格尺寸显示成一块矩形。
/// 交互(全部转交 <see cref="BagPanel"/> 统一处理坐标换算与命中校验):
///   - 左键点击 = 使用物品
///   - 右键点击 = 原地旋转 90°
///   - 拖拽     = 移动到新位置
/// 拖拽与点击天然互斥(发生拖拽时 EventSystem 不再触发 OnPointerClick)。
///
/// 预制体结构(BagPrefabBuilder 自动生成):
///   BagItem (本组件 + Image 背景[raycastTarget=true])
///     ├─ Icon (Image)            → icon
///     └─ Name (TextMeshProUGUI)  → nameText(可选)
/// </summary>
public class BagItemWidget : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    public Image background;
    public Image icon;
    public TextMeshProUGUI nameText;

    public RectTransform Rect { get; private set; }
    public int InstanceId { get; private set; }
    public int ItemId { get; private set; }

    private BagPanel panel;

    private void Awake()
    {
        Rect = (RectTransform)transform;
    }

    /// <summary>由面板在创建/复用时填充数据。</summary>
    public void Setup(BagPanel owner, PlacedItem item, Sprite sprite, string displayName)
    {
        panel = owner;
        InstanceId = item.instanceId;
        ItemId = item.itemId;

        if (icon != null)
        {
            icon.enabled = sprite != null;
            icon.sprite = sprite;
        }
        if (nameText != null) nameText.text = displayName;
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
