/// <summary>
/// Tower 组件基类。结构对照 ICharacterComponent / IBulletComponent / IWeaponComponent：
/// 强类型 Owner（Tower），sealed override Attach(Actor) 把回调路由到强类型 Attach(Tower)。
///
/// 当前 Tower 子类无专属字段（HP / Position / Rotation / TeamId / OwnerActorId 都在 Actor 基类），
/// 用 ITowerComponent 主要是表达"语义专属"——这些组件只该挂塔，不该挂角色 / 子弹。
/// Tower 未来加专属字段（如 TurretAngle / UpgradeLevel）时本子家族就有实际类型收窄价值。
/// </summary>
public class ITowerComponent : IActorComponent
{
    /// <summary>强类型 Owner。隐藏基类 <see cref="IActorComponent.Owner"/>（Actor）—— 两者实际指同一对象。</summary>
    public new Tower Owner { get; private set; }

    public sealed override void Attach(Actor owner)
    {
        base.Attach(owner);
        Owner = owner as Tower;
        Attach(Owner);
    }

    public virtual void Attach(Tower owner) { }

    public override void Detach()
    {
        Owner = null;
        base.Detach();
    }
}
