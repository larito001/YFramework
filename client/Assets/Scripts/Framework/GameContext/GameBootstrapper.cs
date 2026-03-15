using HotUpdate.Scripts.Framework.Pool.newPool;
using NoSLoofah.BuffSystem.Manager;
using Unity.VisualScripting;
using YOTO;

public static partial class GameBootstrapper
{
    public static GameContext BuildContext()
    {
        var ctx = new GameContext();

        ctx.Register(new TestService());

        // Framework services only. Project-specific services are injected via partial methods.
        ctx.Register(new ObjectPool());
        ctx.Register(new ScreenMonitor());
        ctx.Register(new EventMgr());
        ctx.Register(new StoreMgr());
        ctx.Register(new ResMgr());
        ctx.Register(new SceneReferenceService());
        ctx.Register(new CameraMgr());
        ctx.Register(new UIMgr(BuildUiConfig()));
        ctx.Register(new SoundMgr());
        ctx.Register(new TaskManager());

        var sceneManager = new GotSceneManager();
        ConfigureProjectScenes(sceneManager);
        ctx.Register(sceneManager);

        ctx.Register(new FlyTextMgr());
        ctx.Register(new BuffManager());

        var runner = GameLoop.Instance.GetComponent<CoroutineRunner>();
        if (runner == null)
        {
            runner = GameLoop.Instance.AddComponent<CoroutineRunner>();
        }

        ctx.Register<ICoroutineRunner>(runner);
        RegisterProjectServices(ctx);

        return ctx;
    }

    public static void RunStartup(GameContext ctx)
    {
        RunProjectStartup(ctx);
    }

    private static UIConfig BuildUiConfig()
    {
        var uiConfig = new UIConfig();
        uiConfig.Register(new UIInfo(UIEnum.LoadingPanel, UILayerEnum.RayCast, "UI/LoadingPanel"));
        ConfigureProjectUi(uiConfig);
        return uiConfig;
    }

    static partial void RegisterProjectServices(GameContext ctx);
    static partial void ConfigureProjectScenes(GotSceneManager sceneManager);
    static partial void ConfigureProjectUi(UIConfig uiConfig);
    static partial void RunProjectStartup(GameContext ctx);
}
