using UnityEngine;

/// <summary>
/// 弹道移动组件的共享基类：提供"每帧一段 raycast + 命中扣血 + Despawn"的通用代码。
/// 子类只决定**段是怎么算的**（直线 / 贝塞尔 / 追踪…），命中和清理走基类。
///
/// 当前两个子类：
///   - <see cref="BulletMoveComponent"/>：直线段 = Position + Velocity * dt
///   - <see cref="MissileMoveComponent"/>：贝塞尔段 = Bezier(t) - Bezier(t-dt)
///
/// 仍是 <see cref="IBulletComponent"/>（Owner=Bullet）——抽 base 是为了消除两个子类的重复，
/// 不是为了让它给非 Bullet 用。Bullet 之外的"线段移动 + 命中" 用例（激光、扫描）出现时再考虑泛化。
///
/// 共享字段：
///   <see cref="HitLayers"/>                                   — 命中物理过滤层（effect 侧配置，跟 spec 解耦）
///   <see cref="bulletMgr"/> / <see cref="world"/>             — Attach 时拉好的 service 引用
///   <see cref="raycastBuf"/>                                  — 静态共享 RaycastHit buffer，避免每帧 alloc
///
/// 共享行为：
///   <see cref="CastSegment"/>                                 — 跳过发射者自身的段 raycast
///   <see cref="HandleHitAndDespawn"/>                         — Build DamageInfo from Owner.Damage spec → DamageRouter → Despawn
///
/// **HitstopTier 等伤害参数从 Owner.Damage (DamageSpec) 取**，本组件不重复持字段——避免 spec 配 Long 而 MoveComponent 配 Short 的冲突。
/// </summary>
public abstract class SegmentRaycastMoveBase : IBulletComponent
{
    /// <summary>沿途命中过滤层。默认所有层；建议生产期设为仅含可被击中的实体。
    /// 这是物理 layer mask，跟伤害 spec 解耦——同一种 spec 子弹可能挂不同 LayerMask（玩家枪打敌人层 / 塔枪打玩家层等）。</summary>
    public LayerMask HitLayers = ~0;

    protected BulletManager bulletMgr;
    protected ActorWorld world;
    // RaycastNonAlloc buffer：所有子类共用同一份。8 个够用（弹丸一帧穿 >8 collider 极少）。
    protected static readonly RaycastHit[] raycastBuf = new RaycastHit[8];

    public override void Attach(Bullet owner)
    {
        Ctx?.TryGet(out bulletMgr);
        Ctx?.TryGet(out world);
    }

    public override void Detach()
    {
        bulletMgr = null;
        world = null;
        base.Detach();
    }

    /// <summary>对 [from, to] 这段做 raycast，跳过发射者自身 collider。
    /// dist&lt;1e-4 视为零段，直接返回 false（避免数值误差导致 raycast 失败）。</summary>
    protected bool CastSegment(Vector3 from, Vector3 to, out RaycastHit hit)
    {
        hit = default;
        var step = to - from;
        var dist = step.magnitude;
        if (dist < 1e-4f) return false;
        var dir = step / dist;
        return DamageRouter.RaycastSkipActor(from, dir, dist, HitLayers, Owner.OwnerActorId, raycastBuf, out hit);
    }

    /// <summary>命中处理：从 Owner.Damage spec 组装 DamageInfo（含暴击 roll / 元素 / buff）→ DamageRouter 路由扣血
    /// → 把 Position 拉到命中点 → 通知 BulletManager Despawn。子类拿到 CastSegment=true 后调本方法即可。</summary>
    protected void HandleHitAndDespawn(in RaycastHit hit)
    {
        // DamageSpec 由 FireEffect 在 SpawnBullet 时传入；TeamId / OwnerActorId 由 BulletManager 写入 Owner
        var info = DamageInfo.Build(in Owner.Damage, Owner.OwnerActorId, Owner.TeamId);
        DamageRouter.TryHitAndDamage(hit.collider, world, in info);
        Owner.Position = hit.point;
        bulletMgr?.Despawn(Owner);
    }
}
