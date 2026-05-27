/// <summary>
/// Bullet 组件基类。结构对照 ICharacterComponent / IWeaponComponent：
/// 强类型 Owner（Bullet），显式实现 IActorComponent.Attach 把 Actor 路由到 Weapon。
/// </summary>
public class IBulletComponent : IActorComponent
{
    public Bullet Owner { get; private set; }

    public virtual void Attach(Bullet owner) { Owner = owner; }
    public virtual void Tick(float dt) { }
    public virtual void Detach() { Owner = null; }

    void IActorComponent.Attach(Actor owner) => Attach(owner as Bullet);
}
