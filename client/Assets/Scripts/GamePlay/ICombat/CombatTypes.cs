using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 伤害类型
    /// </summary>
    public enum DamageType
    {
        Kinetic,
        Fire,
        Ice,
        Electric,
        Explosive
    }

    /// <summary>
    /// 组id
    /// </summary>
    public readonly struct TeamId
    {
        public readonly int Value;
        public TeamId(int value) => Value = value;
        public override string ToString() => Value.ToString();
    }
    /// <summary>
    /// 伤害信息
    /// </summary>
    public struct DamageSpec
    {
        public float Amount;
        public DamageType Type;

        // 可扩展：爆头、穿甲、元素倍率等
        public float CritChance; // 0..1
        public float CritMultiplier; // >= 1
        public float ArmorPenetration; // 0..1
    }

    /// <summary>
    /// 受击信息
    /// </summary>
    public struct HitInfo
    {
        public Vector3 Point;
        public Vector3 Normal;
        public Vector3 Direction; // 入射方向（从发射者到目标）
    }
    /// <summary>
    /// 武器发射子弹的请求信息
    /// </summary>
    public struct FireRequest
    {
        public TeamId Team;
        public IWeapon Owner;          // 发射者（枪/塔的根）
        public Vector3 Origin;           // 发射起点
        public Vector3 Direction;        // 发射方向（单位向量）
        public float MaxDistance;        // hitscan 用
        public DamageSpec Damage;        // 伤害规格
        public WeaponConfigSO Config;    // 武器配置（用于弹道、散布、速度等）
    }
    /// <summary>
    /// buff信息
    /// </summary>
    public readonly struct EffectContext
    {
        public readonly IDamageable Damageable;
        public readonly IEffectReceiver Receiver;
        public EffectContext( IDamageable damageable, IEffectReceiver receiver)
        {
            Damageable = damageable;
            Receiver = receiver;
        }
    }

}