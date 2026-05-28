using UnityEngine;

/// <summary>
/// 命中 → HealthComponent 扣血的统一路由。所有产生伤害的组件（近战 / 子弹 / 射线 / 导弹）走这里，
/// 避免重复实现 "Collider → BaseView.ID → ActorWorld → Character → HealthComponent" 反查链。
///
/// 设计：暴露两步原子操作 + 一步合并版。
///   - <see cref="ResolveActorId"/>：collider 反查 actor ID，过滤掉非 actor / 自伤。
///   - <see cref="ApplyToActor"/>：按 ID 扣血。
///   - <see cref="TryHitAndDamage"/>：合并版，不需要去重的场景（子弹/射线/导弹单次命中）。
///
/// 需要去重的（近战 swing 内同 ID 只扣一次）在 ID 解析与扣血之间插自己的 HashSet 检查。
///
/// 不在 HealthComponent 上放静态方法，因为这个 helper 跨系统（要识别 view、要查 world），
/// 放在 TPS 顶级目录更中性。
/// </summary>
public static class DamageRouter
{
    /// <summary>用 collider 反查 Actor.ID。要求受击 GameObject 链上有 BaseView 且 ID&gt;=0。
    /// 自伤（id == attackerId）也过滤掉。</summary>
    public static int ResolveActorId(Collider col, int attackerId)
    {
        if (col == null) return -1;
        var view = col.GetComponentInParent<BaseView>();
        if (view == null || view.ID < 0) return -1;
        if (view.ID == attackerId) return -1;
        return view.ID;
    }

    /// <summary>给指定 actor 扣血。targetId 一般是 <see cref="ResolveActorId"/> 的返回值。
    /// 目标不是 Character / 没有 HealthComponent 都静默返回 false。
    /// hitstopTier：受击卡肉分级，由攻击端在命中瞬间根据子弹/目标类型决定；默认 Long。</summary>
    public static bool ApplyToActor(ActorWorld world, int targetId, int attackerId, float damage,
        HitstopTier hitstopTier = HitstopTier.Long)
    {
        if (world == null || targetId < 0) return false;
        if (!world.TryGet(targetId, out var actor)) return false;
        if (actor is Character target)
        {
            var hp = target.Get<HealthComponent>();
            if (hp == null) return false;
            hp.ApplyDamage(damage, attackerId, hitstopTier);
            return true;
        }
        return false;
    }

    /// <summary>合并版：collider → 扣血一步到位（无需去重场景）。返回命中 actor 的 ID，未命中返回 -1。
    /// hitstopTier：受击卡肉分级，默认 Long。可以在调用前根据子弹类型 + 目标 collider 自定义。</summary>
    public static int TryHitAndDamage(Collider col, ActorWorld world, int attackerId, float damage,
        HitstopTier hitstopTier = HitstopTier.Long)
    {
        int id = ResolveActorId(col, attackerId);
        if (id < 0) return -1;
        ApplyToActor(world, id, attackerId, damage, hitstopTier);
        return id;
    }
}
