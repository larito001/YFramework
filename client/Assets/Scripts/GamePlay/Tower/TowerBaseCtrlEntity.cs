using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public enum TowerBaseState
{
    None,
    Idel,
    Play,
    Dead,
}
public class TowerBaseCtrlEntity : ObjectBase, PoolItem<object>
{
    // TowerBaseHud hud;
    public TowerBaseState state = TowerBaseState.None;
    public TowerEntity _towerEntity;
    private bool _isEditing = false;
    public static DataObjPool<TowerBaseCtrlEntity, object> pool =
        new DataObjPool<TowerBaseCtrlEntity, object>("TowerBaseCtrlEntity", 20);

    public int TowerId = -1;
  

    public void OnSwitchMode(bool isEditing)
    {
        _isEditing = isEditing;
    }
    

    protected override void AfterInstanceGObj()
    {
    }

    protected override void BeforeRecover(bool isDelete)
    {
    }
    
    public void AfterIntoObjectPool()
    {
        if (_towerEntity != null)
        {
            TowerEntity.pool.RecoverItem(_towerEntity);
            _towerEntity = null;
        }

        RecoverObject();
    }
    
    
    public TowerEntity GetTower()
    {
        return _towerEntity;
    }
    public void SetData(object serverData)
    {
        SetInVision(true);
        SetPrefabBundlePath("Tower/towerBase");
        InstanceGObj();
    }

    public void GenerateTowerById(int id)
    {
        TowerId = id;
        if (_towerEntity != null)
        {
            _towerEntity.Parent = this.objTrans;
            _towerEntity.Location = new Vector3(0, 0, 0);
            _towerEntity.Rotation = Quaternion.identity;
        }
    }

    public void RemoveTower()
    {
        if (_towerEntity != null)
        {
            TowerId = -1;
            TowerEntity.pool.RecoverItem(_towerEntity);
            _towerEntity = null;
        }
    }
}
