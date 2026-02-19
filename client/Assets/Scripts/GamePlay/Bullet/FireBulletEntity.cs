using System.Collections;
using System.Collections.Generic;
using Combat;
using UnityEngine;

public class FireBulletEntity : BaseBulletEntity, PoolItem<BulletConfig>,IProjectile,IFixedTickable
{
    public static DataObjPool<FireBulletEntity, BulletConfig> pool =
        new DataObjPool<FireBulletEntity, BulletConfig>("FireBulletEntity", 10);

  
    public override void DestoryBullet()
    {
        base.DestoryBullet();
        pool.RecoverItem(this);
    }

    public bool IsAlive { get; }
    public void Init(in FireRequest request)
    {
        
    }

    public void Tick(float dt)
    {
        throw new System.NotImplementedException();
    }

    public void FixedTick(float fdt)
    {

        if (!isLive) return;
        timer += fdt;
        // if (timer >= _config.duration || _fireRole.GetProperties().State == RoleState.Dead)
        // {
        //     DestoryBullet();
        //     return;
        // }
        //
        // if (ObjTrans != null)
        // {
        //     ObjTrans.position = _fireRole.GetPosition()+new Vector3(0,1,0);
        //     ObjTrans.forward = _fireRole.GetForward();
        // }
        //
        // stayTimer += deltaTime;
        // //延迟触发
        // if (stayTimer >= _config.triggerTimer && triggerCount > 0)
        // {
        //     stayTimer -= _config.triggerTimer;
        //     foreach (var theVictim in victims)
        //     {
        //         theVictim.OnHurt(_fireRole, _config.damage);
        //         triggerCount--;
        //         if (triggerCount <= 0)
        //         {
        //             break;
        //         }
        //     }
        //
        //     if (triggerCount <= 0)
        //     {
        //         pool.RecoverItem(this);
        //     }
        // }
    }
}
