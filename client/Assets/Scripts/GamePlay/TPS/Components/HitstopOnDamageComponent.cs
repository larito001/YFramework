/// <summary>
/// 订阅本 Actor 的 <see cref="HealthComponent.OnDamaged"/>，在受击时按 info.HitstopTier 触发全局卡肉。
/// HealthComponent 不直接调 TimeScaleService 是为了保持分层：HP 数学纯净，反馈由独立组件挂上去。
///
/// **通用组件**：直接继承 IActorComponent，Owner=Actor。可挂在任何"有 HealthComponent 的 Actor"上。
///
/// 想让某 actor 受击不触发卡肉 → 不挂这个组件即可。
/// 想自定义触发条件（比如 boss 免疫卡肉 / 弱点放大卡肉）→ 在子类里 override OnDamaged。
/// </summary>
public class HitstopOnDamageComponent : IActorComponent
{
    private HealthComponent health;
    private TimeScaleService timeScaleService;

    public override void Attach(Actor owner)
    {
        base.Attach(owner);
        Ctx?.TryGet(out timeScaleService);
        if (owner == null) return;
        health = owner.Get<HealthComponent>();
        if (health != null) health.OnDamaged += OnDamaged;
    }

    public override void Detach()
    {
        if (health != null) health.OnDamaged -= OnDamaged;
        health = null;
        timeScaleService = null;
        base.Detach();
    }

    private void OnDamaged(DamageInfo info)
    {
        timeScaleService?.HitstopByTier(Owner != null ? Owner.ID : -1, info.HitstopTier);
    }
}
