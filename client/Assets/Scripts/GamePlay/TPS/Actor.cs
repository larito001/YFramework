using System.Collections.Generic;

/// <summary>
/// 游戏内的任意物体。ID 全局唯一，构造时自动分配。
/// 纯数据 + 组件容器：子类（Character/Weapon/Bullet…）只加字段，行为放进 IActorComponent。
/// 组件按 Add 顺序 Tick；Dispose 时按反向 Detach。
///
/// 动态增减约定：
///   Add / AddBefore&lt;TAnchor&gt; / AddAfter&lt;TAnchor&gt; 控制 Tick 顺序，Anchor 找不到时回退为 append。
///   Remove&lt;T&gt; / Remove(comp) 摘单个；**Tick 期间调用会延迟到 Tick 末执行**，避免遍历改 list。
///   Get&lt;T&gt; 返回首个匹配；同类型多实例（如 Buff 叠加）用 GetAll&lt;T&gt;(buffer) 写入外部 list。
///
/// 组件 Detach 时**必须把自己写过的 Owner 字段清回默认值**，否则 writer 离场后 reader 读到死值
/// （典型：WeaponComponent 在 IsMeleeing=true 时被移走，MoveComponent 永远锁位移）。
/// </summary>
public class Actor
{
    private static int idCounter = 1;
    public int ID { get; } = idCounter++;

    /// <summary>本 actor 的局部时间缩放因子。1=正常，0=完全冻结。
    /// 由 <see cref="TimeScaleService"/> 写入（卡肉 / 局部慢动作），<see cref="Tick"/> 内部用它缩放 dt 再派发给组件。
    /// View 端如果用 Unity Time.deltaTime 跑物理 / 动画，也应读这个字段把 dt 乘上去（参考 CharacterView）。
    /// 全局慢动作走 Unity Time.timeScale，跟这个字段相乘叠加。</summary>
    public float TimeScale = 1f;

    private readonly List<IActorComponent> _components = new List<IActorComponent>();
    /// <summary>Tick 中 Remove 的延迟队列，Tick 末批量摘 + Detach。</summary>
    private readonly List<IActorComponent> _toRemove = new List<IActorComponent>();
    private bool _isTicking;

    /// <summary>追加到末尾。Tick 顺序 = 现有组件全部跑完后才轮到本组件。</summary>
    public T Add<T>(T comp) where T : IActorComponent
    {
        _components.Add(comp);
        comp.Attach(this);
        return comp;
    }

    /// <summary>在首个 TAnchor 之前插入。找不到 TAnchor 时回退 append（不抛错，调用方负责确认时机）。
    /// 调用形如：<c>actor.AddBefore&lt;BuffComponent, MoveComponent&gt;(new BuffComponent())</c>。</summary>
    public T AddBefore<T, TAnchor>(T comp) where T : IActorComponent where TAnchor : IActorComponent
    {
        int idx = IndexOf<TAnchor>();
        if (idx < 0) _components.Add(comp);
        else _components.Insert(idx, comp);
        comp.Attach(this);
        return comp;
    }

    /// <summary>在首个 TAnchor 之后插入。找不到 TAnchor 时回退 append。</summary>
    public T AddAfter<T, TAnchor>(T comp) where T : IActorComponent where TAnchor : IActorComponent
    {
        int idx = IndexOf<TAnchor>();
        if (idx < 0) _components.Add(comp);
        else _components.Insert(idx + 1, comp);
        comp.Attach(this);
        return comp;
    }

    /// <summary>摘掉首个 T 类型组件。Tick 期间调用 → 延迟到 Tick 末摘。返回是否找到（或已入延迟队列）。</summary>
    public bool Remove<T>() where T : IActorComponent
    {
        for (int i = 0; i < _components.Count; i++)
            if (_components[i] is T t) return RemoveInternal(t);
        return false;
    }

