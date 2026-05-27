/// <summary>
/// Weapon 组件基类。开火/弹夹/扩散等行为按子类组合，每种武器装不同的组件即可。
/// 生命周期：Attach → Tick（由 WeaponManager 每帧调） → Detach
/// 组件只读写 Owner 上的字段，不碰 view、不持有 GameObject 引用。
///
/// 容器侧（Actor）走 IActorComponent.Attach(Actor)，这里转成强类型 Attach(Weapon)。
/// </summary>
public class IWeaponComponent : IActorComponent
{
    public Weapon Owner { get; private set; }

    public virtual void Attach(Weapon owner) { Owner = owner; }
    public virtual void Tick(float dt) { }
    public virtual void Detach() { Owner = null; }

    void IActorComponent.Attach(Actor owner) => Attach(owner as Weapon);
}
