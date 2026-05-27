/// <summary>
/// Bullet 组件基类。结构对照 ICharacterComponent / IWeaponComponent：
/// 强类型 Owner（Bullet），sealed override Attach(Actor) 把回调路由到强类型 Attach(Bullet)。
/// 子类 Attach(Bullet) 中 Ctx 和 Owner 已就绪。
/// </summary>
public class IBulletComponent : IActorComponent
{
    public Bullet Owner { get; private set; }

    public sealed override void Attach(Actor owner)
    {
        base.Attach(owner);
        Owner = owner as Bullet;
        Attach(Owner);
    }

    public virtual void Attach(Bullet owner) { }

    public override void Detach()
    {
        Owner = null;
        base.Detach();
    }
}
