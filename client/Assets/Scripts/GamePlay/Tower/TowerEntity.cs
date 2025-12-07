
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

    private float timer = 0;
    private float attackInterval = 3f;

    public override void YOTOUpdate(float deltaTime)
    {
        if (objTrans == null) return;
        timer+= deltaTime;
        if (timer >= attackInterval)
        {
            timer-= attackInterval;
            Vector3 pos = new Vector3();
            if (EnemiesManager.instance.GetEnemyPos(objTrans.position,out pos))
            {
                // BaseBulletEntity b = BaseBulletEntity.pool.GetItem(new BulletConfig()
                // {
                //     name = "Bullet/bullet",
                //     moveSpeed = 10,
                //     damage = 1,
                //     duration = 10,
                // });
                BaseBulletEntity b = BaseBulletEntity.pool.GetItem(new BulletConfig()
                {
                    name = "Bullet/bulletFire",
                    moveSpeed =0,
                    attackType = AttackType.Near,
                    damage = 1,
                    TrggerCount = 999,
                    duration = 3,
                    triggerTimer = 0.2f
                });
                pos += new Vector3(0, 1.5f, 0);
                b.Fire(ObjTrans.position, pos - ObjTrans.position);
            }
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
