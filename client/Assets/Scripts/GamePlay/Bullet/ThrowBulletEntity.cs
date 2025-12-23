using UnityEngine;

public class ThrowBulletEntity : BaseBulletEntity, PoolItem<BulletConfig>
{
    public static DataObjPool<ThrowBulletEntity, BulletConfig> pool =
        new DataObjPool<ThrowBulletEntity, BulletConfig>("ThrowBulletEntity", 50);

    public override void YOTOFixedUpdate(float deltaTime)
    {
        if (!isLive) return;

        if (objTrans == null) return;
        timer += deltaTime;
        if (timer >= _config.duration)
        {
            ThrowBulletEntity.pool.RecoverItem(this);
            return;
        } // 真实抛体参数


        const float gravity = 9.8f;

        timer += deltaTime;

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

        objTrans.position = startPos + horizontalOffset + Vector3.up * yOffset;

        stayTimer += deltaTime;
        //非延迟触发
        if (_config.triggerTimer == 0 && triggerCount > 0)
        {
            foreach (var theVictim in victims)
            {
                theVictim.OnHurt(_fireRole, _config.damage);
                triggerCount--;
                if (triggerCount <= 0)
                {
                    pool.RecoverItem(this);
                    break;
                }
            }

            victims.Clear();
            return;
        }
    }
}