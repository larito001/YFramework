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
    TowerBaseModel model;
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
    protected override void YOTOOnload()
    {
        
    }

    public override void YOTOStart()
    {
       
    }

    public override void YOTOUpdate(float deltaTime)
    {
        
    }

    public override void YOTONetUpdate()
    {
        
    }

    public override void YOTOFixedUpdate(float deltaTime)
    {
        
    }

    public override void YOTOOnHide()
    {
       
    }

    protected override void AfterInstanceGObj()
    {
        model = objTrans.GetComponent<TowerBaseModel>();
        model.OnClickEvent = OnBaseClick;
    }

    protected override void BeforeRecover(bool isDelete)
    {
        if(model!=null)
        model.OnClickEvent = null;
    }

    private void OnBaseClick()
    {
        if (_towerEntity == null)
        {
            _towerEntity = TowerEntity.pool.GetItem(objTrans);
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

    public void SetData(object serverData)
    {
        SetInVision(true);
        SetPrefabBundlePath("Tower/towerBase");
        InstanceGObj();
        TowerManager.Instance.AddBaseCtrl(this);
    }
}
