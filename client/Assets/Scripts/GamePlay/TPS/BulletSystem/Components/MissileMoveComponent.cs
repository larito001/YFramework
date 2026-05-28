using UnityEngine;

/// <summary>
/// 贝塞尔曲线导弹移动：沿三阶贝塞尔从 Start 飞向 End，控制点 P1/P2 决定弧线形状。
/// 节奏由 Duration 控制（不依赖 Velocity.magnitude），到 t&gt;=1 自动 Despawn。
/// 飞行过程中每帧做线段 Raycast：撞墙提前 Despawn（保持和 BulletMoveComponent 命中语义一致）。
///
/// 朝向：每帧把"本段位移/dt"写回 Owner.Velocity，BulletView 用这个值算 LookRotation，
///       看起来就是机头沿切线方向飞。
///
/// 配方由 <see cref="BezierMissileEffect"/> 在 spawn 时填好；这个组件本身不算控制点。
/// </summary>
public class MissileMoveComponent : IBulletComponent
{
    /// <summary>贝塞尔起点 P0（发射时的枪口位置）。</summary>
    public Vector3 Start;
    /// <summary>贝塞尔第一控制点 P1（沿发射初始朝向往前推，决定离手段弧度）。</summary>
    public Vector3 Control1;
    /// <summary>贝塞尔第二控制点 P2（一般在终点上方某高度，决定下落段弧度）。</summary>
    public Vector3 Control2;
    /// <summary>贝塞尔终点 P3（鼠标投影点 = Weapon.FireTarget）。</summary>
    public Vector3 End;
    /// <summary>飞完整条曲线的总时间（秒）。Duration 越长弧度看起来越"慢"。</summary>
    public float Duration = 1f;
    /// <summary>沿途命中过滤层。默认所有层。</summary>
    public LayerMask HitLayers = ~0;
    /// <summary>命中目标时使用的卡肉分级。FireEffect 在 spawn 时按武器类型设。</summary>
    public HitstopTier HitstopTier = HitstopTier.Long;

    private float elapsed;
    private BulletManager bulletMgr;
    private ActorWorld world;
    // RaycastNonAlloc buffer：复用避免每帧 alloc。
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
        elapsed = 0f;
        base.Detach();
    }

    public override void Tick(float dt)
    {
        if (Owner == null) return;

        Owner.LifetimeRemaining -= dt;
        elapsed += dt;

        // t 推进到 1 → 落地（在终点引爆）
        float t = Duration > 0.001f ? elapsed / Duration : 1f;
        if (t >= 1f)
        {
            // 段尾再做一次 Raycast，覆盖落点附近近距离命中。已 hit 则 CastAndHit 内部已 Despawn + 设位置，直接返回。
            if (!CastAndHit(Owner.Position, End))
            {
                Owner.Position = End;
#if UNITY_EDITOR
                Debug.Log($"[Missile] arrived @ End (no damage), dmg={Owner.Damage}");
#endif
                bulletMgr?.Despawn(Owner);
            }
            return;
        }

        var prev = Owner.Position;
        var next = Bezier(t);
        if (CastAndHit(prev, next)) return;

        // view 用 Velocity 决定朝向，写"瞬时段速度"上去
        Owner.Velocity = (next - prev) / Mathf.Max(dt, 1e-4f);
        Owner.Position = next;
    }

    /// <summary>对 [from, to] 这段做 Raycast，撞到东西就引爆 + Despawn 并返回 true。
    /// 自己（OwnerCharacterId）的 collider 被忽略，避免起飞早期撞自己。</summary>
    private bool CastAndHit(Vector3 from, Vector3 to)
    {
        var step = to - from;
        var dist = step.magnitude;
        if (dist < 1e-4f) return false;
        var dir = step / dist;

        if (!DamageRouter.RaycastSkipActor(from, dir, dist, HitLayers,
                Owner.OwnerCharacterId, raycastBuf, out var hit))
            return false;

#if UNITY_EDITOR
        Debug.Log($"[Missile] hit {hit.collider.name} @ {hit.distance:F2}m, dmg={Owner.Damage}");
#endif
        var info = new DamageInfo(Owner.Damage, Owner.OwnerCharacterId, HitstopTier);
        DamageRouter.TryHitAndDamage(hit.collider, world, in info);
        Owner.Position = hit.point;
        bulletMgr?.Despawn(Owner);
        return true;
    }

    /// <summary>三阶贝塞尔 B(t) = (1-t)³P0 + 3(1-t)²t·P1 + 3(1-t)t²·P2 + t³P3</summary>
    private Vector3 Bezier(float t)
    {
        float u = 1f - t;
        float uu = u * u;
        float tt = t * t;
        return uu * u * Start
             + 3f * uu * t * Control1
             + 3f * u * tt * Control2
             + tt * t * End;
    }
}
