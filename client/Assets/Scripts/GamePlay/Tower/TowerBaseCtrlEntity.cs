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
    public TowerBaseState state = TowerBaseState.None;
    public TowerEntity _towerEntity;
    private bool _isEditing = false;
    public static DataObjPool<TowerBaseCtrlEntity, object> pool =
        new DataObjPool<TowerBaseCtrlEntity, object>("TowerBaseCtrlEntity", 20);

  
    public void AddTower(TowerEntity towerEntity)
    {
        _towerEntity = towerEntity;
    }
    public void OnSwitchMode(bool isEditing)
    {
        _isEditing = isEditing;
    }

    public override string GetModelLayer()
    {
        return "Agent";
    }

    protected override void AfterInstanceGObj()
    {

    }

    protected override void BeforeRecover(bool isDelete)
    {
    }

    private void OnBaseClick()
    {
        if (_towerEntity == null)
        {
            _towerEntity = TowerEntity.pool.GetItem(this);
            _towerEntity.Parent = this.objTrans;
            _towerEntity.Location = new Vector3(0,1,0);
            _towerEntity.Rotation = Quaternion.identity;
        }
    }

    public void AfterIntoObjectPool()
    {
        if (_towerEntity != null)
        {
            TowerEntity.pool.RecoverItem(_towerEntity);
            _towerEntity = null;
        }
        
        TowerManager.Instance.RemoveBaseCtrl(this);
        RecoverObject();
    }

    public override void OnObjectClick()
    {
        base.OnObjectClick();
        OnBaseClick();
    }

    public void RemoveTower()
    {
        _towerEntity = null;
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
        TowerManager.Instance.AddBaseCtrl(this);
    }
}
