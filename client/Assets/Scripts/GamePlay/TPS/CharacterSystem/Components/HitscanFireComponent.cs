using UnityEngine;

/// <summary>
/// 射线开火组件：装在 Weapon Actor 上。每帧读 Owner.FireIntent / FireOrigin / FireDirection，
/// 按 FireInterval 冷却节流，发 Physics.Raycast，命中通过 <see cref="DamageRouter"/> 扣血。
///
/// 未装备的武器也会 Tick（WeaponManager 不挑），所以开头检查 IsEquipped 早退。
/// </summary>
public class HitscanFireComponent : IWeaponComponent
{
    /// <summary>两次射击最小间隔（秒）。0.1 = 600 RPM 全自动。</summary>
    public float FireInterval = 0.1f;
    /// <summary>单发伤害。命中 Character 时由 HealthComponent 应用。</summary>
    public float Damage = 25f;
    /// <summary>射线最大距离（米）。</summary>
    public float Range = 100f;
    /// <summary>命中过滤层。默认所有层；后续应排除 Player 自身层避免打到自己 capsule。</summary>
    public LayerMask HitLayers = ~0;
    /// <summary>Debug 线显示时长（秒）。</summary>
    public float DebugDrawSeconds = 0.1f;

    private float cooldown;
    private ActorWorld world;

    public override void Attach(Weapon owner)
    {
        Ctx?.TryGet(out world);
    }

    public override void Detach()
    {
        world = null;
        cooldown = 0f;
        base.Detach();
    }

    public override void Tick(float dt)
    {
        if (Owner == null || !Owner.IsEquipped) return;
        if (cooldown > 0f) cooldown -= dt;
        if (!Owner.FireIntent) return;
        if (cooldown > 0f) return;

        cooldown = FireInterval;

        if (Physics.Raycast(Owner.FireOrigin, Owner.FireDirection, out var hit, Range, HitLayers))
        {
            Debug.DrawLine(Owner.FireOrigin, hit.point, Color.red, DebugDrawSeconds);
            Debug.Log($"[Hitscan] {Owner.Name} hit {hit.collider.name} @ {hit.distance:F2}m, dmg={Damage}");
            DamageRouter.TryHitAndDamage(hit.collider, world, Owner.OwnerCharacterId, Damage);
        }
        else
        {
            Debug.DrawRay(Owner.FireOrigin, Owner.FireDirection * Range, Color.yellow, DebugDrawSeconds);
        }
    }
}
