/// <summary>泛型状态接口。<typeparamref name="TCtx"/> 为每帧透传的上下文（本项目 = Character），
/// 替代旧版"回调不带 Character、靠外部字段暂存"的写法。</summary>
public interface IYState<TCtx>
{
    string GetStateName();
    void EnterState(YStateMachine<TCtx> machine, TCtx ctx);
    void UpdateState(YStateMachine<TCtx> machine, TCtx ctx, float dt);
    void ExitState(YStateMachine<TCtx> machine, TCtx ctx);
}

/// <summary>
/// 极简状态机：**引用判重 + 幂等切换**——切到当前态直接 no-op，故选态方可每帧无条件 <see cref="Switch"/>，
/// Enter/Exit 只在真正变更时各触发一次。<see cref="Previous"/> 暴露上一态供"转移感知"逻辑使用
/// （如从技能/闪避态回 Locomotion 时按来源态取恢复淡入）。
///
/// 上下文经 <typeparamref name="TCtx"/> 透传到每个回调，状态无需缓存当前帧数据。
/// </summary>
public class YStateMachine<TCtx>
{
    public IYState<TCtx> Current  { get; private set; }
    public IYState<TCtx> Previous { get; private set; }

    public void Reset() { Current = null; Previous = null; }

    /// <summary>切到 <paramref name="next"/>。已是当前态则静默 no-op（幂等）；否则 Exit 旧态 → 记 Previous → Enter 新态。</summary>
    public void Switch(IYState<TCtx> next, TCtx ctx)
    {
        if (next == Current) return;
        Previous = Current;
        Current?.ExitState(this, ctx);
        Current = next;
        Current.EnterState(this, ctx);
    }

    /// <summary>无上下文重载：给 Enter/Exit 不读 ctx 的切换点（如初始化、layer 让位/恢复）。</summary>
    public void Switch(IYState<TCtx> next) => Switch(next, default);

    public void Update(TCtx ctx, float dt) => Current?.UpdateState(this, ctx, dt);

    public string GetCurrentStateName() => Current?.GetStateName() ?? "No State";
}
