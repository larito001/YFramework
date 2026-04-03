using System.Collections.Generic;
using UnityEngine;
using YOTO;

public class BagSystem
{
    private readonly List<Vector2Int> itemList = new();
    private readonly List<ItemData> itemDatas = new List<ItemData>();
    private EventMgr eventMgr;

    public int GetListCount => itemList.Count;

    public ItemData GetItemData(int id)
    {
        for (var i = 0; i < itemDatas.Count; i++)
        {
            if (itemDatas[i].Id == id)
            {
                return itemDatas[i];
            }
        }

        return null;
    }

    public void AddItem(int id, int num)
    {
        for (var i = 0; i < itemList.Count; i++)
        {
            if (itemList[i].x == id)
            {
                itemList[i] = new Vector2Int(id, itemList[i].y + num);
                eventMgr.Trigger(YOTOEventType.RefreshBagList);
                return;
            }
        }

        itemList.Add(new Vector2Int(id, num));
        eventMgr.Trigger(YOTOEventType.RefreshBagList);
    }

    public int GetItemNum(int id)
    {
        for (var i = 0; i < itemList.Count; i++)
        {
            if (itemList[i].x == id)
            {
                return itemList[i].y;
            }
        }

        return 0;
    }

    public void RemoveItem(int id, int num)
    {
        for (var i = 0; i < itemList.Count; i++)
        {
            if (itemList[i].x == id)
            {
                var lastNum = itemList[i].y;
                itemList[i] = new Vector2Int(id, lastNum - num);
                if (itemList[i].y <= 0)
                {
                    itemList.RemoveAt(i);
                }
            }
        }

        itemList.Remove(new Vector2Int(id, num));
        eventMgr.Trigger(YOTOEventType.RefreshBagList);
    }

    public Vector2Int GetItemByIndex(int index)
    {
        return itemList[index];
    }

    public bool CheckIsEnoughAndUse(List<Vector2Int> consumeList)
    {
        for (var i = 0; i < consumeList.Count; i++)
        {
            var item = consumeList[i];
            var itemData = GetItemData(item.x);
            if (itemData == null || item.y > GetItemNum(item.x))
            {
                return false;
            }
        }

        for (var i = 0; i < consumeList.Count; i++)
        {
            var item = consumeList[i];
            RemoveItem(item.x, item.y);
        }

        return true;
    }

    public void GMGetAllItem()
    {
        for (var i = 0; i < itemDatas.Count; i++)
        {
            itemList.Add(new Vector2Int(itemDatas[i].Id, 999));
        }
    }

    public void Init(GameContext ctx)
    {
        eventMgr = ctx.Get<EventMgr>();
        itemList.Clear();
        itemDatas.Clear();

        // using var itemDataHandle = ctx.Get<ResMgr>().LoadHandle<ItemDataSO>("Config/ItemsData");
        // var itemDataSO = itemDataHandle?.Asset;
        // if (itemDataSO == null)
        // {
        //     Debug.LogError("[BagSystem] Failed to load item config: Config/ItemsData");
        //     return;
        // }
        //
        // for (var i = 0; i < itemDataSO.ItemDatas.Count; i++)
        // {
        //     itemDatas.Add(new ItemData(itemDataSO.ItemDatas[i]));
        // }

        eventMgr.Trigger(YOTOEventType.RefreshBagList);
    }
}
