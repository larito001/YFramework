/// <summary>
/// 实体子弹 Actor：纯数据，行为靠 IBulletComponent 组合（移动、命中、追踪…）。
///
/// 字段归属：通用字段（Position / Velocity / LifetimeRemaining / OwnerActorId / TeamId）已下沉到 <see cref="Actor"/> 基类；
/// 本类只持 Bullet 专属字段。
///
/// 字段语义：
///   Damage —— 攻击者侧配置的伤害"配方"（含基础伤害 / 卡肉 / 暴击 / 元素 / buff）。
///             命中时 <see cref="SegmentRaycastMoveBase.HandleHitAndDespawn"/> 调 <see cref="DamageInfo.Build"/> 一行组装 DamageInfo 走 DamageRouter。
///
/// 通用字段（继承自 Actor）：
///   Position           —— 当前世界坐标，View 同步 transform。
///   Velocity           —— 飞行速度向量（m/s）。Spawn 时定，后续由移动组件按需写入（贝塞尔时每帧写切线方向）。
///   LifetimeRemaining  —— 剩余寿命（秒），&lt;=0 时移动组件触发 Despawn。
///   OwnerActorId       —— 发射者 Actor.ID，用于伤害归因 + 自伤过滤。
///   TeamId             —— 发射者阵营，由 FireEffect 传入；HealthComponent 友军过滤。
/// </summary>
public class Bullet : Actor
{
    public DamageSpec Damage;
}
