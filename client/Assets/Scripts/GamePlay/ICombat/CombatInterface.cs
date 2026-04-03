using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 受击者
    /// </summary>
    public interface IDamageable
    {
        TeamId Team { get; }
        bool IsAlive { get; }
        Vector3 Position { get; }
        float MaxHP { get; }
        float Hp { get; }
        float Atk { get; }
        float Def { get; }
        int Level { get; }

        // 返回是否真正造成伤害（例如无敌/护盾可返回 false）
        bool ApplyDamage(in DamageSpec spec, in HitInfo hit, IProjectile instigator);
    }

    /// <summary>
    /// 被索敌者
    /// </summary>
    public interface IThreatTarget
    {
        TeamId Team { get; }
        bool IsTargetable { get; }
        Vector3 Position { get; }
        Vector3 AimPoint { get; } // 塔/枪瞄准点（头/胸/弱点）
    }


    /// <summary>
    /// 武器
    /// </summary>
    public interface IWeapon
    {
        TeamId Team { get; }
        Vector3 Owner { get; }

        int WeaponId { get; }

        // 统一入口：枪和塔都是 Fire(FireRequest)
        bool TryFire(in FireRequest request);
    }

    /// <summary>
    /// 子弹
    /// </summary>
    public interface IProjectile
    {
        bool IsAlive { get; }
        void Init(in FireRequest request);
    }
}