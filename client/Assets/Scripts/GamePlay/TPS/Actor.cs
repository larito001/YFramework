using System.Collections.Generic;

/// <summary>
/// 游戏内的任意物体。ID 全局唯一，构造时自动分配。
/// 纯数据 + 组件容器：子类（Character/Weapon/Bullet…）只加字段，行为放进 IActorComponent。
/// 组件按 Add 顺序 Tick；Dispose 时按反向 Detach。
/// </summary>
public class Actor
{
    private static int idCounter = 1;
    public int ID { get; } = idCounter++;

    private readonly List<IActorComponent> _components = new List<IActorComponent>();

    public T Add<T>(T comp) where T : IActorComponent
    {
        _components.Add(comp);
        comp.Attach(this);
        return comp;
    }

    public T Get<T>() where T : class, IActorComponent
    {
        for (int i = 0; i < _components.Count; i++)
            if (_components[i] is T t) return t;
        return null;
    }

    public virtual void Tick(float dt)
    {
        for (int i = 0; i < _components.Count; i++)
            _components[i].Tick(dt);
    }

    public virtual void Dispose()
    {
        for (int i = _components.Count - 1; i >= 0; i--)
            _components[i].Detach();
        _components.Clear();
    }
}

/// <summary>
/// Actor 组件统一契约。Actor 容器只认这个接口；
/// 具体类型（ICharacterComponent / IWeaponComponent 等）用显式实现把 Actor 回调路由到自己强类型的 Attach。
/// </summary>
public interface IActorComponent
{
    void Attach(Actor owner);
    void Tick(float dt);
    void Detach();
}
