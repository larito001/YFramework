/// <summary>
/// Character 组件基类。逻辑组件全部继承这个类。
/// 生命周期：Attach → 每帧 Tick → Detach
/// 组件只读写 Owner 上的数据字段，不持有 view、不访问 Unity 渲染对象——
/// view 自己每帧从 Character 读数据驱动表现。
///
/// 容器侧（Actor）走 IActorComponent.Attach(Actor)，这里转成强类型 Attach(Character)，
/// 子类继续重写 Attach(Character owner)。
/// </summary>
public class ICharacterComponent : IActorComponent
{
    public Character Owner { get; private set; }

    public virtual void Attach(Character owner) { Owner = owner; }
    public virtual void Tick(float dt) { }
    public virtual void Detach() { Owner = null; }

    void IActorComponent.Attach(Actor owner) => Attach(owner as Character);
}
