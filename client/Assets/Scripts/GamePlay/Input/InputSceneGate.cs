using YOTO;

/// <summary>
/// 按当前场景启停输入总开关 <see cref="InputService.IsEnabled"/>:只有处于游戏场景
/// (<see cref="YSceneType.Home"/> / <see cref="YSceneType.GamePlay"/>)时才开启,菜单/启动/读档等
/// 非游戏场景(<see cref="YSceneType.None"/>)一律关闭——这样在开始界面按 B 不会唤起背包。
///
/// 注册早于 <see cref="InputService"/>,同帧先把 IsEnabled 设好,再让 InputService.Tick 读它。
/// 与 <see cref="CombatInputGate"/> 同样是每帧轮询当前场景类型(开销可忽略),新增场景零改动。
/// </summary>
public class InputSceneGate : IGameService, ITickable
{
    private InputService input;
    private YSceneManager sceneMgr;

    public void Init(GameContext ctx)
    {
        input = ctx.Get<InputService>();
        sceneMgr = ctx.Get<YSceneManager>();
    }

    public void Shutdown()
    {
        input = null;
        sceneMgr = null;
    }

    public void Tick(float dt)
    {
        if (input == null || sceneMgr == null) return;
        var t = sceneMgr.SceneType;
        input.IsEnabled = t == YSceneType.Home || t == YSceneType.GamePlay;
    }
}
