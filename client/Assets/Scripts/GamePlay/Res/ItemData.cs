using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ItemType
{
    Weapon,
}

[System.Serializable]
public class ItemData
{
   public int Id;
   public string Name;
   public ItemType ItemType;
   public Sprite Icon;
}
