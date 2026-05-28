/// <summary>
/// Weapon 组件基类。开火/弹夹/扩散等行为按子类组合，每种武器装不同的组件即可。
/// 生命周期：Attach(Weapon) → Tick（由 WeaponManager 每帧调） → Detach
/// 组件只读写 Owner 上的字段，不碰 view、不持有 GameObject 引用。
///
/// Attach 链路同 ICharacterComponent：base.Attach(Actor) 先设 Ctx，再调强类型 Attach(Weapon)。
/// 子类用 Ctx?.TryGet(out svc) 拿 service。
/// </summary>
public class IWeaponComponent : IActorComponent
{
    /// <summary>强类型 Owner。隐藏基类 <see cref="IActorComponent.Owner"/>（Actor）—— 两者实际指同一对象。</summary>
    public new Weapon Owner { get; private set; }

    public sealed override void Attach(Actor owner)
    {
        base.Attach(owner);
        Owner = owner as Weapon;
        Attach(Owner);
    }

    public virtual void Attach(Weapon owner) { }

    public override void Detach()
    {
        Owner = null;
        base.Detach();
    }
}
