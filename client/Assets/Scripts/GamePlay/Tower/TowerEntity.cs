
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TowerEntity: ObjectBase, PoolItem<TowerBaseCtrlEntity>,IVictim
{
    public static DataObjPool<TowerEntity, TowerBaseCtrlEntity> pool =
        new DataObjPool<TowerEntity, TowerBaseCtrlEntity>("TowerEntity", 20);
    public TowerBaseCtrlEntity towerBaseCtrl;
    Properties properties;
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
                BaseBulletEntity b = BaseBulletEntity.pool.GetItem(new BulletConfig()
                {
                    name = "Bullet/bullet",
                    moveSpeed = 10,
                    damage = 2,
                    duration = 10,
                    TrggerCount=1,
                    triggerTimer=0,
                    attackType = AttackType.Remote,
                    camp = Camp.Player,
                });
                // BaseBulletEntity b = BaseBulletEntity.pool.GetItem(new BulletConfig()
                // {
                //     name = "Bullet/bulletFire",
                //     moveSpeed =0,
                //     attackType = AttackType.Near,
                //     damage = 1,
                //     TrggerCount = 999,
                //     duration = 3,
                //     triggerTimer = 0.5f,
                //     camp  = Camp.Player
                // });
                pos += new Vector3(0, 1.5f, 0);
                b.Fire(this,ObjTrans.position, pos - ObjTrans.position);
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
        if (!ObjTrans.gameObject.TryGetComponent<TheVictim>(out TheVictim victim))
        {
            victim = ObjTrans.gameObject.AddComponent<TheVictim>();
        }

        victim.Init(new Vector3(5, 1, 5), this);
    }

    protected override void BeforeRecover(bool isDelete)
    {
        
    }

    public void AfterIntoObjectPool()
    {
        RecoverObject();
    }

    public void SetData(TowerBaseCtrlEntity serverData)
    {
        towerBaseCtrl = serverData;
        SetInVision(true);
        SetPrefabBundlePath("Tower/TowerRenderer");
        InstanceGObj();
        properties = new Properties();
        properties.HP = 100;
        properties.OnDead = () =>
        {
            //todo:玩家死亡
            properties.State = RoleState.Dead;
            pool.RecoverItem( this);
            towerBaseCtrl.RemoveTower();
            // PlayerManager.Instance.Switch();
        };
        properties.Camp= Camp.Player;
        properties.State = RoleState.Alive;
    }

    public Properties GetProperties()
    {
        return  properties;
    }

    public void OnHurt(IVictim fireRole, float hurt)
    {
        FlyTextMgr.Instance.AddText(hurt.ToString(), objTrans.position, FlyTextType.Quick);
        properties.HP-= hurt;
        fireRole.OnHurtSomeone();
    }

    public void OnEnter(Collider other)
    {
    
    }

    public void OnExit(Collider other)
    {
       
    }

    public Vector3 GetPosition()
    {
        if (objTrans)
        {
            return objTrans.position;
        }
        return Location;
    }

    public void OnHurtSomeone()
    {
        
    }
}
