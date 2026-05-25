using UnityEngine;
using YFramework.Config;
using YOTO;

/// <summary>
/// 组装入口。各 Mgr 已是 MonoBehaviour，通过在 GameLoop 同一个 GameObject 上
/// AddComponent 顺序保证 Awake 顺序（被依赖者在前）。
/// </summary>
public static partial class GameBootstrapper
{
    public static void BuildContext(GameLoop loop, GameContext ctx)
    {
        var go = loop.gameObject;

        ctx.Register(go.AddComponent<ConfigManager>());
        ctx.Register(go.AddComponent<ScreenMonitor>());
        ctx.Register(go.AddComponent<EventMgr>());
        ctx.Register(go.AddComponent<StoreMgr>());
        ctx.Register(go.AddComponent<ResMgr>());
        ctx.Register(go.AddComponent<SceneReferenceService>());
        ctx.Register(go.AddComponent<CameraMgr>());
        ctx.Register(go.AddComponent<SceneInteractionService>());

        var uiMgr = go.AddComponent<UIMgr>();
        ctx.Register(uiMgr);
        uiMgr.Initialize(BuildUiConfig());

        ctx.Register(go.AddComponent<SoundMgr>());
        ctx.Register(go.AddComponent<TaskManager>());
        ctx.Register(go.AddComponent<FlyTextMgr>());

        var runner = loop.GetComponent<CoroutineRunner>();
        if (runner == null) runner = go.AddComponent<CoroutineRunner>();
        ctx.Register<ICoroutineRunner>(runner);

        ctx.Register(go.AddComponent<YAStarManager>());

        RegisterProjectServices(ctx);
    }

    public static void RunStartup(GameContext ctx)
    {
        RunProjectStartup(ctx);
    }

    public static UIConfig BuildUiConfig()
    {
        var uiConfig = new UIConfig();
        ConfigureProjectUi(uiConfig);
        return uiConfig;
    }

    static partial void RegisterProjectServices(GameContext ctx);
    static partial void ConfigureProjectUi(UIConfig uiConfig);
    static partial void RunProjectStartup(GameContext ctx);
}
