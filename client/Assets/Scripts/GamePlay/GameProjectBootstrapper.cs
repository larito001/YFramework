using YOTO;

public static partial class GameBootstrapper
{
    static partial void RegisterProjectServices(GameContext ctx)
    {
        ctx.Register(new EnemiesManager());
        ctx.Register(new SceneResManager());
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
        uiConfig.Register<ShopPanel>(UIEnum.ShopPanel, UILayerEnum.Normal, "UI/Shop/ShopPanel");
        uiConfig.Register<WarehousePanel>(UIEnum.WarehousePanel, UILayerEnum.Normal, "UI/bag/WarehousePanel");
        uiConfig.Register<SkillTreePanel>(UIEnum.SkillTreePanel, UILayerEnum.Normal, "UI/SkillTree/SkillTreePanel");
        uiConfig.Register<BagPanel>(UIEnum.BagPanel, UILayerEnum.Normal, "UI/bag/BagPanel");
        uiConfig.Register<SelectTowerPanel>(UIEnum.SelectTowerPanel, UILayerEnum.PopText, "UI/SelectTower/SelectTowerPanel");
        uiConfig.Register<TowerUpPanel>(UIEnum.TowerUpPanel, UILayerEnum.PopText, "UI/TowerUpPanel");
        uiConfig.Register<WinPanel>(UIEnum.WinPanel, UILayerEnum.Normal, "UI/WinPanel");
        uiConfig.Register<GuidePanel>(UIEnum.GuidePanel, UILayerEnum.Normal, "UI/GuidePanel");
        uiConfig.Register<SearchPanel>(UIEnum.SearchPanel, UILayerEnum.Normal, "UI/bag/SearchPanel");
    }

    static partial void RunProjectStartup(GameContext ctx)
    {
        ctx.Get<UIMgr>().Show<StartPanel>();
    }
}
