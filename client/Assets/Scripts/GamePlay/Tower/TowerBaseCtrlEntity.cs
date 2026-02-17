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

    public override string GetModelLayer()
    {
        return "Agent";
    }

    protected override void AfterInstanceGObj()
    {

        // hud = objTrans.GetComponentInChildren<TowerBaseHud>();
        // hud.Init(this);
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

    
        // TowerManager.Instance.RemoveBaseCtrl(this);
        RecoverObject();
    }

    public override void OnObjectClick()
    {
        base.OnObjectClick();
        // OnBaseClick();
        if (_towerEntity == null)
        {
            // TowerManager.Instance.ClickTower(this);
            // hud.OnShow(); 
        }

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
        // TowerManager.Instance.AddBaseCtrl(this);
    }

    public void GenerateTowerById(int id)
    {
        TowerId = id;
        // _towerEntity= TowerManager.Instance.GetTowerById(id,this);
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
            // foreach (var instanceTowerData in TowerManager.Instance.towerDatas)
            // {
            //     if (instanceTowerData.Id == TowerId)
            //     {
            //         foreach (var removeBackRe in instanceTowerData.RemoveBackRes)
            //         {
            //             BagPlugin.Instance.AddItem(removeBackRe.x,removeBackRe.y);
            //         } 
            //     }
            // }
            
            
            TowerId = -1;
            TowerEntity.pool.RecoverItem(_towerEntity);
            _towerEntity = null;
        }
    }
}
