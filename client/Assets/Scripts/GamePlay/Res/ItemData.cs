using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public enum ItemType
{
    Currency, //货币
    Res, //资源
    Weapon, //武器
    UsableRes, //可使用资源
}

[System.Serializable]
public class ItemData
{
    public int Id;
    public string Name;
    public ItemType ItemType;
    public Sprite Icon;
    public string path;

    public ItemData()
    {
    }

    public ItemData(ItemData itemData)
    {
        Id = itemData.Id;
        Name = itemData.Name;
        ItemType = itemData.ItemType;
        Icon = itemData.Icon;
        path = itemData.path;
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

public enum EnemyType
{
    Normal,
    Far,
    Summon,
    Boss,
}

[System.Serializable]
public class EnemyData
{
    public int id;
    public string name;
    public string path;
    public EnemyType enemyType;
    public float moveSpeed;
    public float atkRange;
    public float indexRange;
    public float hp;
    public float atk;
    public float atkSpeed;

    public EnemyData()
    {
    }

    public EnemyData(EnemyData enemyData)
    {
        id = enemyData.id;
        name = enemyData.name;
        enemyType = enemyData.enemyType;
        moveSpeed = enemyData.moveSpeed;
        indexRange = enemyData.indexRange;
        atkRange = enemyData.atkRange;
        hp = enemyData.hp;
        atk = enemyData.atk;
        atkSpeed = enemyData.atkSpeed;
    }
}

[System.Serializable]
public class EnemyGroupData
{
    public int id;
    public string name;
    public Vector3 locationTemp;
    public List<Vector2Int> enemyIdAndNumber = new();

    public EnemyGroupData()
    {
        
    }
    public EnemyGroupData(EnemyGroupData enemyGroupData)
    {
        id = enemyGroupData.id;
        name = enemyGroupData.name;
        enemyIdAndNumber = enemyGroupData.enemyIdAndNumber;
    }
}