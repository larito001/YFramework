using YOTO;

public static partial class GameBootstrapper
{
    static partial void RegisterProjectServices(GameContext ctx)
    {
        // ctx.Register(new EnemiesManager());
        // ctx.Register(new SceneResManager());
    }

    static partial void ConfigureProjectScenes(YSceneManager sceneManager)
    {
        sceneManager.RegisterScene<GameMainScene>();
        sceneManager.RegisterScene<GameStartScene>();
    }

    static partial void ConfigureProjectUi(UIConfig uiConfig)
    {
        uiConfig.RegisterLoading<LoadingPanel>(UIEnum.LoadingPanel, UILayerEnum.RayCast, "UI/LoadingPanel", 0f);
        uiConfig.Register<StartPanel>(UIEnum.StartPanel, UILayerEnum.Normal, "UI/StartPanel");
        uiConfig.Register<GameMainPanel>(UIEnum.GameMainPanel, UILayerEnum.Normal, "UI/GameMainPanel");
        uiConfig.Register<FinishPanel>(UIEnum.FinishPanel, UILayerEnum.Normal, "UI/FinishPanel");
        uiConfig.Register<SettingPanel>(UIEnum.SettingPanel, UILayerEnum.Normal, "UI/Setting/SettingPanel");
    }

    static partial void RunProjectStartup(GameContext ctx)
    {
        ctx.Get<UIMgr>().Show<StartPanel>();
    }
}
