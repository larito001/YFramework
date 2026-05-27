using UnityEngine;

/// <summary>
/// 子弹移动 + 段内命中检测 + 寿命管理。
///
/// 每帧把 Velocity*dt 当成一段线段做 Physics.Raycast：
///   - 命中：log + Position 设到 hit.point + Despawn。后续接 HealthComponent 在这里扣血。
///   - 未命中：Position += step，继续飞。
/// 寿命到 → Despawn。
/// 起点已经被持枪人推到 capsule 外（FireOrigin + 0.6m forward），正常飞不会打到自己。
/// </summary>
public class BulletMoveComponent : IBulletComponent
{
    public LayerMask HitLayers = ~0;

    private BulletManager bulletMgr;
    private ActorWorld world;

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

        if (Physics.Raycast(Owner.Position, dir, out var hit, dist, HitLayers))
        {
            Debug.Log($"[Bullet] hit {hit.collider.name} @ {hit.distance:F2}m, dmg={Owner.Damage}");
            DamageRouter.TryHitAndDamage(hit.collider, world, Owner.OwnerCharacterId, Owner.Damage);
            Owner.Position = hit.point;
            bulletMgr?.Despawn(Owner);
            return;
        }

        Owner.Position += step;
    }
}
