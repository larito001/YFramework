using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public enum ItemType
{
    Currency,//货币
    Res,//资源
    Weapon,//武器
    UsableRes,//可使用资源
}

[System.Serializable]
public class ItemData
{
   public int Id;
   public string Name;
   public ItemType ItemType;
   public Sprite Icon;

   public ItemData()
   {
       
   }
   public ItemData(ItemData itemData)
   {
       Id = itemData.Id;
       Name = itemData.Name;
       ItemType = itemData.ItemType;
       Icon = itemData.Icon;
   }
}

[System.Serializable]
public class TowerData
{
    public int Id;
    public string Name;
    public float BuildTime;
    public List<Vector2Int> UseIdAndNumber = new();
    public float HP;
    public float Attack;
    public float AttackSpeed;
    public float Range;
    public Sprite Icon;
   public TowerData()
    { 
    }

    public TowerData(TowerData towerData)
    {
        Id = towerData.Id;
        Name = towerData.Name;
        BuildTime = towerData.BuildTime;
        UseIdAndNumber = towerData.UseIdAndNumber;
        HP = towerData.HP;
        Attack = towerData.Attack;
        AttackSpeed = towerData.AttackSpeed;
        Range = towerData.Range;
        Icon = towerData.Icon;
    }
   
}
