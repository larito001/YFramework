using UnityEngine;

/// <summary>
/// 投射物开火组件：装在 Weapon 上。按 FireInterval 节流，从 Owner.FireOrigin 沿 FireDirection
/// 调 BulletManager.Spawn 出实体子弹。命中检测/伤害由 Bullet 自己负责（BulletMoveComponent）。
///
/// Recoil：每次实际开火（cooldown 触发）会调 <c>CameraManager.Shake</c>。
/// RecoilShakeIntensity=0 表示无抖动（适合 NPC 武器），玩家武器在 Factory 里设非零值即可。
/// </summary>
public class ProjectileFireComponent : IWeaponComponent
{
    /// <summary>两次射击最小间隔（秒）。</summary>
    public float FireInterval = 0.1f;
    /// <summary>单发伤害（传给 Bullet，命中时由 Bullet 应用）。</summary>
    public float Damage = 25f;
    /// <summary>子弹初速度 (m/s)。60 = 中等速度，看得清飞行轨迹；要更"枪感"调到 100+。</summary>
    public float BulletSpeed = 60f;
    /// <summary>子弹最大寿命（秒）。lifetime * speed = 有效射程。2s * 60m/s = 120m。</summary>
    public float BulletLifetime = 2f;
    /// <summary>每发开火的相机抖动强度。0 = 不抖（默认，给 NPC 武器用）；玩家武器一般 0.1~0.2。</summary>
    public float RecoilShakeIntensity = 0f;
    /// <summary>每发开火的相机抖动衰减时长（秒）。和 FireInterval 量级接近，抖动有连续感。</summary>
    public float RecoilShakeDuration = 0.08f;

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
        // 弹药检查：MagCapacity=0 表示无限弹药（不扣不查）。否则弹匣空就不开火。
        if (Owner.MagCapacity > 0 && Owner.CurrentAmmo <= 0) return;

        cooldown = FireInterval;
        if (Owner.MagCapacity > 0) Owner.CurrentAmmo--;
        var velocity = Owner.FireDirection * BulletSpeed;
        bulletMgr.Spawn(Owner.FireOrigin, velocity, BulletLifetime, Damage, Owner.OwnerCharacterId);
        Owner.ShootEvent = true; // 喂 WeaponComponent，下一帧转写 Owner(Character).Shoot 给 view SetTrigger

        if (RecoilShakeIntensity > 0f && cameraMgr?.Shake != null)
            cameraMgr.Shake.Shake(RecoilShakeDuration, RecoilShakeIntensity);
    }
}
