
/// <summary>
/// 任何需要 Update 的系统，显式实现这些接口，GameLoop 统一调度。
/// </summary>
public interface IGameService
{
    void Init(GameContext ctx);
    void Shutdown();
}

public interface ITickable { void Tick(float dt); }
public interface IFixedTickable { void FixedTick(float fdt); }
public interface ILateTickable { void LateTick(float dt); }