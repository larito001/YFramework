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
    /// info 携带 amount + attackerId + 反馈相关字段（卡肉 tier 等）。</summary>
    public static bool ApplyToActor(ActorWorld world, int targetId, in DamageInfo info)
    {
        if (world == null || targetId < 0) return false;
        if (!world.TryGet(targetId, out var actor)) return false;
        if (actor is Character target)
        {
            var hp = target.Get<HealthComponent>();
            if (hp == null) return false;
            hp.ApplyDamage(in info);
            return true;
        }
        return false;
    }

    /// <summary>合并版：collider → 扣血一步到位（无需去重场景）。返回命中 actor 的 ID，未命中返回 -1。</summary>
    public static int TryHitAndDamage(Collider col, ActorWorld world, in DamageInfo info)
    {
        int id = ResolveActorId(col, info.AttackerId);
        if (id < 0) return -1;
        ApplyToActor(world, id, in info);
        return id;
    }

    /// <summary>沿 dir 在 [origin, origin+dir*maxDist] 内做 RaycastNonAlloc，
    /// 跳过 attackerId 自己的 collider，挑距离最近的非自身 hit。
    /// hitBuf 由调用方自己提供（一般 static reuse 避免 GC）。
    /// 返回是否命中（非自身）；命中时 best 写入对应 RaycastHit。
    /// 子弹 / 导弹 / hitscan 三处共用这个 helper，避免三份重复的"过滤自身"逻辑。</summary>
    public static bool RaycastSkipActor(
        Vector3 origin, Vector3 dir, float maxDist, LayerMask mask,
        int attackerId, RaycastHit[] hitBuf, out RaycastHit best)
    {
        // QueryTriggerInteraction.Ignore：弹道只跟实体碰撞体结算命中，穿透触发器体积
        // （减速圈 TimeScaleZone / 交互圈 / 感应区等都是 isTrigger，不该挡子弹、更不该让子弹在其表面 Despawn）。
        // 不传这个参数时用全局 Physics.queriesHitTriggers（默认 true）→ 子弹会命中圈的触发球而消失。
        int n = Physics.RaycastNonAlloc(origin, dir, hitBuf, maxDist, mask, QueryTriggerInteraction.Ignore);
        int bestIdx = -1;
        float bestDist = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var h = hitBuf[i];
            var view = h.collider.GetComponentInParent<BaseView>();
            if (view != null && view.ID == attackerId) continue; // 跳过自身
            if (h.distance < bestDist) { bestDist = h.distance; bestIdx = i; }
        }
        if (bestIdx < 0) { best = default; return false; }
        best = hitBuf[bestIdx];
        return true;
    }
}
