using UnityEngine;

/// <summary>
/// FireComponent 触发后"做什么"的策略：spawn 直线子弹 / 抛物导弹 / 直接 raycast 等。
/// 自身不持有任何 cooldown/ammo/shake 状态（那些在 <see cref="FireComponent"/> 里），
/// 也不持伤害配置（伤害走 <see cref="DamageSpec"/>，由调用方传入）——只关心"造一发"的具体行为。
///
/// **签名设计**：Fire 接收 origin / dir / target / ownerActorId / attackerTeamId / in DamageSpec / bullets / world。
/// damage 走 DamageSpec（含暴击 / 元素 / buff），effect 不再单字段持 damage / HitstopTier 等——避免和 spec 字段重复。
/// 技能 / 炮塔 / NPC 等"没 Weapon 但要造投射物"的场景调用方式一致：自己 build spec + 调本方法。
/// </summary>
public abstract class FireEffect
{
    /// <summary>FireComponent 在通过 cooldown + ammo 门控后调用。
    /// origin / dir / target 来自 Weapon.FireOrigin / FireDirection / FireTarget（或调用方自己算）；
    /// ownerActorId = 发射者 Actor.ID（用于自伤过滤 + 伤害归属）；
    /// attackerTeamId = 发射者阵营（用于友军伤害过滤）；
    /// damage = 攻击者侧伤害配方 spec（基础 / 卡肉 / 暴击 / 元素 / buff 全在内），透传给 Bullet 或 DamageInfo.Build。</summary>
    public abstract void Fire(
        Vector3 origin, Vector3 dir, Vector3 target,
        int ownerActorId, int attackerTeamId, in DamageSpec damage,
        BulletManager bullets, ActorWorld world);
}

/// <summary>直线弹道：沿 dir 推一发指定初速的子弹，
/// 由 <see cref="BulletMoveComponent"/> 每帧线段 raycast 自动命中。</summary>
public class LinearProjectileEffect : FireEffect
{
    /// <summary>子弹初速度 (m/s)。60 = 中等速度，看得清飞行轨迹；要更"枪感"调到 100+。</summary>
    public float BulletSpeed = 60f;
    /// <summary>子弹最大寿命（秒）。lifetime * speed = 有效射程。</summary>
    public float BulletLifetime = 2f;

    public override void Fire(
        Vector3 origin, Vector3 dir, Vector3 target,
        int ownerActorId, int attackerTeamId, in DamageSpec damage,
        BulletManager bullets, ActorWorld world)
    {
        if (bullets == null) return;
        var velocity = dir * BulletSpeed;
        // 走 SpawnBullet（不挂默认 MoveComponent）+ 手动 Add BulletMoveComponent，让未来调 LayerMask 时可以配；
        // damage spec 透传给 bullet.Damage，命中时 SegmentRaycastMoveBase Build 成 DamageInfo
        var b = bullets.SpawnBullet(origin, BulletLifetime, in damage, ownerActorId, attackerTeamId, velocity);
        if (b != null) b.Add(new BulletMoveComponent());
    }
}

/// <summary>三阶贝塞尔曲线导弹：抛物弧线飞向 target，控制点 P1/P2 决定弧形。
/// spawn 时算出 4 个控制点塞进 <see cref="MissileMoveComponent"/>，之后由它自己 Tick。</summary>
public class BezierMissileEffect : FireEffect
{
    /// <summary>整条曲线飞完总时间（秒）。值越大弧线越慢，可视性越强。</summary>
    public float FlightDuration = 1.0f;
    /// <summary>P1 沿初始朝向往前推的距离（米）。决定离手段弧度。</summary>
    public float ForwardPushDist = 2f;
    /// <summary>P2 在终点正上方抬高的高度（米）。决定下落段弧度。</summary>
    public float ArcHeight = 4f;
    /// <summary>子弹寿命兜底（秒）。一般 = FlightDuration + 余量，超时强制 Despawn 防永驻。</summary>
    public float LifetimeSlack = 0.5f;

    public override void Fire(
        Vector3 origin, Vector3 dir, Vector3 target,
        int ownerActorId, int attackerTeamId, in DamageSpec damage,
        BulletManager bullets, ActorWorld world)
    {
        if (bullets == null) return;
        var p0 = origin;
        var p3 = target;
        var p1 = p0 + dir * ForwardPushDist;
        var p2 = p3 + Vector3.up * ArcHeight;

        var b = bullets.SpawnBullet(p0, FlightDuration + LifetimeSlack, in damage, ownerActorId, attackerTeamId);
        if (b == null) return;
        b.Add(new MissileMoveComponent
        {
            Start = p0,
            Control1 = p1,
            Control2 = p2,
            End = p3,
            Duration = FlightDuration,
        });
    }
}

/// <summary>瞬时射线：不 spawn 子弹，直接沿 dir 做 Physics.Raycast 命中扣血。
/// 适合 NPC 武器 / 调试武器。命中通过 <see cref="DamageRouter"/> 路由。</summary>
public class HitscanEffect : FireEffect
{
    /// <summary>射线最大距离（米）。</summary>
    public float Range = 100f;
    /// <summary>命中过滤层。默认所有层；后续应排除发射者自身层避免打到自己 capsule。</summary>
    public LayerMask HitLayers = ~0;
    /// <summary>Debug 线显示时长（秒）。</summary>
    public float DebugDrawSeconds = 0.1f;

    // RaycastNonAlloc buffer：复用避免每发开火 alloc。
    private static readonly RaycastHit[] raycastBuf = new RaycastHit[8];

    public override void Fire(
        Vector3 origin, Vector3 dir, Vector3 target,
        int ownerActorId, int attackerTeamId, in DamageSpec damage,
        BulletManager bullets, ActorWorld world)
    {
        // 跳过发射者自身 collider：起点在 forward 0.6m 处但仍可能擦到 capsule 边缘
        if (DamageRouter.RaycastSkipActor(origin, dir, Range, HitLayers,
                ownerActorId, raycastBuf, out var hit))
        {
            Debug.DrawLine(origin, hit.point, Color.red, DebugDrawSeconds);
#if UNITY_EDITOR
            Debug.Log($"[Hitscan] hit {hit.collider.name} @ {hit.distance:F2}m, dmg={damage.BaseDamage}");
#endif
            // hitscan 不 spawn bullet，直接 Build DamageInfo + Apply
            var info = DamageInfo.Build(in damage, ownerActorId, attackerTeamId);
            DamageRouter.TryHitAndDamage(hit.collider, world, in info);
        }
        else
        {
            Debug.DrawRay(origin, dir * Range, Color.yellow, DebugDrawSeconds);
        }
    }
}
