using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Combat;
using UnityEngine;

public class TowerEntity : ObjectBase, PoolItem<TowerBaseCtrlEntity>, IWeapon,IFixedTickable
{
    public static DataObjPool<TowerEntity, TowerBaseCtrlEntity> pool =
        new DataObjPool<TowerEntity, TowerBaseCtrlEntity>("TowerEntity", 20);

    public TowerBaseCtrlEntity towerBaseCtrl;

    RateHud rateHud;
    private TowerBaseHud towerHud;
    private float CDtimer = 0;
    private float attackCD = 1f;

    public List<EnemyEntity> enemies = new List<EnemyEntity>();

    // public IVictim lockTarget = null;
    private bool isCdEnd = false;
    

    // private void GenerateBullet(IVictim victim)
    // {
    //     if (ObjTrans == null) return;
    //     var config = new ParticleEntityData();
    //
    //
    //     config.scale = 1;
    //     Vector3 startOffset = new Vector3(0, 0, 0);
    //     Vector3 pos = victim.GetPosition();
    //     BaseBulletEntity b = null;
    //
    //     if (towerBaseCtrl.TowerId == 1001)
    //     {
    //         //投石机
    //         b = ThrowBulletEntity.pool.GetItem(new BulletConfig()
    //         {
    //             name = "Bullet/bulletStone",
    //             moveSpeed = 9,
    //             damage = 30 + 30 * properties.Level * 0.05f,
    //             duration = 5,
    //             TrggerCount = 5,
    //             triggerTimer = 0,
    //             attackType = AttackType.Throw,
    //             camp = Camp.Player,
    //             canAtkWall = false
    //         });
    //         startOffset.y += 2.1f;
    //         startOffset.z += 0.7f;
    //         config.path = "Bullet/StoneBulletFire";
    //         config.pos = ObjTrans.position + ObjTrans.rotation * startOffset;
    //         var particle = ParticleEntity.pool.GetItem(config);
    //         particle.Play();
    //         particle.Rotation = Quaternion.LookRotation(ObjTrans.rotation * startOffset, ObjTrans.up);
    //     }
    //     else if (towerBaseCtrl.TowerId == 1002)
    //     {
    //         //喷火器
    //         b = FireBulletEntity.pool.GetItem(new BulletConfig()
    //         {
    //             name = "Bullet/bulletFire",
    //             moveSpeed = 0,
    //             attackType = AttackType.Near,
    //             damage = 7+7*properties.Level * 0.05f,
    //             TrggerCount = 999,
    //             duration = 4,
    //             triggerTimer = 0.25f,
    //             camp = Camp.Player,
    //             canAtkWall = false
    //         });
    //         pos += new Vector3(0, 1.5f, 0);
    //     }
    //     else if (towerBaseCtrl.TowerId == 1003)
    //     {
    //         //寒冰蛋
    //         b = IceBulletEntity.pool.GetItem(new BulletConfig()
    //         {
    //             name = "Bullet/bulletIce",
    //             moveSpeed = 30,
    //             damage = 10+10*properties.Level * 0.05f,
    //             duration = 10,
    //             TrggerCount = 999,
    //             triggerTimer = 0,
    //             attackType = AttackType.Remote,
    //             camp = Camp.Player, canAtkWall = true
    //         });
    //         pos += new Vector3(0, 1.5f, 0);
    //         startOffset.y += 1.2f;
    //         startOffset.z += 1f;
    //         config.path = "Bullet/IceBulletFire";
    //         config.pos = ObjTrans.position + ObjTrans.rotation * startOffset;
    //         var particle = ParticleEntity.pool.GetItem(config);
    //         particle.Play();
    //         particle.Rotation = Quaternion.LookRotation(ObjTrans.forward, ObjTrans.up);
    //     }
    //
    //     //todo:再加z轴方向
    //     anim.Play();
    //     b.Fire(this, ObjTrans.position + ObjTrans.rotation * startOffset, pos);
    // }

