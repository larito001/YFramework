using Combat;
using UnityEngine;

public class ThrowBulletEntity : BaseBulletEntity, PoolItem<BulletConfig>,IProjectile,IFixedTickable
{
    public static DataObjPool<ThrowBulletEntity, BulletConfig> pool =
        new DataObjPool<ThrowBulletEntity, BulletConfig>("ThrowBulletEntity", 50);


    public override void DestoryBullet()
    {
        base.DestoryBullet();
        var config = new ParticleEntityData();
        config.pos = objTrans.position;
        config.path = "Bullet/StoneBulletDestory";
        config.scale = 3;
        var particle = ParticleEntity.pool.GetItem(config);
        particle.Play();
        particle.Rotation = Quaternion.LookRotation(-ObjTrans.forward, ObjTrans.up);
        pool.RecoverItem(this);
    }

    public bool IsAlive { get; }
    public void Init(in FireRequest request)
    {
        
    }

    public void Tick(float dt)
    {
   
    }


    public void FixedTick(float fdt)
    {
          if (!isLive) return;

        if (objTrans == null) return;
        timer += fdt;
        if (timer >= _config.duration)
        {
            DestoryBullet();
            return;
        } // 真实抛体参数

        const float gravity = 9.8f;

        // 注意：这里已经有一个 timer += deltaTime 在上面了，所以不需要重复添加
        // timer += deltaTime; // 这行是重复的，应该删除

        // 起点、终点
        Vector3 startPos = pos;
        Vector3 targetPos = _target;

        // 水平分量
        Vector3 delta = targetPos - startPos;
        Vector3 deltaXZ = new Vector3(delta.x, 0f, delta.z);
        float distanceXZ = deltaXZ.magnitude;

        // 初始水平速度（speed 即初速度）
        float horizontalSpeed = _config.moveSpeed;
        Vector3 horizontalDir = deltaXZ.normalized;

        // 飞行时间（由水平匀速决定）
        float totalTime = distanceXZ / horizontalSpeed;

        if (timer >= totalTime)
        {
            objTrans.position = targetPos;
            foreach (var theVictim in victims)
            {
                // theVictim.OnHurt(_fireRole, _config.damage);
            }

            DestoryBullet();
            return;
        }

        // 竖直初速度（距离越近，totalTime 越小，vy 越小，高度自然越低）
        float verticalSpeed =
            (delta.y + 0.5f * gravity * totalTime * totalTime) / totalTime;

        // 水平位移
        Vector3 horizontalOffset =
            horizontalDir * horizontalSpeed * timer;

        // 竖直位移
        float yOffset = verticalSpeed * timer - 0.5f * gravity * timer * timer;

        // 计算当前位置
        Vector3 currentPosition = startPos + horizontalOffset + Vector3.up * yOffset;
        objTrans.position = currentPosition;

        // 计算当前速度向量
        Vector3 currentVelocity = new Vector3(
            horizontalDir.x * horizontalSpeed, // 水平速度 x 分量
            verticalSpeed - gravity * timer, // 垂直速度（考虑重力影响）
            horizontalDir.z * horizontalSpeed // 水平速度 z 分量
        );

        // 设置 forward 方向为当前速度方向（如果速度不为零）
        if (currentVelocity.sqrMagnitude > 0.001f)
        {
            objTrans.forward = currentVelocity.normalized;
        }

        stayTimer += fdt;
    }
}