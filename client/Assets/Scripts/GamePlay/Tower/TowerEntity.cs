using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

    public IVictim lockTarget = null;

    public override void YOTOFixedUpdate(float deltaTime)
    {
        if (objTrans == null) return;
        timer += deltaTime;
        if (timer >= attackInterval)
        {
            timer -= attackInterval;

            enemies.Clear();
            if (EnemiesManager.instance.GetEnemyIsInRange(objTrans.position, 20, enemies))
            {
                lockTarget = GetNearestEnemyPos();
                GenerateBullet(lockTarget);
            }
        }

        if (lockTarget != null)
        {
            //todo:让ObjTrans，朝向lockTarget，只旋转y轴
            // 计算水平方向（忽略Y轴高度差）
            Vector3 direction = lockTarget.GetPosition() - ObjTrans.position;
            direction.y = 0f;

            // 防止零向量导致异常
            if (direction.sqrMagnitude < 0.0001f) return;

            // 计算目标旋转
            Quaternion targetRotation = Quaternion.LookRotation(direction);

            // 直接设置（立即转向）
            ObjTrans.rotation = targetRotation;
        }
    }

    private void GenerateBullet(IVictim victim)
    {
        Vector3 startOffset = new Vector3(0, 0, 0);
        Vector3 pos = victim.GetPosition();
        BaseBulletEntity b = null;
        if (towerBaseCtrl.TowerId == 1001)
        {
            //投石机
            b = ThrowBulletEntity.pool.GetItem(new BulletConfig()
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
            startOffset.y+= 2.1f;
            startOffset.z += 0.7f;
        }
        else if (towerBaseCtrl.TowerId == 1002)
        {
            //喷火器
            b = FireBulletEntity.pool.GetItem(new BulletConfig()
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
            startOffset.y += 1f;
            startOffset.z += 1f;
        }
        else if (towerBaseCtrl.TowerId == 1003)
        {
            //寒冰蛋
            b = NormalBulletEntity.pool.GetItem(new BulletConfig()
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
            startOffset.y += 1.2f;
            startOffset.z += 1f;
        }

        //todo:再加z轴方向

        b.Fire(this, ObjTrans.position+ ObjTrans.rotation * startOffset , pos);
    }

    private IVictim GetNearestEnemyPos()
    {
        return enemies.OrderBy(x => Vector3.Distance(x.GetPosition(), ObjTrans.position)).FirstOrDefault();
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

        string path = "Tower/TowerRendererNormal";
        if (towerBaseCtrl.TowerId == 1001)
        {
            path = "Tower/TowerRendererThrow";
        }
        else if (towerBaseCtrl.TowerId == 1002)
        {
            path = "Tower/TowerRendererFire";
        }
        else
        {
            path = "Tower/TowerRendererNormal";
        }

        SetPrefabBundlePath(path);
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

    public Vector3 GetForward()
    {
        if (objTrans != null)
        {
            return objTrans.forward;
        }

        return Vector3.down;
    }
}