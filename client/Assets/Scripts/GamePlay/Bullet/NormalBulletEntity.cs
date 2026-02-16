using System.Collections;
using System.Collections.Generic;
using Combat;
using UnityEngine;

public class NormalBulletEntity : BaseBulletEntity, PoolItem<BulletConfig>,IProjectile
{
    public static DataObjPool<NormalBulletEntity, BulletConfig> pool =
        new DataObjPool<NormalBulletEntity, BulletConfig>("NormalBulletEntity", 50);

    public override void YOTOFixedUpdate(float deltaTime)
    {
        if (!isLive) return;
        timer += deltaTime;
        if (timer >= _config.duration)
        {
            DestoryBullet();
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
            victims.Clear();

            if (triggerCount <= 0)
            {
                DestoryBullet();
            }
        }
    }

    public override void DestoryBullet()
    {
        base.DestoryBullet();

        //todo: 生成粒子
        var config = new ParticleEntityData();
        config.pos = objTrans.position;
        config.path = "Bullet/NormalBulletDestory";
        config.scale = 1;
        var particle = ParticleEntity.pool.GetItem(config);
        particle.Play();
        particle.Rotation = Quaternion.LookRotation(-ObjTrans.forward, ObjTrans.up);
        pool.RecoverItem(this);
    }

    public bool IsAlive { get; }
    public void Init(in FireRequest request)
    {
        throw new System.NotImplementedException();
    }

    public void Tick(float dt)
    {
        throw new System.NotImplementedException();
    }
}