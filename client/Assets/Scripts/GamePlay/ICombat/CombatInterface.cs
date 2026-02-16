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
        float ThreatRadius { get; } // 用于“近似体积”/优先级
    }

    /// <summary>
    /// buff接收者
    /// </summary>
    public interface IEffectReceiver
    {
        bool CanReceiveEffects { get; }
        void AddEffect(IStatusEffect effect);
        bool HasEffect(string effectId);
    }

    /// <summary>
    /// buff基类
    /// </summary>
    public interface IStatusEffect
    {
        string Id { get; }
        float Duration { get; } // <=0 表示瞬时
        void OnApply(EffectContext ctx);
        void Tick(EffectContext ctx, float dt);
        void OnRemove(EffectContext ctx);
    }

    /// <summary>
    /// 武器
    /// </summary>
    public interface IWeapon
    {
        TeamId Team { get; }
        Vector3 Owner { get; }
        WeaponConfigSO Config { get; }
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
        void Tick(float dt);
    }
}