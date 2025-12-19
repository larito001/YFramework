using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BagPlugin:LogicPluginBase
{
    private List<Vector2Int> ItemList = new ();
    public static BagPlugin Instance;
    public override void Init()
    {
        Instance = this;
    }
    public void ReStar()
    {
        ItemList.Clear();
    }
    public void AddItem(int id,int num)
    {
        for (var i = 0; i < ItemList.Count; i++)
        {
            if (ItemList[i].x == id)
            {
                ItemList[i] = new Vector2Int(id,ItemList[i].y+num);
                return;
            }
        }
        ItemList.Add(new Vector2Int(id,num));
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
    public void RemoveItem(int id,int num)
    {
        for (var i = 0; i < ItemList.Count; i++)
        {
            if (ItemList[i].x == id)
            {
                var lastNum = ItemList[i].y;
                 ItemList[i] = new Vector2Int(id,lastNum-num);
                 if (ItemList[i].y <= 0)
                 {
                     ItemList.RemoveAt(i);
                 }
                
            }
        }

        ItemList.Remove(new Vector2Int(id,num));
    }
    public int GetListCount => ItemList.Count;
    public Vector2Int GetItemByIndex(int index)
    {
        return ItemList[index];
    }
}
