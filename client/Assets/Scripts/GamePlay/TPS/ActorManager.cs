using System.Collections.Generic;

/// <summary>
/// Actor 管理器基类：维护 List + ActorWorld 注册/注销 + view 清理 + （可选的）每帧 Tick + 延迟移除。
/// 抽出 TowerManager / ChestManager / DropItemSystem 等共有的「actor 集合生命周期」骨架，子类只填差异
/// （工厂、spawn 配方、移除时的额外退订）。CharacterManager 因有玩家重生等特殊流程暂不并入。
///
/// **Tick 是 opt-in**：本基类不实现 <see cref="ITickable"/>。需要每帧驱动 actor 组件的子类（如 <see cref="TowerManager"/>）
/// 自己声明 <c>: ActorManager&lt;T&gt;, ITickable</c> 并在 <c>Tick</c> 里调 <see cref="TickActors"/>；
/// 无逻辑组件、不需要每帧跑的子类（宝箱 / 掉落物）不实现 ITickable，因此不进 GameContext 的 tick 调度——零空转。
///
/// 延迟移除：组件 Tick 期间调 <see cref="Remove"/> 不会改正在遍历的列表，攒到本帧 <see cref="TickActors"/> 末统一清；
/// 非 Tick 期（或不 tick 的子类）调 Remove 直接立即清。
/// </summary>
public abstract class ActorManager<T> : IGameService where T : Actor
{
    protected GameContext Ctx { get; private set; }
    protected ViewManager ViewMgr { get; private set; }
    protected ActorWorld World { get; private set; }

    protected readonly List<T> actors = new List<T>();
    private readonly List<T> toRemove = new List<T>();
    private bool ticking;

    public void Init(GameContext context)
    {
        Ctx = context;
        ViewMgr = context.Get<ViewManager>();
        World = context.Get<ActorWorld>();
        OnInit();
    }

    /// <summary>子类在基础服务(Ctx/ViewMgr/World)就绪后做自己的初始化（建工厂、取额外服务）。</summary>
    protected virtual void OnInit() { }

    public void Shutdown()
    {
        for (int i = actors.Count - 1; i >= 0; i--) RemoveImmediate(actors[i]);
        actors.Clear();
        toRemove.Clear();
        OnShutdown();
        Ctx = null; ViewMgr = null; World = null;
    }

    /// <summary>子类释放自己持有的额外资源（材质缓存等）。actor 列表已清空后调用。</summary>
    protected virtual void OnShutdown() { }

    /// <summary>顺序 Tick 全部 actor，期间的 <see cref="Remove"/> 请求安全延迟到末尾统一清。
    /// 需要每帧驱动 actor 组件的子类实现 <see cref="ITickable"/> 并在其 Tick 里调用本方法。</summary>
    protected void TickActors(float dt)
    {
        ticking = true;
        try
        {
            for (int i = 0; i < actors.Count; i++) actors[i].Tick(dt);
        }
        finally { ticking = false; }

        if (toRemove.Count > 0)
        {
            for (int i = 0; i < toRemove.Count; i++) RemoveImmediate(toRemove[i]);
            toRemove.Clear();
        }
    }

    /// <summary>登记一个已创建好的 actor：入列表 + 注册 ActorWorld。子类 spawn 末尾调。</summary>
    protected void Track(T actor)
    {
        if (actor == null) return;
        actors.Add(actor);
        World.Register(actor);
    }

    /// <summary>移除 actor：在 <see cref="TickActors"/> 期间调用安全（攒到本帧末），其余时机立即清。重复请求去重。</summary>
    public void Remove(T actor)
    {
        if (actor == null) return;
        if (ticking)
        {
            if (!toRemove.Contains(actor)) toRemove.Add(actor);
            return;
        }
        RemoveImmediate(actor);
    }

    private void RemoveImmediate(T actor)
    {
        if (actor == null) return;
        OnRemoving(actor);
        actors.Remove(actor);
        World.Unregister(actor.ID);
        ViewMgr.RemoveBaseView(actor.ID);
        actor.Dispose();
    }

    /// <summary>移除某 actor 前的额外清理（退订其组件事件等），在列表移除 / Dispose 之前调用。</summary>
    protected virtual void OnRemoving(T actor) { }
}
