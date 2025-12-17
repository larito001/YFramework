using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BagPlugin 
{
    public Dictionary<int ,int> ItemDict = new Dictionary<int, int>();//id,数量
    public void AddItem(int id,int num)
    {
        if (ItemDict.ContainsKey(id))
        {
            ItemDict[id] += num;
        }
        else
        {
            ItemDict.Add(id, num);
        }
    }
    public void RemoveItem(int id,int num)
    {
        if (ItemDict.ContainsKey(id))
        {
            ItemDict[id] -= num;
            if (ItemDict[id] <= 0)
            {
                ItemDict.Remove(id);
            }
        }
    }
}
