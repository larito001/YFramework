using UnityEngine;

/// <summary>
/// 投射物开火组件：装在 Weapon 上。按 FireInterval 节流，从 Owner.FireOrigin 沿 FireDirection
/// 调 BulletManager.Spawn 出实体子弹。命中检测/伤害由 Bullet 自己负责（BulletMoveComponent）。
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

    private float cooldown;
    private BulletManager bulletMgr;

    public override void Attach(Weapon owner)
    {
        base.Attach(owner);
        var ctx = GameLoop.Instance != null ? GameLoop.Instance.Ctx : null;
        if (ctx != null) ctx.TryGet(out bulletMgr);
    }

    public override void Tick(float dt)
    {
        if (Owner == null || !Owner.IsEquipped) return;
        if (cooldown > 0f) cooldown -= dt;
        if (!Owner.FireIntent) return;
        if (cooldown > 0f) return;
        if (bulletMgr == null) return;

        cooldown = FireInterval;
        var velocity = Owner.FireDirection * BulletSpeed;
        bulletMgr.Spawn(Owner.FireOrigin, velocity, BulletLifetime, Damage, Owner.OwnerCharacterId);
    }
}
