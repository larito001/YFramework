using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyEntity : ObjectBase, PoolItem<object>
{
    public static DataObjPool<EnemyEntity, object> pool =
        new DataObjPool<EnemyEntity, object>("EnemyEntity", 200);
    SampleEnemyMoveCtrl enemyMoveCtrl;
    protected override void YOTOOnload()
    {
        
    }

    public override void YOTOStart()
    {
      
    }

    public override void YOTOUpdate(float deltaTime)
    {

        if (enemyMoveCtrl != null)
        {
            enemyMoveCtrl.SetPlayerPosition(EnemiesManager.instance.GetPlayerPos());
            CheckDistance();
        }

    }

    private void CheckDistance()
    {
        var dis = objTrans.position - EnemiesManager.instance.GetPlayerPos();
        if (dis.magnitude > 30)
        {
            EnemiesManager.instance.RemoveEnemy(this);
        }
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

    public void SetTarget()
    {
        
    }
    
    protected override void AfterInstanceGObj()
    {
        enemyMoveCtrl = ObjTrans.GetComponent<SampleEnemyMoveCtrl>();
    }

    public void AfterIntoObjectPool()
    {
        RecoverObject();
    }

    public void SetData(object serverData)
    {
        SetInVision(true);
        SetPrefabBundlePath("Enemies/Enemy");
        InstanceGObj();
    }
}
