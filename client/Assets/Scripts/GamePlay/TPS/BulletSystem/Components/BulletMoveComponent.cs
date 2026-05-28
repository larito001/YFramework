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

        // RaycastNonAlloc + 跳过发射者自身：玩家鼠标停在自己身上时方向已被 WeaponComponent 兜底回 forward，
        // 但起点（forward 0.6m）仍可能离自己 capsule 太近被命中——这里二次防御过滤掉自己。
        int hitCount = Physics.RaycastNonAlloc(Owner.Position, dir, raycastBuf, dist, HitLayers);
        if (hitCount > 0)
        {
            int bestIdx = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                var h = raycastBuf[i];
                // 跳过发射者自己的 collider：让子弹直接穿过自己，不消耗、不计伤
                var view = h.collider.GetComponentInParent<BaseView>();
                if (view != null && view.ID == Owner.OwnerCharacterId) continue;
                if (h.distance < bestDist) { bestDist = h.distance; bestIdx = i; }
            }
            if (bestIdx >= 0)
            {
                var hit = raycastBuf[bestIdx];
                Debug.Log($"[Bullet] hit {hit.collider.name} @ {hit.distance:F2}m, dmg={Owner.Damage}");
                // 卡肉 tier 透传：默认用子弹自带的 HitstopTier；要按目标类型动态决定的话在这里 inspect hit.collider 改 tier
                DamageRouter.TryHitAndDamage(hit.collider, world, Owner.OwnerCharacterId, Owner.Damage, Owner.HitstopTier);
                Owner.Position = hit.point;
                bulletMgr?.Despawn(Owner);
                return;
            }
            // 所有 hit 都是自己 → 视作未命中，继续前进（穿过自己 capsule）
        }

        Owner.Position += step;
    }
}
