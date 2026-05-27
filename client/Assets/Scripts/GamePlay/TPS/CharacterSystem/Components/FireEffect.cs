using UnityEngine;

/// <summary>
/// FireComponent 触发后"做什么"的策略：spawn 直线子弹 / 抛物导弹 / 直接 raycast 等。
/// 自身不持有任何 cooldown/ammo/shake 状态（那些在 <see cref="FireComponent"/> 里），
/// 只关心"造一发"的具体行为。子类按需读 weapon.FireOrigin / FireDirection / FireTarget。
/// </summary>
public abstract class FireEffect
{
    /// <summary>FireComponent 在通过 cooldown + ammo 门控后调用。
    /// damage 由 FireComponent 传入（武器侧统一配，effect 透传给 bullet 或 DamageRouter）。</summary>
    public abstract void Fire(Weapon weapon, BulletManager bullets, ActorWorld world, float damage);
}

/// <summary>直线弹道：FireOrigin 出发，沿 FireDirection 推一发指定初速的子弹，
/// 由 <see cref="BulletMoveComponent"/> 每帧线段 raycast 自动命中。</summary>
public class LinearProjectileEffect : FireEffect
{
    /// <summary>子弹初速度 (m/s)。60 = 中等速度，看得清飞行轨迹；要更"枪感"调到 100+。</summary>
    public float BulletSpeed = 60f;
    /// <summary>子弹最大寿命（秒）。lifetime * speed = 有效射程。</summary>
    public float BulletLifetime = 2f;

    public override void Fire(Weapon weapon, BulletManager bullets, ActorWorld world, float damage)
    {
        if (bullets == null) return;
        var velocity = weapon.FireDirection * BulletSpeed;
        bullets.Spawn(weapon.FireOrigin, velocity, BulletLifetime, damage, weapon.OwnerCharacterId);
    }
}

/// <summary>三阶贝塞尔曲线导弹：抛物弧线飞向 FireTarget，控制点 P1/P2 决定弧形。
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

    public override void Fire(Weapon weapon, BulletManager bullets, ActorWorld world, float damage)
    {
        if (bullets == null) return;
        var p0 = weapon.FireOrigin;
        var p3 = weapon.FireTarget;
        var p1 = p0 + weapon.FireDirection * ForwardPushDist;
        var p2 = p3 + Vector3.up * ArcHeight;

        var b = bullets.SpawnBullet(p0, FlightDuration + LifetimeSlack, damage, weapon.OwnerCharacterId);
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

/// <summary>瞬时射线：不 spawn 子弹，直接沿 FireDirection 做 Physics.Raycast 命中扣血。
/// 适合 NPC 武器 / 调试武器。命中通过 <see cref="DamageRouter"/> 路由。</summary>
public class HitscanEffect : FireEffect
{
    /// <summary>射线最大距离（米）。</summary>
    public float Range = 100f;
    /// <summary>命中过滤层。默认所有层；后续应排除 Player 自身层避免打到自己 capsule。</summary>
    public LayerMask HitLayers = ~0;
    /// <summary>Debug 线显示时长（秒）。</summary>
    public float DebugDrawSeconds = 0.1f;

    public override void Fire(Weapon weapon, BulletManager bullets, ActorWorld world, float damage)
    {
        if (Physics.Raycast(weapon.FireOrigin, weapon.FireDirection, out var hit, Range, HitLayers))
        {
            Debug.DrawLine(weapon.FireOrigin, hit.point, Color.red, DebugDrawSeconds);
            Debug.Log($"[Hitscan] {weapon.Name} hit {hit.collider.name} @ {hit.distance:F2}m, dmg={damage}");
            DamageRouter.TryHitAndDamage(hit.collider, world, weapon.OwnerCharacterId, damage);
        }
        else
        {
            Debug.DrawRay(weapon.FireOrigin, weapon.FireDirection * Range, Color.yellow, DebugDrawSeconds);
        }
    }
}
