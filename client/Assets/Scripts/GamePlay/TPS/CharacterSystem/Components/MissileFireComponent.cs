using UnityEngine;

/// <summary>
/// 导弹武器开火组件：装在 Weapon 上，按 FireInterval 节流。
/// 每次开火读 Owner.FireOrigin / FireDirection / FireTarget 算出贝塞尔四个控制点，
/// 调 BulletManager.SpawnBullet 创建无移动组件的子弹，再 Add 一个 MissileMoveComponent 让它沿曲线飞。
///
/// 弹道：抛物弧线 —— P0=枪口，P1=沿初始朝向推 ForwardPushDist，P2=终点正上方 ArcHeight，P3=鼠标点。
/// 整条曲线在 FlightDuration 秒内飞完。中途撞墙提前爆，否则到点引爆（必中目标）。
///
/// 复用普通枪的模型/动画/控制器：把这个组件替换 ProjectileFireComponent 装在同样的 Weapon 配方上即可，
/// 武器模型、Mount socket、切枪动画、IsShooting/IsAiming 流程完全不变。
/// </summary>
public class MissileFireComponent : IWeaponComponent
{
    /// <summary>两次发射最小间隔（秒）。0.6 = 大约每秒 1.7 发，导弹手感。</summary>
    public float FireInterval = 0.6f;
    /// <summary>单发伤害（命中时由 MissileMoveComponent 应用，目前仅 log）。</summary>
    public float Damage = 80f;
    /// <summary>整条贝塞尔飞完的总时间（秒）。值越大弧线越慢，可视性越强。</summary>
    public float FlightDuration = 1.0f;
    /// <summary>P1 沿初始朝向往前推的距离（米）。决定离手段弧度。</summary>
    public float ForwardPushDist = 2f;
    /// <summary>P2 在终点正上方抬高的高度（米）。决定下落段弧度。</summary>
    public float ArcHeight = 4f;
    /// <summary>子弹寿命兜底（秒）。一般用 FlightDuration + 余量，超过时间强制 Despawn 防止永驻。</summary>
    public float LifetimeSlack = 0.5f;
    /// <summary>每发开火的相机抖动强度。0 = 不抖（默认，给 NPC 武器用）；导弹这种大武器可调到 0.25~0.4。</summary>
    public float RecoilShakeIntensity = 0f;
    /// <summary>每发开火的相机抖动衰减时长（秒）。导弹之类的大震一般 0.15~0.25。</summary>
    public float RecoilShakeDuration = 0.15f;

    private float cooldown;
    private BulletManager bulletMgr;
    private CameraManager cameraMgr;

    public override void Attach(Weapon owner)
    {
        Ctx?.TryGet(out bulletMgr);
        Ctx?.TryGet(out cameraMgr);
    }

    public override void Detach()
    {
        bulletMgr = null;
        cameraMgr = null;
        cooldown = 0f;
        base.Detach();
    }

    public override void Tick(float dt)
    {
        if (Owner == null || !Owner.IsEquipped) return;
        if (cooldown > 0f) cooldown -= dt;
        if (!Owner.FireIntent) return;
        if (cooldown > 0f) return;
        if (bulletMgr == null) return;
        if (Owner.MagCapacity > 0 && Owner.CurrentAmmo <= 0) return;

        cooldown = FireInterval;
        if (Owner.MagCapacity > 0) Owner.CurrentAmmo--;
        Owner.ShootEvent = true; // 喂 WeaponComponent，下一帧转写 Owner(Character).Shoot 给 view SetTrigger

        // 控制点：P0=枪口、P1=朝前推、P2=目标上方、P3=目标点
        var p0 = Owner.FireOrigin;
        var p3 = Owner.FireTarget;
        var p1 = p0 + Owner.FireDirection * ForwardPushDist;
        var p2 = p3 + Vector3.up * ArcHeight;

        var bullet = bulletMgr.SpawnBullet(p0, FlightDuration + LifetimeSlack, Damage, Owner.OwnerCharacterId);
        bullet.Add(new MissileMoveComponent
        {
            Start = p0,
            Control1 = p1,
            Control2 = p2,
            End = p3,
            Duration = FlightDuration,
        });

        if (RecoilShakeIntensity > 0f && cameraMgr?.Shake != null)
            cameraMgr.Shake.Shake(RecoilShakeDuration, RecoilShakeIntensity);
    }
}
