using System.Collections.Generic;
using UnityEngine;

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
/// （典型：SkillCastComponent 在 IsCastingSkill=true 时被移走，MoveComponent 永远锁位移）。
///
/// 基类字段语义（详见 ARCHITECTURE.md 的"字段归属"小节）：
///   通用组件（HealthComponent / HitstopOnDamageComponent / GravityComponent / AutoDespawnComponent）
///   只读写这些基类字段；子类专属字段（Character.AnimMoveX / Weapon.FireOrigin 等）由特化组件处理。
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

    // ── 空间（所有 Actor 都需要） ──
    /// <summary>世界坐标。Character 由 View 物理后回写、Bullet 由 BulletMoveComponent 推进。</summary>
    public Vector3 Position;
    /// <summary>世界朝向。Character 由 AimComponent 写、Bullet 由 View 通过 Velocity 推算（不写本字段）。</summary>
    public Quaternion Rotation = Quaternion.identity;
    /// <summary>意图速度（m/s）。Character 由 MoveComponent / SkillCastComponent / GravityComponent 写，
    /// CharacterView.LateUpdate 用 CharacterController.Move 应用。Bullet 不用此字段（Bullet 用 <see cref="Velocity"/>）。</summary>
    public Vector3 WishVelocity;
    /// <summary>实际速度（m/s）。Bullet 由 FireEffect 写初值、MissileMoveComponent 每帧写切线方向。
    /// Character 不用此字段（CC 内部维护实际速度，回写到 Position 即可）。</summary>
    public Vector3 Velocity;
    /// <summary>是否贴地。CharacterView 调 CC.isGrounded 后回写。Bullet/Weapon 不用。</summary>
    public bool IsGrounded;

    // ── 生命周期（多数 Actor 用，少数不写） ──
    /// <summary>剩余寿命（秒）。Bullet / 投射物 / 临时召唤物用，&lt;=0 视为不限制或已结束。
    /// Character / Weapon 一般不写本字段，清理走 AutoDespawnComponent。</summary>
    public float LifetimeRemaining;
    /// <summary>发射者 / 召唤者 Actor.ID，用于自伤过滤和归属。-1 表示无主。</summary>
    public int OwnerActorId = -1;

    // ── 阵营 ──
    /// <summary>阵营 ID。0=中立 / 1=玩家军 / 2=敌军，3+ 留作扩展。详见 ARCHITECTURE.md "阵营" 小节。
    /// 塔 AI 锁敌 / DamageRouter 走 HealthComponent 友军伤害过滤 / Weapon.TeamId 跟随持有者—— 都靠本字段。</summary>
    public int TeamId;

    // ── HP（HealthComponent 写，UI / 死亡逻辑读） ──
    /// <summary>最大生命值。HealthComponent.Attach 时写入；可被增益 / 装备改动。</summary>
    public float MaxHealth;
    /// <summary>当前生命值。HealthComponent.ApplyDamage / Heal 修改。</summary>
    public float CurHealth;
    /// <summary>死亡标志位。HealthComponent 在 CurHealth&lt;=0 时置 true；其他组件按需 Tick 头部早退。</summary>
    public bool IsDead;
    /// <summary>死亡一次性 trigger：HealthComponent 死亡时置 true（和 IsDead 同帧），view 消费 SetTrigger("Die") 后清回。</summary>
    public bool Die;
    /// <summary>死亡动画变体：HealthComponent 在置 Die 时随机选（0=DeathL，1=DeathR），view 写到 Animator Int。</summary>
    public int DeathVariant;

    private readonly List<IActorComponent> _components = new List<IActorComponent>();
    /// <summary>Tick 中 Remove 的延迟队列，Tick 末批量摘 + Detach。</summary>
    private readonly List<IActorComponent> _toRemove = new List<IActorComponent>();
    private bool _isTicking;

    /// <summary>追加到末尾。Tick 顺序 = 现有组件全部跑完后才轮到本组件。
    /// **不允许在 Tick 期间调用**——会破坏当前帧遍历语义（append 让新组件本帧立刻 Tick，
    /// AddBefore 插到当前 index 前面会让当前组件本帧重复 Tick）。Tick 中要插组件，存意图到
    /// Owner 字段、下帧由外部代码 Add，或通过 service 调度。</summary>
    public T Add<T>(T comp) where T : IActorComponent
    {
        if (_isTicking) { LogTickAddError(nameof(Add)); return comp; }
        _components.Add(comp);
        comp.Attach(this);
        return comp;
    }

    /// <summary>在首个 TAnchor 之前插入。找不到 TAnchor 时回退 append（不抛错，调用方负责确认时机）。
    /// 调用形如：<c>actor.AddBefore&lt;BuffComponent, MoveComponent&gt;(new BuffComponent())</c>。
    /// 同 <see cref="Add"/>，**不允许 Tick 中调用**。</summary>
    public T AddBefore<T, TAnchor>(T comp) where T : IActorComponent where TAnchor : IActorComponent
    {
        if (_isTicking) { LogTickAddError(nameof(AddBefore)); return comp; }
        int idx = IndexOf<TAnchor>();
        if (idx < 0) _components.Add(comp);
        else _components.Insert(idx, comp);
        comp.Attach(this);
        return comp;
    }

    /// <summary>在首个 TAnchor 之后插入。找不到 TAnchor 时回退 append。**不允许 Tick 中调用**。</summary>
    public T AddAfter<T, TAnchor>(T comp) where T : IActorComponent where TAnchor : IActorComponent
    {
        if (_isTicking) { LogTickAddError(nameof(AddAfter)); return comp; }
        int idx = IndexOf<TAnchor>();
        if (idx < 0) _components.Add(comp);
        else _components.Insert(idx + 1, comp);
        comp.Attach(this);
        return comp;
    }

    private static void LogTickAddError(string api)
    {
        UnityEngine.Debug.LogError($"[Actor] {api} called during Tick — Add 操作不能在 Tick 中执行（会破坏遍历语义）。" +
            $" Remove 有延迟队列，Add 没有。请把 {api} 移到 Tick 外，或通过 service 调度到下帧。本次调用已忽略。");
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

    /// <summary>把所有 T 类型组件**追加**到 result（不清空 result，调用方自己 Clear）。
    /// 低频使用，避免 enumerator alloc。用于同类型可叠加场景（Buff、被动技能）。
    /// 命名 Append 而非 Get 是因为不替换 buffer 内容——和 ActorWorld.AppendAll 保持一致。</summary>
    public void AppendAll<T>(List<T> result) where T : IActorComponent
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
        // 注意：dt 缩到 0 时仍然派发给组件——组件自己决定是否在 dt=0 时做 dt-独立的事
        // （比如 AimComponent 在 dt<=0 时早退，避免冻结期间还转身瞄准）。基类不替子类做决定。
        if (TimeScale != 1f) dt *= TimeScale;

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
/// Actor 组件统一基类。Actor 容器只认这个类型。两种用法：
///   - **通用组件**：直接继承本类，Owner 类型保持 Actor，只读写 Actor 基类字段
///     （如 HealthComponent / HitstopOnDamageComponent / GravityComponent / AutoDespawnComponent）。
///   - **特化组件**：继承 ICharacterComponent / IWeaponComponent / IBulletComponent，
///     sealed override Attach(Actor) 把回调路由到强类型的 Attach(TActor)，访问子类专属字段。
///
/// 提供 Ctx：Attach 时统一从 GameLoop.Instance 拉一次 GameContext，子类直接用
/// Ctx?.TryGet(out service)，不再每个组件重复写 GameLoop.Instance.Ctx 链。Detach 时清回 null。
///
/// 提供 Owner（Actor 类型）：通用组件直接用本字段访问 Actor 基类字段；特化组件子类用 new Owner 隐藏出强类型版本。
/// </summary>
public abstract class IActorComponent
{
    /// <summary>挂载到的 Actor。Attach 后非 null，Detach 后 null。通用组件直接用；
    /// 特化组件（IXxxComponent）通过 new 隐藏暴露强类型版本，本字段仍是 Actor。</summary>
    public Actor Owner { get; private set; }

    /// <summary>全局服务上下文。Attach 后非 null（除非 GameLoop 未就绪），Detach 后 null。
    /// 子类用 Ctx?.TryGet(out svc) 拿 service。</summary>
    protected GameContext Ctx { get; private set; }

    public virtual void Attach(Actor owner)
    {
        Owner = owner;
        Ctx = GameLoop.Instance != null ? GameLoop.Instance.Ctx : null;
    }
    public virtual void Tick(float dt) { }
    public virtual void Detach()
    {
        Owner = null;
        Ctx = null;
    }
}
