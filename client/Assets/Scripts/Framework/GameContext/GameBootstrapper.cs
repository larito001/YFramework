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
        var aStarManager = new YAStarManager(ctx.Get<SceneReferenceService>(), ctx.Get<ResMgr>());
        ctx.Register(aStarManager);
        RegisterProjectServices(ctx);
        Debug.Log(ctx.Get<ConfigManager>().heroConfig.Get(1001).HeroName);
        ctx.Register(new CharacterManager());

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