    public override void OnObjectClick()
    {
        towerHud.OnShow(1);
    }



    public override string GetModelLayer()
    {
        return "Agent";
    }

    Animation anim;

    protected override void AfterInstanceGObj()
    {
        rateHud = ObjTrans.GetComponentInChildren<RateHud>(true);
        rateHud.Show();
        towerHud = ObjTrans.GetComponentInChildren<TowerBaseHud>(true);
        towerHud.Init(this);
        towerHud.OnHide();
        // rateHud.UpdateRate(properties.HP / properties.MaxHP);
        anim = ObjTrans.GetComponentInChildren<Animation>();
    }

    protected override void BeforeRecover(bool isDelete)
    {
        rateHud.Hide();
        towerHud.OnHide();
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

 




    public Vector3 GetPosition()
    {
        if (objTrans)
        {
            return objTrans.position;
        }

        return Location;
    }

    List<Vector3> atkSlot = new List<Vector3>();

    public List<Vector3> GetAtkSlot()
    {
        atkSlot.Clear();
        if (ObjTrans != null)
        {
            atkSlot.Add(ObjTrans.position);
        }

        return atkSlot;
    }

    public void OnHurtSomeone()
    {
    }

    public void OnSlowDown(float rate)
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
       

        var config = new ParticleEntityData();
        config.path = "LevelUp/Teleport";
        config.pos = ObjTrans.position+new Vector3(0,0.5f,0);
        config.scale = 1;
        var particle = ParticleEntity.pool.GetItem(config);
        particle.Play(0.3f);
    }

    public void OnFix()
    {
        // GameLoop.Instance.Ctx.Get<FlyTextMgr>().AddText("+" + (properties.MaxHP - properties.HP), objTrans.position, FlyTextType.AddHP);
        // properties.HP = properties.MaxHP;
        // rateHud.UpdateRate(properties.HP / properties.MaxHP);
    }

    public void RemoveOnBase()
    {
        var config = new ParticleEntityData();
        config.path = "Tower/SmokePuff";
        config.pos = ObjTrans.position;
        config.scale = 1;
        var particle = ParticleEntity.pool.GetItem(config);
        particle.Play();
        
        towerBaseCtrl.RemoveTower();
    }

    public TeamId Team { get; }
    public Vector3 Owner { get; }
    public WeaponConfigSO Config { get; }
    public bool TryFire(in FireRequest request)
    {
        return true;
    }

    public void FixedTick(float fdt)
    {
        if (objTrans == null) return;

        if (CDtimer > 0)
        {
            CDtimer -= fdt;
        }
        else
        {
            isCdEnd = true;
        }

        enemies.Clear();
        if (EnemiesManager.instance.GetEnemyIsInRange(objTrans.position, 20, enemies))
        {
            // lockTarget = GetNearestEnemyPos();
        }
        //
        // if (lockTarget != null)
        // {
        //     //todo:让ObjTrans，朝向lockTarget，只旋转y轴
        //     // 计算水平方向（忽略Y轴高度差）
        //     Vector3 direction = lockTarget.GetPosition() - ObjTrans.position;
        //     direction.y = 0f;
        //
        //     // 防止零向量导致异常
        //     if (direction.sqrMagnitude < 0.0001f) return;
        //
        //     // 计算目标旋转
        //     Quaternion targetRotation = Quaternion.LookRotation(direction);
        //
        //     // 直接设置（立即转向）
        //     //todo:lerp旋转，
        //     ObjTrans.rotation = Quaternion.RotateTowards(
        //         ObjTrans.rotation,
        //         targetRotation,
        //         180 * dt
        //     );
        //
        //     //todo:如果两角相差1度以内则发射
        //     if (Vector3.Angle(ObjTrans.forward, direction) < 1)
        //     {
        //         if (isCdEnd)
        //         {
        //             isCdEnd = false;
        //             CDtimer = attackCD;
        //
        //             GenerateBullet(lockTarget);
        //             lockTarget = null;
        //         }
        //     }
        // }
    }
}