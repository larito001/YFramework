using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TowerEntity : ObjectBase, PoolItem<TowerBaseCtrlEntity>, IVictim
{
    public static DataObjPool<TowerEntity, TowerBaseCtrlEntity> pool =
        new DataObjPool<TowerEntity, TowerBaseCtrlEntity>("TowerEntity", 20);

    public TowerBaseCtrlEntity towerBaseCtrl;
    Properties properties;

    private float timer = 0;
    private float attackInterval = 1f;

    public List<EnemyEntity> enemies = new List<EnemyEntity>();

    public override void YOTOUpdate(float deltaTime)
    {
        if (objTrans == null) return;
        timer += deltaTime;
        if (timer >= attackInterval)
        {
            timer -= attackInterval;
            Vector3 pos = new Vector3();
            BaseBulletEntity b = null;
            enemies.Clear();
            if (EnemiesManager.instance.GetEnemyIsInRange(objTrans.position, 20, enemies))
            {
                pos = enemies[0].ObjTrans.position;
                if (towerBaseCtrl.TowerId == 1001)
                {
                    //投石机
                    b = BaseBulletEntity.pool.GetItem(new BulletConfig()
                    {
                        name = "Bullet/bulletStone",
                        moveSpeed = 3,
                        damage = 30,
                        duration = 5,
                        TrggerCount = 5,
                        triggerTimer = 0,
                        attackType = AttackType.Throw,
                        camp = Camp.Player,
                    });
                }
                else if (towerBaseCtrl.TowerId == 1002)
                {
                    //喷火器
                    b = BaseBulletEntity.pool.GetItem(new BulletConfig()
                    {
                        name = "Bullet/bulletFire",
                        moveSpeed = 0,
                        attackType = AttackType.Near,
                        damage = 7,
                        TrggerCount = 999,
                        duration = 4,
                        triggerTimer = 0.25f,
                        camp = Camp.Player
                    });
                    pos += new Vector3(0, 1.5f, 0);
                }
                else if (towerBaseCtrl.TowerId == 1003)
                {
                    //寒冰蛋
                    b = BaseBulletEntity.pool.GetItem(new BulletConfig()
                    {
                        name = "Bullet/bullet",
                        moveSpeed = 80,
                        damage = 15,
                        duration = 10,
                        TrggerCount = 999,
                        triggerTimer = 0,
                        attackType = AttackType.Remote,
                        camp = Camp.Player,
                    });
                    pos += new Vector3(0, 1.5f, 0);
                }


                b.Fire(this, ObjTrans.position, pos);
            }
        }
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
        properties.HP = 120;
        properties.OnDead = () =>
        {
            //todo:玩家死亡
            properties.State = RoleState.Dead;
            pool.RecoverItem(this);
            towerBaseCtrl.RemoveTower();
            // PlayerManager.Instance.Switch();
        };
        properties.Camp = Camp.Player;
        properties.State = RoleState.Alive;


        if (towerBaseCtrl.TowerId == 1001)
        {
            //投石机
            attackInterval = 1f;
        }
        else if (towerBaseCtrl.TowerId == 1002)
        {
            //喷火器
            attackInterval = 5f;
        }
        else if (towerBaseCtrl.TowerId == 1003)
        {
            //寒冰蛋
            attackInterval = 0.5f;
        }
    }

    public Transform GetTransform()
    {
        return ObjTrans;
    }

    public int GetId()
    {
        return _entityID;
    }

    public Properties GetProperties()
    {
        return properties;
    }

    public void OnHurt(IVictim fireRole, float hurt)
    {
        if (properties == null || properties.State == RoleState.Dead || objTrans == null) return;
        FlyTextMgr.Instance.AddText(hurt.ToString(), objTrans.position, FlyTextType.Quick);
        properties.HP -= hurt;
        fireRole.OnHurtSomeone();
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