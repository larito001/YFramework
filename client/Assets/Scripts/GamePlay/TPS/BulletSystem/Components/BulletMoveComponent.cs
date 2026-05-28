using UnityEngine;

/// <summary>
/// 直线子弹移动 + 段内命中检测 + 寿命管理。继承 <see cref="SegmentRaycastMoveBase"/>，
/// 命中扣血 / Despawn / raycast 跳自身等公共逻辑由基类提供，本类只算"每帧线段是怎么走的"。
///
/// 每帧把 Velocity*dt 当成一段线段做 raycast：
///   - 命中：调 base.HandleHitAndDespawn 路由扣血 + 写命中点 + Despawn
///   - 未命中：Position += step，继续飞
/// 寿命到 → Despawn。
/// </summary>
public class BulletMoveComponent : SegmentRaycastMoveBase
{
    public override void Tick(float dt)
    {
        if (Owner == null) return;

        Owner.LifetimeRemaining -= dt;
        if (Owner.LifetimeRemaining <= 0f)
        {
            bulletMgr?.Despawn(Owner);
            return;
        }

        var step = Owner.Velocity * dt;
        var dist = step.magnitude;
        if (dist < 1e-4f) return;
        var next = Owner.Position + step;

        if (CastSegment(Owner.Position, next, out var hit))
        {
#if UNITY_EDITOR
            Debug.Log($"[Bullet] hit {hit.collider.name} @ {hit.distance:F2}m, dmg={Owner.Damage}");
#endif
            HandleHitAndDespawn(hit);
            return;
        }

        Owner.Position = next;
    }
}
