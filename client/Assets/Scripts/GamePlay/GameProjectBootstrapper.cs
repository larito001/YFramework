using YOTO;

public static partial class GameBootstrapper
{
    static partial void RegisterProjectServices(GameContext ctx)
    {
        ctx.Register(new PlayerManager());
        ctx.Register(new EnemiesManager());
        ctx.Register(new SceneResManager());
    }

    static partial void ConfigureProjectScenes(GotSceneManager sceneManager)
    {
        sceneManager.RegisterScene<GameMainScene>();
        sceneManager.RegisterScene<GameStartScene>();
    }

    static partial void ConfigureProjectUi(UIConfig uiConfig)
    {
        uiConfig.RegisterLoading(new UIInfo(UIEnum.LoadingPanel, UILayerEnum.RayCast, "UI/LoadingPanel", 0f));
        uiConfig.Register(new UIInfo(UIEnum.StartPanel, UILayerEnum.Normal, "UI/StartPanel"));
        uiConfig.Register(new UIInfo(UIEnum.GameMainPanel, UILayerEnum.Normal, "UI/GameMainPanel"));
        uiConfig.Register(new UIInfo(UIEnum.FinishPanel, UILayerEnum.Normal, "UI/FinishPanel"));
        uiConfig.Register(new UIInfo(UIEnum.SettingPanel, UILayerEnum.Normal, "UI/Setting/SettingPanel"));
        uiConfig.Register(new UIInfo(UIEnum.ShopPanel, UILayerEnum.Normal, "UI/Shop/ShopPanel"));
        uiConfig.Register(new UIInfo(UIEnum.WarehousePanel, UILayerEnum.Normal, "UI/bag/WarehousePanel"));
        uiConfig.Register(new UIInfo(UIEnum.SkillTreePanel, UILayerEnum.Normal, "UI/SkillTree/SkillTreePanel"));
        uiConfig.Register(new UIInfo(UIEnum.BagPanel, UILayerEnum.Normal, "UI/bag/BagPanel"));
        uiConfig.Register(new UIInfo(UIEnum.SelectTowerPanel, UILayerEnum.PopText, "UI/SelectTower/SelectTowerPanel"));
        uiConfig.Register(new UIInfo(UIEnum.TowerUpPanel, UILayerEnum.PopText, "UI/TowerUpPanel"));
        uiConfig.Register(new UIInfo(UIEnum.WinPanel, UILayerEnum.Normal, "UI/WinPanel"));
        uiConfig.Register(new UIInfo(UIEnum.GuidePanel, UILayerEnum.Normal, "UI/GuidePanel"));
        uiConfig.Register(new UIInfo(UIEnum.SearchPanel, UILayerEnum.Normal, "UI/bag/SearchPanel"));
    }

    static partial void RunProjectStartup(GameContext ctx)
    {
        ctx.Get<UIMgr>().Show(UIEnum.StartPanel);
    }
}
