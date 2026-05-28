using UnityEngine;

/// <summary>
/// 子弹移动 + 段内命中检测 + 寿命管理。
///
/// 每帧把 Velocity*dt 当成一段线段做 Physics.Raycast（通过 <see cref="DamageRouter.RaycastSkipActor"/> 过滤发射者自身）：
///   - 命中：log + Position 设到 hit.point + Despawn。
///   - 未命中：Position += step，继续飞。
/// 寿命到 → Despawn。
/// </summary>
public class BulletMoveComponent : IBulletComponent
{
    public LayerMask HitLayers = ~0;
    /// <summary>命中目标时使用的卡肉分级。FireEffect 在 spawn 时按武器类型设，命中时透传给 DamageInfo。</summary>
    public HitstopTier HitstopTier = HitstopTier.Long;

    private BulletManager bulletMgr;
    private ActorWorld world;
    // RaycastNonAlloc buffer：复用避免每帧 alloc。8 个够用（子弹一帧穿过 >8 个碰撞体的场景极少）。
    private static readonly RaycastHit[] raycastBuf = new RaycastHit[8];

    public override void Attach(Bullet owner)
    {
        Ctx?.TryGet(out bulletMgr);
        Ctx?.TryGet(out world);
    }

    public override void Detach()
    {
        bulletMgr = null;
        world = null;
        base.Detach();
    }

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
        var dir = step / dist;

        if (DamageRouter.RaycastSkipActor(Owner.Position, dir, dist, HitLayers,
                Owner.OwnerCharacterId, raycastBuf, out var hit))
        {
#if UNITY_EDITOR
            Debug.Log($"[Bullet] hit {hit.collider.name} @ {hit.distance:F2}m, dmg={Owner.Damage}");
#endif
            var info = new DamageInfo(Owner.Damage, Owner.OwnerCharacterId, HitstopTier);
            DamageRouter.TryHitAndDamage(hit.collider, world, in info);
            Owner.Position = hit.point;
            bulletMgr?.Despawn(Owner);
            return;
        }

        Owner.Position += step;
    }
}
