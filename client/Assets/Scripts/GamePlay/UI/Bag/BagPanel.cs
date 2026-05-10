using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YFramework.Config;
using YOTO;

public class BagPanel : UIPageBase
{
    public BagSlotItem[] slotItems;
    public BagCurrencyRow[] currencyRows;
    public BagTooltip tooltip;
    public RectTransform panelRect;
    public RectTransform dragLayer;
    public Button btn_sort;
    public Button btn_close;

    private BagManager bagMgr;
    private ConfigManager configMgr;
    private EventMgr eventMgr;
    private ResMgr resMgr;

    private int draggingSlotIndex = -1;

    public override void OnLoad()
    {
        bagMgr = GetService<BagManager>();
        configMgr = GetService<ConfigManager>();
        eventMgr = GetService<EventMgr>();
        resMgr = GetService<ResMgr>();

        if (panelRect == null) panelRect = transform as RectTransform;
        if (dragLayer == null) dragLayer = panelRect;

        if (slotItems != null)
        {
            for (int i = 0; i < slotItems.Length; i++)
            {
                if (slotItems[i] != null)
                {
                    slotItems[i].Init(this, resMgr, i);
                }
            }
        }

        if (currencyRows != null)
        {
            for (int i = 0; i < currencyRows.Length; i++)
            {
                if (currencyRows[i] != null)
                {
                    currencyRows[i].Init(resMgr);
                }
            }
        }

        if (tooltip != null) tooltip.Init(resMgr);

        if (btn_sort != null) btn_sort.onClick.AddListener(OnSortClick);
        if (btn_close != null) btn_close.onClick.AddListener(CloseSelf);
    }

    public override void OnShow()
    {
        if (eventMgr != null) eventMgr.Add(YOTOEventType.RefreshBagList, OnRefresh);
        OnRefresh();
    }

    public override void OnHide()
    {
        if (eventMgr != null) eventMgr.Remove(YOTOEventType.RefreshBagList, OnRefresh);
        if (draggingSlotIndex >= 0)
        {
            CancelActiveDrag();
        }
        if (tooltip != null) tooltip.Hide();
        if (slotItems != null)
        {
            for (int i = 0; i < slotItems.Length; i++)
            {
                if (slotItems[i] != null) slotItems[i].ReleaseIcon();
            }
        }
        if (currencyRows != null)
        {
            for (int i = 0; i < currencyRows.Length; i++)
            {
                if (currencyRows[i] != null) currencyRows[i].ReleaseIcon();
            }
        }
    }

    public override void OnResize()
    {
    }

    public Transform GetDragLayer()
    {
        return dragLayer != null ? (Transform)dragLayer : transform;
    }

    public void NotifyDragBegin(int slotIndex)
    {
        draggingSlotIndex = slotIndex;
    }

    public void NotifyDragEnd(int slotIndex, Vector2 screenPos)
    {
        int from = draggingSlotIndex;
        draggingSlotIndex = -1;
        if (from < 0) return;

        if (!IsScreenInsidePanel(screenPos))
        {
            ShowDropConfirm(from);
            return;
        }

        int to = HitTestSlot(screenPos);
        if (to < 0 || to == from)
        {
            return;
        }

        if (bagMgr != null)
        {
            bagMgr.SwapOrMergeSlot(from, to);
        }
    }

    public void ShowTooltipFor(int itemId, int slotIndex)
    {
        if (tooltip == null || configMgr == null) return;
        var def = configMgr.itemConfig.Get((uint)itemId);
        Vector3 anchor = Vector3.zero;
        if (slotItems != null && slotIndex >= 0 && slotIndex < slotItems.Length && slotItems[slotIndex] != null)
        {
            anchor = slotItems[slotIndex].transform.position;
        }
        tooltip.ShowFor(def, anchor);
    }

    public void HideTooltip()
    {
        if (tooltip != null) tooltip.Hide();
    }

    private void OnRefresh()
    {
        if (bagMgr == null || configMgr == null) return;
        RefreshSlots();
        RefreshCurrencies();
    }

    private void RefreshSlots()
    {
        if (slotItems == null) return;

        for (int i = 0; i < slotItems.Length; i++)
        {
            if (slotItems[i] != null) slotItems[i].ClearSlot();
        }

        var snapshot = bagMgr.GetSlotsSnapshot();
        if (snapshot == null) return;

        for (int i = 0; i < snapshot.Count; i++)
        {
            var slot = snapshot[i];
            if (slot == null) continue;
            int idx = slot.slotIndex;
            if (idx < 0 || idx >= slotItems.Length || slotItems[idx] == null) continue;

            var def = configMgr.itemConfig.Get((uint)slot.itemId);
            string iconPath = def != null ? def.IconPath : string.Empty;
            slotItems[idx].SetSlot(slot.itemId, slot.count, iconPath);
        }
    }

    private readonly List<CurrencyEntry> currencyBuffer = new List<CurrencyEntry>();

    private void RefreshCurrencies()
    {
        if (currencyRows == null) return;

        currencyBuffer.Clear();
        foreach (var pair in configMgr.itemConfig.items)
        {
            var def = pair.Value;
            if (def == null || (BagItemType)def.Type != BagItemType.Currency) continue;
            long count = bagMgr.GetCurrencyAmount((int)def.Id);
            currencyBuffer.Add(new CurrencyEntry((int)def.Id, count));
        }

        for (int i = 0; i < currencyRows.Length; i++)
        {
            if (currencyRows[i] == null) continue;
            if (i < currencyBuffer.Count)
            {
                var def = configMgr.itemConfig.Get((uint)currencyBuffer[i].itemId);
                currencyRows[i].Setup(def, currencyBuffer[i].count);
            }
            else
            {
                currencyRows[i].Clear();
            }
        }
    }

    private bool IsScreenInsidePanel(Vector2 screenPos)
    {
        if (panelRect == null) return true;
        return RectTransformUtility.RectangleContainsScreenPoint(panelRect, screenPos, null);
    }

    private int HitTestSlot(Vector2 screenPos)
    {
        if (slotItems == null) return -1;
        for (int i = 0; i < slotItems.Length; i++)
        {
            var item = slotItems[i];
            if (item == null) continue;
            var rt = item.transform as RectTransform;
            if (rt == null) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, null))
            {
                return i;
            }
        }
        return -1;
    }

    private void ShowDropConfirm(int slotIndex)
    {
        if (bagMgr == null || configMgr == null) return;
        var snapshot = bagMgr.GetSlotsSnapshot();
        BagSlot target = null;
        for (int i = 0; i < snapshot.Count; i++)
        {
            if (snapshot[i] != null && snapshot[i].slotIndex == slotIndex)
            {
                target = snapshot[i];
                break;
            }
        }
        if (target == null) return;

        var def = configMgr.itemConfig.Get((uint)target.itemId);
        var param = new BagDropConfirmParam
        {
            slotIndex = slotIndex,
            itemName = def != null ? def.Name : string.Empty,
            count = target.count,
        };
        Show<BagDropConfirmPanel, BagDropConfirmParam>(param);
    }

    private void OnSortClick()
    {
        if (bagMgr != null) bagMgr.SortBag();
    }

    private void CancelActiveDrag()
    {
        if (slotItems == null) return;
        for (int i = 0; i < slotItems.Length; i++)
        {
            if (slotItems[i] != null && slotItems[i].SlotIndex == draggingSlotIndex)
            {
                slotItems[i].NotifyDragCancelled();
                break;
            }
        }
        draggingSlotIndex = -1;
    }
}
