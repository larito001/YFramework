
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TowerEntity: ObjectBase, PoolItem<Transform>
{
    public static DataObjPool<TowerEntity, Transform> pool =
        new DataObjPool<TowerEntity, Transform>("TowerEntity", 20);
    public TowerBaseCtrlEntity towerBaseCtrl;
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
       
    }

    protected override void BeforeRecover(bool isDelete)
    {
        
    }

    public void AfterIntoObjectPool()
    {
        RecoverObject();
    }

    public void SetData(Transform serverData)
    {
        SetInVision(true);
        SetPrefabBundlePath("Tower/TowerRenderer");
        InstanceGObj();
    }
}
