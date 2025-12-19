using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New ItemData", menuName = "ItemData")]
public class ItemDataSO : ScriptableObject
{
    [SerializeField]
    public List<ItemData> ItemDatas = new List<ItemData>();
}

