using System.Collections.Generic;
using UnityEngine;
using YFramework.Config;
using YOTO;

public class BagManager : IGameService
{
    public const int BagSize = 20;
    public const int DefaultStackLimit = 99;
    public const float BagTooltipDelay = 0.3f;

    private static readonly (int id, int count)[] InitialItems =
    {
        (1001, 5),
        (2001, 10),
        (3001, 1),
        (9001, 100),
    };

    private ConfigManager configMgr;
    private EventMgr eventMgr;
    private StoreMgr storeMgr;
    private FlyTextMgr flyTextMgr;
    private SceneReferenceService sceneRefService;

    private BagDataContaner container;
    private BagData data;

    private readonly List<BagSlot> sortBuffer = new List<BagSlot>();

    public void Init(GameContext ctx)
    {
        configMgr = ctx.Get<ConfigManager>();
        eventMgr = ctx.Get<EventMgr>();
        storeMgr = ctx.Get<StoreMgr>();
        flyTextMgr = ctx.Get<FlyTextMgr>();
        sceneRefService = ctx.Get<SceneReferenceService>();

        container = new BagDataContaner();
        container.BindStore(storeMgr);
        container.Load(() =>
        {
            data = container.GetData();
            GrantInitialItemsIfEmpty();
            eventMgr?.Trigger(YOTOEventType.RefreshBagList);
        });
    }

    public void Shutdown()
    {
        container?.Save();
        container = null;
        data = null;
        configMgr = null;
        eventMgr = null;
        storeMgr = null;
        flyTextMgr = null;
        sceneRefService = null;
    }

    public IReadOnlyList<BagSlot> GetSlotsSnapshot()
    {
        EnsureData();
        return data.slots;
    }

    public long GetCurrencyAmount(int id)
    {
        EnsureData();
        for (int i = 0; i < data.currencyEntries.Count; i++)
        {
            if (data.currencyEntries[i].itemId == id)
            {
                return data.currencyEntries[i].count;
            }
        }
        return 0L;
    }

    public bool TryAddItem(int id, int count)
    {
        if (count <= 0)
        {
            return true;
        }

        EnsureData();

        var def = configMgr.itemConfig.Get((uint)id);
        if (def == null)
        {
            Debug.LogError($"[BagManager] TryAddItem unknown itemId={id}");
            return false;
        }

        int remaining = count;
        int addedTotal = 0;

        if ((BagItemType)def.Type == BagItemType.Currency)
        {
            AddCurrencyInternal(id, count);
            addedTotal = count;
            remaining = 0;
        }
        else
        {
            int stackLimit = def.MaxStack > 0 ? def.MaxStack : DefaultStackLimit;

            for (int i = 0; i < data.slots.Count && remaining > 0; i++)
            {
                var slot = data.slots[i];
                if (slot.itemId != id || slot.count >= stackLimit)
                {
                    continue;
                }
                int space = stackLimit - slot.count;
                int put = remaining < space ? remaining : space;
                slot.count += put;
                remaining -= put;
                addedTotal += put;
            }

            while (remaining > 0)
            {
                int freeIndex = FindFreeSlotIndex();
                if (freeIndex < 0)
                {
                    break;
                }
                int put = remaining < stackLimit ? remaining : stackLimit;
                data.slots.Add(new BagSlot(id, put, freeIndex));
                remaining -= put;
                addedTotal += put;
            }
        }

        if (addedTotal > 0)
        {
            OnDataMutated();
            eventMgr?.Trigger<int, int>(YOTOEventType.OnItemPickup, id, addedTotal);
            ShowPickupFlyText(def.Name, addedTotal);
        }

        if (remaining > 0)
        {
            flyTextMgr?.AddTextAtScreenCenter("背包已满，无法拾取", FlyTextType.Quick);
            return false;
        }

        return true;
    }

    public bool TryRemoveItem(int id, int count)
    {
        if (count <= 0)
        {
            return true;
        }

        EnsureData();

        var def = configMgr.itemConfig.Get((uint)id);
        if (def == null)
        {
            Debug.LogError($"[BagManager] TryRemoveItem unknown itemId={id}");
            return false;
        }

        if ((BagItemType)def.Type == BagItemType.Currency)
        {
            long owned = GetCurrencyAmount(id);
            if (owned < count)
            {
                return false;
            }
            AddCurrencyInternal(id, -count);
            OnDataMutated();
            return true;
        }

        int total = 0;
        for (int i = 0; i < data.slots.Count; i++)
        {
            if (data.slots[i].itemId == id)
            {
                total += data.slots[i].count;
            }
        }
        if (total < count)
        {
            return false;
        }

        int remaining = count;
        for (int i = data.slots.Count - 1; i >= 0 && remaining > 0; i--)
        {
            var slot = data.slots[i];
            if (slot.itemId != id) continue;
            int take = remaining < slot.count ? remaining : slot.count;
            slot.count -= take;
            remaining -= take;
            if (slot.count <= 0)
            {
                data.slots.RemoveAt(i);
            }
        }

        OnDataMutated();
        return true;
    }

    public void DropSlot(int slotIndex)
    {
        EnsureData();
        for (int i = 0; i < data.slots.Count; i++)
        {
            if (data.slots[i].slotIndex != slotIndex) continue;
            int id = data.slots[i].itemId;
            int count = data.slots[i].count;
            data.slots.RemoveAt(i);
            OnDataMutated();
            eventMgr?.Trigger<int, int>(YOTOEventType.OnItemDrop, id, count);
            return;
        }
    }

    public void SwapOrMergeSlot(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex) return;
        EnsureData();

        BagSlot fromSlot = FindSlotAt(fromIndex);
        BagSlot toSlot = FindSlotAt(toIndex);

