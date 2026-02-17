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
        //todo: 加入日夜管理系统里，从这里移除
        ctx.Register(new RunPhaseMachine());
        
        /// --- Gameplay Services ---
        ctx.Register(new BuildManager());//构建模块（放置系统） 耦合列车模块
        ctx.Register(new TowerManager());//塔防模块（todo：合入构建系统） 
        
        //todo:BattleManager：（所有可索敌、可受伤、可吃buff的，提供索敌系统，伤害结算系统，buff系统）
        ctx.Register(new PlayerManager());//玩家模块（管理玩家数量，目前就一个）耦合战斗系统
        ctx.Register(new TrainManager());//列车模块（管理列车段数） 耦合战斗系统、构建模块
        ctx.Register(new EnemiesManager());//敌人模块（刷怪系统） 耦合战斗系统
     
        ctx.Register(new SceneResManager());//场景资源模块（资源刷取系统，所有可拾取，可采集，可破坏的，提供背包系统、资源刷取系统、拾取系统、采集系统、破坏系统）
        ctx.Register(new BagPlugin());//背包模块（todo：合入场景资源管理）
        
        ctx.Register(new GameDayNightManager());//昼夜模块（昼夜交替系统、时间管理系统）
        
        

        return ctx;
    }
}