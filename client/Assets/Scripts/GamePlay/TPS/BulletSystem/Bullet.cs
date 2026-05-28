using UnityEngine;

/// <summary>
/// 实体子弹 Actor：纯数据，行为靠 IBulletComponent 组合（移动、命中、追踪…）。
///
/// 字段语义：
///   Position    —— 当前世界坐标。BulletMoveComponent 每帧推进，BulletView 读取设 transform。
///   Velocity    —— 飞行速度向量（m/s）。Spawn 时定，飞行中常量（要做重力/制导再加组件）。
///   LifetimeRemaining —— 剩余寿命（秒），<=0 时 BulletMoveComponent 让 manager Despawn。
///   Damage      —— 命中时扣血量。HealthComponent 接入后实际生效。
///   OwnerCharacterId —— 发射者 character.ID，用于伤害归因 + 避免打到自己（起点已推到 capsule 外，目前不显式过滤）。
/// </summary>
public class Bullet : Actor
{
    public Vector3 Position;
    public Vector3 Velocity;
    public float LifetimeRemaining;
    public float Damage;
    public int OwnerCharacterId = -1;
    /// <summary>命中受击者时使用的卡肉分级。FireEffect 在 spawn 时按子弹类型设；
    /// 命中处理（BulletMoveComponent / MissileMoveComponent）透传给 DamageRouter。默认 Long。
    /// 命中前需要按目标类型再调整的话，在 move 组件的 hit 分支里改 tier 传值即可。</summary>
    public HitstopTier HitstopTier = HitstopTier.Long;
}
