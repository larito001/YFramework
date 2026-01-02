using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FireBulletEntity : BaseBulletEntity, PoolItem<BulletConfig>
{
    public static DataObjPool<FireBulletEntity, BulletConfig> pool =
        new DataObjPool<FireBulletEntity, BulletConfig>("FireBulletEntity", 10);

    public override void YOTOFixedUpdate(float deltaTime)
    {
        base.YOTOUpdate(deltaTime);
          if (!isLive) return;
        timer += deltaTime;
        if (timer >= _config.duration || _fireRole.GetProperties().State == RoleState.Dead)
        {
            DestoryBullet();
            return;
        }

        if (ObjTrans != null)
        {
            ObjTrans.position = _fireRole.GetPosition()+new Vector3(0,1,0);
            ObjTrans.forward = _fireRole.GetForward();
        }

        stayTimer += deltaTime;
        //延迟触发
        if (stayTimer >= _config.triggerTimer && triggerCount > 0)
        {
            stayTimer -= _config.triggerTimer;
            foreach (var theVictim in victims)
            {
                theVictim.OnHurt(_fireRole, _config.damage);
                triggerCount--;
                if (triggerCount <= 0)
                {
                    break;
                }
            }

            if (triggerCount <= 0)
            {
                pool.RecoverItem(this);
            }
        }
    }
    public override void DestoryBullet()
    {
        base.DestoryBullet();
        pool.RecoverItem(this);
    }
}
