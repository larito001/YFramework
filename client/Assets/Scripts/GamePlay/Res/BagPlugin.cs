using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YOTO;

public class BagSystem
{
    private List<Vector2Int> ItemList = new();
    private List<ItemData> ItemDatas = new List<ItemData>();


    public ItemData GetItemData(int id)
    {
        for (var i = 0; i < ItemDatas.Count; i++)
        {
            if (ItemDatas[i].Id == id)
            {
                return ItemDatas[i];
            }
        }

        return null;
    }
    
   
    public void AddItem(int id, int num)
    {
        for (var i = 0; i < ItemList.Count; i++)
        {
            if (ItemList[i].x == id)
            {
                ItemList[i] = new Vector2Int(id, ItemList[i].y + num);
                GameLoop.Instance.Ctx.Get<EventMgr>().TriggerEvent(YOTOEventType.RefreshBagList);
                return;
            }
        }

        ItemList.Add(new Vector2Int(id, num));
        GameLoop.Instance.Ctx.Get<EventMgr>().TriggerEvent(YOTOEventType.RefreshBagList);
    }

    public int GetItemNum(int id)
    {
        for (var i = 0; i < ItemList.Count; i++)
        {
            if (ItemList[i].x == id)
            {
                return ItemList[i].y;
            }
        }

        return 0;
    }

    public void RemoveItem(int id, int num)
    {
        for (var i = 0; i < ItemList.Count; i++)
        {
            if (ItemList[i].x == id)
            {
                var lastNum = ItemList[i].y;
                ItemList[i] = new Vector2Int(id, lastNum - num);
                if (ItemList[i].y <= 0)
                {
                    ItemList.RemoveAt(i);
                }
            }
        }

        ItemList.Remove(new Vector2Int(id, num));
        GameLoop.Instance.Ctx.Get<EventMgr>().TriggerEvent(YOTOEventType.RefreshBagList);
    }

    public int GetListCount => ItemList.Count;

    public Vector2Int GetItemByIndex(int index)
    {
        return ItemList[index];
    }

    public bool CheckIsEnoughAndUse(List<Vector2Int> itemList)
    {
        for (var i = 0; i < itemList.Count; i++)
        {
            var item = itemList[i];
            var itemData = GetItemData(item.x);
            if (itemData == null)
            {
                return false;
            }

            if (item.y > GetItemNum(item.x))
            {
                return false;
            }
        }
        for (var i = 0; i < itemList.Count; i++)
        {
            var item = itemList[i];
            RemoveItem(item.x, item.y);
        }
        return true;
    }

    public void GMGetAllItem()
    {
        for (var i = 0; i < ItemDatas.Count; i++)
        {
            ItemList.Add(new Vector2Int(ItemDatas[i].Id, 999));
        }
    }

    public void Init(GameContext ctx)
    {
        
        ItemList.Clear();

        var itemDataSO = Resources.Load<ItemDataSO>("Config/ItemsData");
        for (var i = 0; i < itemDataSO.ItemDatas.Count; i++)
        {
            ItemDatas.Add(new ItemData(itemDataSO.ItemDatas[i]));
        }
        Resources.UnloadAsset(itemDataSO);
        GameLoop.Instance.Ctx.Get<EventMgr>().TriggerEvent(YOTOEventType.RefreshBagList);
    }
    
}