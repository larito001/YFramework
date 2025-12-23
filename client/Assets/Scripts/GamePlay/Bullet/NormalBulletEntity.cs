using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NormalBulletEntity : BaseBulletEntity, PoolItem<BulletConfig>
{
    public static DataObjPool<NormalBulletEntity, BulletConfig> pool =
        new DataObjPool<NormalBulletEntity, BulletConfig>("NormalBulletEntity", 50);

    public override void YOTOFixedUpdate(float deltaTime)
    {
          if (!isLive) return;
        timer += deltaTime;
        if (timer >= _config.duration)
        {
            NormalBulletEntity.pool.RecoverItem(this);
            return;
        }

        if (objTrans)
        {
            objTrans.position += dir * _config.moveSpeed * deltaTime;
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
}
