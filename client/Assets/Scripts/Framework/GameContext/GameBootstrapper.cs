using YOTO;

public static class GameBootstrapper
{
    public static GameContext BuildContext()
    {
        var ctx = new GameContext();
        // --- Core Services ---
        ctx.Register(new TestService());
        ctx.Register(new ScreenMonitor());
        ctx.Register(new EventMgr());
        ctx.Register(new StoreMgr());
        ctx.Register(new ResMgr());
        ctx.Register(new CameraMgr());
        ctx.Register(new UIMgr());
        ctx.Register(new EntityMgr());
        ctx.Register(new SoundMgr());
        ctx.Register(new TaskManager());
        ctx.Register(new GotSceneManager());
        ctx.Register(new FlyTextMgr());
        ctx.Register(new RunPhaseMachine());
        
        /// --- Gameplay Services ---
        ctx.Register(new BuildManager());//构建模块
        ctx.Register(new PlayerManager());//玩家模块
        ctx.Register(new TrainManager());//列车模块
        ctx.Register(new EnemiesManager());//敌人模块
        ctx.Register(new TowerManager());//塔防模块（待丢弃，合入构建模块）
        ctx.Register(new SceneResManager());//场景资源模块
        ctx.Register(new BagPlugin());//背包模块
        ctx.Register(new GameDayNightManager());//昼夜模块
        
        // --- Core Services ---
        // ctx.Register<IEventBus>(new EventBus());
        // ctx.Register<ITimeService>(new TimeService());
        // ctx.Register<IConfigService>(new ConfigService());   // 统一加载 SO/JSON/Addressables
        // ctx.Register<ISaveService>(new SaveService());
        // ctx.Register<IPoolService>(new PoolService());
        // ctx.Register<IAudioService>(new AudioService());
        //
        // // --- World / Scene ---
        // ctx.Register<ISceneService>(new SceneService());
        //
        // // --- Gameplay Orchestration ---
        // ctx.Register(new PhaseStateMachine());               // Build/Combat/Night/Travel
        // ctx.Register(new PlayerModeStateMachine());          // OnFoot/BuildMode/InTrain/Downed
        //
        // // --- Combat (塔和枪共用) ---
        // ctx.Register(new DamageSystem());
        // ctx.Register(new StatusEffectSystem());
        // ctx.Register(new TargetingSystem());
        // ctx.Register(new ProjectileSystem());
        //
        // // --- Build / Economy ---
        // ctx.Register(new InventorySystem());
        // ctx.Register(new BuildPlacementSystem());
        // ctx.Register(new BuildUseCases());                   // TryBuild/Upgrade/Recycle
        //
        // // --- AI / Waves ---
        // ctx.Register(new WaveDirector());
        // ctx.Register(new EnemySpawnSystem());
        //
        // // --- Presentation (可选，推荐做薄) ---
        // ctx.Register<IHUDPresenter>(new HUDPresenter());

        return ctx;
    }
}