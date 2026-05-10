using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YOTO;

public class BagSlotItem : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public const string EmptySlotIconPath = "UI/Item/empty";

    public Image iconImage;
    public TextMeshProUGUI countText;
    public CanvasGroup canvasGroup;

    private BagPanel ownerPanel;
    private ResMgr resMgr;
    private RectTransform rectTransform;

    private int slotIndex;
    private int itemId;
    private int currentLoadingItemId;
    private ResourceHandle<Sprite> iconHandle;

    private Transform originalParent;
    private int originalSiblingIndex;
    private Vector3 originalLocalPosition;
    private GameObject layoutPlaceholder;

    public int SlotIndex => slotIndex;
    public int ItemId => itemId;

    public void Init(BagPanel panel, ResMgr res, int index)
    {
        ownerPanel = panel;
        resMgr = res;
        slotIndex = index;
        rectTransform = transform as RectTransform;
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
        ClearSlot();
    }

    public void SetSlot(int newItemId, int count, string iconPath)
    {
        itemId = newItemId;
        if (countText != null)
        {
            countText.text = count > 1 ? count.ToString() : string.Empty;
            countText.gameObject.SetActive(count > 1);
        }
        LoadIcon(iconPath);
    }

    public void ClearSlot()
    {
        itemId = 0;
        if (countText != null)
        {
            countText.text = string.Empty;
            countText.gameObject.SetActive(false);
        }
        LoadIcon(EmptySlotIconPath);
    }

    public void ReleaseIcon()
    {
        currentLoadingItemId = 0;
        if (iconHandle != null)
        {
            iconHandle.Dispose();
            iconHandle = null;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (itemId <= 0) return;
        Timers.inst.Add(BagManager.BagTooltipDelay, 1, OnTooltipTimer);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Timers.inst.Remove(OnTooltipTimer);
        ownerPanel?.HideTooltip();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (itemId <= 0) return;
        Timers.inst.Remove(OnTooltipTimer);
        ownerPanel?.HideTooltip();

        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();
        originalLocalPosition = transform.localPosition;

        var dragLayer = ownerPanel != null ? ownerPanel.GetDragLayer() : null;
        if (dragLayer != null)
        {
            SpawnLayoutPlaceholder();
            transform.SetParent(dragLayer, true);
            transform.SetAsLastSibling();
        }
        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;

        ownerPanel?.NotifyDragBegin(slotIndex);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (originalParent == null) return;
        if (rectTransform != null)
        {
            rectTransform.position = eventData.position;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (originalParent == null) return;
        ResetToOriginalParent();
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
        ownerPanel?.NotifyDragEnd(slotIndex, eventData.position);
    }

    public void NotifyDragCancelled()
    {
        if (originalParent == null) return;
        ResetToOriginalParent();
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
    }

    private void ResetToOriginalParent()
    {
        int targetIndex = originalSiblingIndex;
        if (layoutPlaceholder != null)
        {
            targetIndex = layoutPlaceholder.transform.GetSiblingIndex();
            DestroyImmediate(layoutPlaceholder);
            layoutPlaceholder = null;
        }
        transform.SetParent(originalParent, true);
        transform.SetSiblingIndex(targetIndex);
        transform.localPosition = originalLocalPosition;
        originalParent = null;
    }

    private void SpawnLayoutPlaceholder()
    {
        if (originalParent == null || layoutPlaceholder != null) return;
        layoutPlaceholder = new GameObject("__BagSlotDragPlaceholder", typeof(RectTransform));
        var rt = (RectTransform)layoutPlaceholder.transform;
        rt.SetParent(originalParent, false);
        rt.SetSiblingIndex(originalSiblingIndex);
        var srcRt = transform as RectTransform;
        if (srcRt != null)
        {
            rt.sizeDelta = srcRt.sizeDelta;
            rt.anchorMin = srcRt.anchorMin;
            rt.anchorMax = srcRt.anchorMax;
            rt.pivot = srcRt.pivot;
            rt.localScale = srcRt.localScale;
        }
    }

    private void OnTooltipTimer(object param)
    {
        if (itemId <= 0) return;
        ownerPanel?.ShowTooltipFor(itemId, slotIndex);
    }

    private void LoadIcon(string iconPath)
    {
        if (iconImage == null) return;
        if (string.IsNullOrEmpty(iconPath))
        {
            ReleaseIcon();
            iconImage.sprite = null;
            iconImage.enabled = false;
            return;
        }
        if (resMgr == null)
        {
            iconImage.enabled = false;
            return;
        }

        ReleaseIcon();
        currentLoadingItemId = itemId;
        int requestedItemId = itemId;
        resMgr.LoadHandleAsync<Sprite>(iconPath, handle =>
        {
            if (this == null)
            {
                handle?.Dispose();
                return;
            }
            if (currentLoadingItemId != requestedItemId || itemId != requestedItemId)
            {
                handle?.Dispose();
                return;
            }
            iconHandle = handle;
            if (handle != null && handle.Asset != null && iconImage != null)
            {
                iconImage.sprite = handle.Asset;
                iconImage.enabled = true;
            }
        });
    }

    private void OnDestroy()
    {
        Timers.inst.Remove(OnTooltipTimer);
        ReleaseIcon();
        if (layoutPlaceholder != null)
        {
            Destroy(layoutPlaceholder);
            layoutPlaceholder = null;
        }
    }
}
