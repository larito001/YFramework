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

    RateHud rateHud;
    private TowerBaseHud towerHud;
    private float CDtimer = 0;
    private float attackCD = 1f;

    public List<EnemyEntity> enemies = new List<EnemyEntity>();

    public IVictim lockTarget = null;
    private bool isCdEnd = false;
    public override void YOTOFixedUpdate(float dt)
    {
        if (objTrans == null) return;

        if (CDtimer > 0)
        {
            CDtimer -= dt;
            
        }
        else
        {
            isCdEnd = true;
        }
        
        enemies.Clear();
        if (EnemiesManager.instance.GetEnemyIsInRange(objTrans.position, 20, enemies))
        {
            lockTarget = GetNearestEnemyPos();
              
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
            //todo:lerp旋转，
            ObjTrans.rotation = Quaternion.Slerp(ObjTrans.rotation, targetRotation, dt*2);
            
            //todo:如果两角相差1度以内则发射
            if (Vector3.Angle(ObjTrans.forward, direction) < 1)
            {
                if (isCdEnd)
                {
                    isCdEnd = false;
                    CDtimer = attackCD;
                 
                    GenerateBullet(lockTarget);
                    lockTarget = null;
                }
            }
            
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
                canAtkWall = false
            });
            startOffset.y += 2.1f;
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
                camp = Camp.Player,
                canAtkWall = false
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
                moveSpeed = 30,
                damage = 15,
                duration = 10,
                TrggerCount = 999,
                triggerTimer = 0,
                attackType = AttackType.Remote,
                camp = Camp.Player, canAtkWall = true
            });
            pos += new Vector3(0, 1.5f, 0);
            startOffset.y += 1.2f;
            startOffset.z += 1f;
        }

        //todo:再加z轴方向

        b.Fire(this, ObjTrans.position + ObjTrans.rotation * startOffset, pos);
    }

    public override void OnObjectClick()
    {
        towerHud.OnShow();
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
        rateHud = ObjTrans.GetComponentInChildren<RateHud>();
        rateHud.Show();
        towerHud = ObjTrans.GetComponentInChildren<TowerBaseHud>();
        towerHud.Init(this);
        towerHud.OnHide();
        rateHud.UpdateRate(properties.HP / properties.MaxHP);
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
        properties = new Properties();
        properties.HP = 100;
        properties.MaxHP = 100;
        properties.OnDead = () =>
        {
            //todo:玩家死亡
            properties.State = RoleState.Dead;
            towerBaseCtrl.RemoveTower();
            // PlayerManager.Instance.Switch();
        };
        properties.Camp = Camp.Player;
        properties.State = RoleState.Alive;
        properties.Level = 1;

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
     
        if (towerBaseCtrl.TowerId == 1001)
        {
            //投石机
            attackCD = 1f;
        }
        else if (towerBaseCtrl.TowerId == 1002)
        {
            //喷火器
            attackCD = 5f;
        }
        else if (towerBaseCtrl.TowerId == 1003)
        {
            //寒冰蛋
            attackCD = 0.5f;
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
        rateHud.UpdateRate(properties.HP / properties.MaxHP);
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

    public void OnLevelUp()
    {
        properties.Level++;
    }
    
    public void RemoveOnBase()
    {
        towerBaseCtrl.RemoveTower();
        
    }
}