    /// <summary>摘指定组件实例。Tick 期间调用 → 延迟到 Tick 末。</summary>
    public bool Remove(IActorComponent comp) => comp != null && RemoveInternal(comp);

    private bool RemoveInternal(IActorComponent comp)
    {
        if (_isTicking)
        {
            // 重复 Remove 同一个不重复入队，防止 Detach 被调两次
            if (!_toRemove.Contains(comp)) _toRemove.Add(comp);
            return true;
        }
        int idx = _components.IndexOf(comp);
        if (idx < 0) return false;
        _components.RemoveAt(idx);
        comp.Detach();
        return true;
    }

    public T Get<T>() where T : IActorComponent
    {
        for (int i = 0; i < _components.Count; i++)
            if (_components[i] is T t) return t;
        return default;
    }

    /// <summary>把所有 T 类型组件追加到 result（不清空 result）。低频使用，避免 enumerator alloc。
    /// 用于同类型可叠加场景（Buff、被动技能）。</summary>
    public void GetAll<T>(List<T> result) where T : IActorComponent
    {
        if (result == null) return;
        for (int i = 0; i < _components.Count; i++)
            if (_components[i] is T t) result.Add(t);
    }

    private int IndexOf<T>() where T : IActorComponent
    {
        for (int i = 0; i < _components.Count; i++)
            if (_components[i] is T) return i;
        return -1;
    }

    public virtual void Tick(float dt)
    {
        // 局部时间缩放：TimeScale=1 时走原 dt（零开销快路径），否则按 actor 自己的节奏跑。
        // 在这里统一缩放，让所有 Actor 子类（Character/Weapon/Bullet/...）自动支持卡肉 / 局部慢动作，
        // Manager 调用方仍传"游戏帧 dt"即可，不用知道 TimeScale 的存在。
        if (TimeScale != 1f) dt *= TimeScale;

        // 完全冻结时直接短路：AimComponent 这类读鼠标算 Rotation 的 dt-独立逻辑不该在卡肉期间继续跑，
        // 否则全局/局部冻住时角色仍能转身瞄准，视觉上"只有动画停了"很穿帮。
        if (dt <= 0f) return;

        _isTicking = true;
        try
        {
            for (int i = 0; i < _components.Count; i++)
                _components[i].Tick(dt);
        }
        finally
        {
            _isTicking = false;
        }

        if (_toRemove.Count > 0)
        {
            for (int i = 0; i < _toRemove.Count; i++)
            {
                var c = _toRemove[i];
                if (_components.Remove(c)) c.Detach();
            }
            _toRemove.Clear();
        }
    }

    public virtual void Dispose()
    {
        for (int i = _components.Count - 1; i >= 0; i--)
            _components[i].Detach();
        _components.Clear();
        _toRemove.Clear();
    }
}

/// <summary>
/// Actor 组件统一基类。Actor 容器只认这个类型；
/// 具体类型（ICharacterComponent / IWeaponComponent / IBulletComponent）继承本类，
/// sealed override Attach(Actor) 把回调路由到自己强类型的 Attach(TActor)。
///
/// 提供 Ctx：Attach 时统一从 GameLoop.Instance 拉一次 GameContext，子类 Attach(TActor) 中直接用
/// Ctx?.TryGet(out service)，不再每个组件重复写 GameLoop.Instance.Ctx 链。Detach 时清回 null。
/// </summary>
public abstract class IActorComponent
{
    /// <summary>全局服务上下文。Attach 后非 null（除非 GameLoop 未就绪），Detach 后 null。
    /// 子类用 Ctx?.TryGet(out svc) 拿 service。</summary>
    protected GameContext Ctx { get; private set; }

    public virtual void Attach(Actor owner)
    {
        Ctx = GameLoop.Instance != null ? GameLoop.Instance.Ctx : null;
    }
    public virtual void Tick(float dt) { }
    public virtual void Detach() { Ctx = null; }
}