        if (fromSlot == null) return;

        if (toSlot == null)
        {
            fromSlot.slotIndex = toIndex;
            OnDataMutated();
            return;
        }

        if (fromSlot.itemId == toSlot.itemId)
        {
            var def = configMgr.itemConfig.Get((uint)fromSlot.itemId);
            int stackLimit = def != null && def.MaxStack > 0 ? def.MaxStack : DefaultStackLimit;
            int space = stackLimit - toSlot.count;
            if (space > 0)
            {
                int merge = fromSlot.count < space ? fromSlot.count : space;
                toSlot.count += merge;
                fromSlot.count -= merge;
                if (fromSlot.count <= 0)
                {
                    data.slots.Remove(fromSlot);
                }
                OnDataMutated();
                return;
            }
        }

        fromSlot.slotIndex = toIndex;
        toSlot.slotIndex = fromIndex;
        OnDataMutated();
    }

    public void SortBag()
    {
        EnsureData();

        var merged = new Dictionary<int, BagSlot>();
        for (int i = 0; i < data.slots.Count; i++)
        {
            var slot = data.slots[i];
            if (merged.TryGetValue(slot.itemId, out var existing))
            {
                existing.count += slot.count;
            }
            else
            {
                merged[slot.itemId] = new BagSlot(slot.itemId, slot.count, 0);
            }
        }

        sortBuffer.Clear();
        foreach (var kv in merged)
        {
            int id = kv.Key;
            int total = kv.Value.count;
            var def = configMgr.itemConfig.Get((uint)id);
            int stackLimit = def != null && def.MaxStack > 0 ? def.MaxStack : DefaultStackLimit;
            while (total > 0)
            {
                int put = total < stackLimit ? total : stackLimit;
                sortBuffer.Add(new BagSlot(id, put, 0));
                total -= put;
            }
        }

        sortBuffer.Sort(CompareSlotForSort);

        data.slots.Clear();
        for (int i = 0; i < sortBuffer.Count && i < BagSize; i++)
        {
            sortBuffer[i].slotIndex = i;
            data.slots.Add(sortBuffer[i]);
        }
        sortBuffer.Clear();

        OnDataMutated();
    }

    private int CompareSlotForSort(BagSlot a, BagSlot b)
    {
        var defA = configMgr.itemConfig.Get((uint)a.itemId);
        var defB = configMgr.itemConfig.Get((uint)b.itemId);

        uint typeA = defA != null ? defA.Type : 0;
        uint typeB = defB != null ? defB.Type : 0;
        if (typeA != typeB) return typeA.CompareTo(typeB);

        int prA = defA != null ? defA.SortPriority : 0;
        int prB = defB != null ? defB.SortPriority : 0;
        if (prA != prB) return prA.CompareTo(prB);

        return a.itemId.CompareTo(b.itemId);
    }

    private void GrantInitialItemsIfEmpty()
    {
        if (data.slots.Count > 0 || data.currencyEntries.Count > 0)
        {
            return;
        }

        int nextSlotIndex = 0;
        for (int i = 0; i < InitialItems.Length; i++)
        {
            int id = InitialItems[i].id;
            int count = InitialItems[i].count;
            var def = configMgr.itemConfig.Get((uint)id);
            if (def == null)
            {
                Debug.LogError($"[BagManager] GrantInitialItemsIfEmpty unknown itemId={id}");
                continue;
            }

            if ((BagItemType)def.Type == BagItemType.Currency)
            {
                AddCurrencyInternal(id, count);
                continue;
            }

            int stackLimit = def.MaxStack > 0 ? def.MaxStack : DefaultStackLimit;
            int remaining = count;
            while (remaining > 0 && nextSlotIndex < BagSize)
            {
                int put = remaining < stackLimit ? remaining : stackLimit;
                data.slots.Add(new BagSlot(id, put, nextSlotIndex));
                nextSlotIndex++;
                remaining -= put;
            }
        }

        container?.Save();
    }

    private void OnDataMutated()
    {
        container?.Save();
        eventMgr?.Trigger(YOTOEventType.RefreshBagList);
    }

    private void EnsureData()
    {
        if (data == null)
        {
            data = container != null ? container.GetData() : new BagData();
        }
    }

    private int FindFreeSlotIndex()
    {
        for (int idx = 0; idx < BagSize; idx++)
        {
            if (FindSlotAt(idx) == null)
            {
                return idx;
            }
        }
        return -1;
    }

    private BagSlot FindSlotAt(int slotIndex)
    {
        for (int i = 0; i < data.slots.Count; i++)
        {
            if (data.slots[i].slotIndex == slotIndex) return data.slots[i];
        }
        return null;
    }

    private void AddCurrencyInternal(int id, int delta)
    {
        for (int i = 0; i < data.currencyEntries.Count; i++)
        {
            if (data.currencyEntries[i].itemId == id)
            {
                data.currencyEntries[i].count += delta;
                if (data.currencyEntries[i].count <= 0)
                {
                    data.currencyEntries.RemoveAt(i);
                }
                return;
            }
        }
        if (delta > 0)
        {
            data.currencyEntries.Add(new CurrencyEntry(id, delta));
        }
    }

    private void ShowPickupFlyText(string itemName, int count)
    {
        if (flyTextMgr == null) return;

        Vector3 worldPos = Vector3.zero;
        if (sceneRefService != null && sceneRefService.TryGetTransform(GameSceneRefKeys.PlayerSpawn, out var t) && t != null)
        {
            worldPos = t.position;
        }
        flyTextMgr.AddText($"获得 {itemName} ×{count}", worldPos, FlyTextType.Normal);
    }
}
