/// <summary>
/// Character 组件基类。逻辑组件全部继承这个类。
/// 生命周期：Attach(Character) → 每帧 Tick → Detach
/// 组件只读写 Owner 上的数据字段，不持有 view、不访问 Unity 渲染对象——
/// view 自己每帧从 Character 读数据驱动表现。
///
/// Attach 链路（Actor.Add 时走这条）：
///   Actor.Add → comp.Attach(Actor) [sealed]
///     → base.Attach(Actor) 设 Ctx
///     → Owner = (Character)owner
///     → 调强类型 Attach(Character) [子类 override 入口]
/// Detach 反序：先用 Owner 清字段，再 base.Detach 把 Owner/Ctx 置 null。
///
/// 子类规约：Detach 时把"自己写过的 Owner 字段"清回默认值（见 Actor.cs 注释）。
/// </summary>
public class ICharacterComponent : IActorComponent
{
    public Character Owner { get; private set; }

    public sealed override void Attach(Actor owner)
    {
        base.Attach(owner); // 设 Ctx
        Owner = owner as Character;
        Attach(Owner);
    }

    /// <summary>子类 override 此版本。进入时 Ctx 和 Owner 已就绪。</summary>
    public virtual void Attach(Character owner) { }

    public override void Detach()
    {
        Owner = null;
        base.Detach(); // 清 Ctx
    }
}
