using YFramework.Config;
using Unity.VisualScripting;
using UnityEngine;
using YOTO;
using YOTO.Network;

/// <summary>
/// Composition root for framework and gameplay services.
/// Framework registrations stay here; gameplay-specific registrations are delegated
/// through partial methods implemented in the gameplay layer.
/// </summary>
public static partial class GameBootstrapper
{
    public static GameContext BuildContext()
    {
        var ctx = new GameContext();
        ctx.Register(new ConfigManager());
        // Framework services only. Project-specific services are injected via partial methods.
        ctx.Register(new AsyncPrefabPool());
        ctx.Register(new ScreenMonitor());
        ctx.Register(new EventMgr());
        ctx.Register(new StoreMgr());
        ctx.Register(new ResMgr());
        ctx.Register(new SceneReferenceService());
        ctx.Register(new CameraManager());
        ctx.Register(new UIMgr(BuildUiConfig()));
        ctx.Register(new SoundMgr());
        ctx.Register(new TaskManager());
        
        var sceneManager = new YSceneManager();
        ConfigureProjectScenes(sceneManager);
        ctx.Register(sceneManager);

        ctx.Register(new FlyTextMgr());

        // ── Network layer (按文档分层注册：Platform → Messaging → Transport → Session → Lobby → Runtime) ──
        // 每个实例只注册一次：GameContext.Register 会把实例加入 init/tick 列表，重复注册会触发双 Init/双 Tick。
        var steamPlatform = new SteamPlatform();
        ctx.Register(steamPlatform);

        var mainThreadInbox = new MainThreadDispatcher();
        ctx.Register(mainThreadInbox);

        var messageRegistry = new MessageRegistry();
        ConfigureProjectNetwork(messageRegistry);
        ctx.Register(messageRegistry);

        IMessageSerializer serializer = new ProtobufMessageSerializer();
        ctx.Register(serializer);

        var messageDispatcher = new MessageDispatcher(messageRegistry, serializer);
        ctx.Register(messageDispatcher);

        // Transport / Session / Lobby 注册为接口类型，gameplay 通过接口取用，看不到 Steam 细节。
        ctx.Register<INetworkTransport>(new SteamNetworkTransport(steamPlatform, mainThreadInbox));
        var transport = ctx.Get<INetworkTransport>();

        ctx.Register<INetworkSession>(new NetworkSession(transport, messageRegistry, messageDispatcher, serializer, mainThreadInbox));
        ctx.Register<ILobbyService>(new SteamLobbyService(steamPlatform));

        ctx.Register(new NetworkRuntime(transport, mainThreadInbox));

        var runner = GameLoop.Instance.GetComponent<CoroutineRunner>();
        if (runner == null)
        {
            runner = GameLoop.Instance.AddComponent<CoroutineRunner>();
        }

        ctx.Register<ICoroutineRunner>(runner);
        RegisterProjectServices(ctx);
        Debug.Log(ctx.Get<ConfigManager>().heroConfig.Get(1001).HeroName);
        ctx.Register(new ActorWorld());
        ctx.Register(new ViewManager());
        // 通用特效服务：按路径池化播放 VFX prefab（技能动效等调它）。在 ViewManager / ResMgr 之后注册（Init 里 Get 它们）。
        ctx.Register(new VfxManager());
        // TimeScaleService 必须在 CharacterManager 之前注册：它用 GameLoop 未缩放 dt 推进卡肉 timer，
        // 同帧写完 character.TimeScale 后，CharacterManager.Tick 读到的就是当帧的缩放值。
        ctx.Register(new TimeScaleService());
        // ── TPS Manager Tick 顺序（不要随意调整）──
        //   CharacterManager → WeaponManager → BulletManager
        //
        // 同帧数据链（依赖 Register 顺序 = Tick 顺序）：
        //   1. CharacterManager.Tick
        //      └─ Aim → Move → Weapon（Add 序，CharacterFactory 固定）
        //         WeaponComponent 写 currentWeapon.FireIntent / FireOrigin / FireDirection
        //   2. WeaponManager.Tick
        //      └─ FireComponent 读上一行刚写的字段，按冷却开火，调 Effect.Fire 走具体弹道
        //         （LinearProjectileEffect 调 BulletManager.Spawn → 子弹本帧加入 BulletManager 列表）
        //   3. BulletManager.Tick
        //      └─ BulletMoveComponent 推进新生 + 已有子弹，做线段命中
        //
        // 反序会让开火延迟一帧、子弹起步少一帧推进。新增同类 Manager 按依赖方向插入。
        ctx.Register(new CharacterManager());
        // TowerManager 必须在 WeaponManager 之前注册：TowerWeaponComponent.Tick 写 currentWeapon.FireIntent /
        // FireOrigin / FireDirection / FireTarget，FireComponent 在 WeaponManager.Tick 里消费这些字段。
        ctx.Register(new TowerManager());
        ctx.Register(new WeaponManager());
        ctx.Register(new BulletManager());

        return ctx;
    }

    public static void RunStartup(GameContext ctx)
    {
        RunProjectStartup(ctx);
    }

    private static UIConfig BuildUiConfig()
    {
        var uiConfig = new UIConfig();
        ConfigureProjectUi(uiConfig);
        return uiConfig;
    }

    static partial void RegisterProjectServices(GameContext ctx);
    static partial void ConfigureProjectScenes(YSceneManager sceneManager);
    static partial void ConfigureProjectUi(UIConfig uiConfig);
    static partial void ConfigureProjectNetwork(MessageRegistry registry);
    static partial void RunProjectStartup(GameContext ctx);
}
