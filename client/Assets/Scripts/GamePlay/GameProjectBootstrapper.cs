using YOTO;
using YOTO.Gameplay.Net;
using YOTO.Network;

/// <summary>
/// 网络消息 key。与 <see cref="YOTOEventType"/> 完全独立——
/// 后者走本地 EventMgr，前者走 NetManager 的 protobuf 路由。
///
/// 用 const int 而非 enum：API 不再走 Enum 装箱，子模块可以再开一个 static class 续写
/// （比如 public static class NetKey_Battle { public const int CmdAttack = 500; ... }）。
///
/// 约定：
///   - 0 保留，永远不要用（NetManager 把 key=0 走 raw 字节通道）
///   - 同一个 NetKey 上注册的 protobuf 消息类型必须固定，整个项目唯一对应
///   - 默认 lane = 0 适合一般消息；要走多通道时 lane 1+ 自定义语义
/// </summary>
public static class NetKey
{
    public const int None = 0;

    // ── 房间 / 大厅级 ──────────────────────────────
    public const int Hello = 1;            // 入房后双方互发的 hello/handshake
    public const int Heartbeat = 2;        // 可选心跳

    // ── 帧同步 / 状态（推荐走 lane 1）──────────────
    public const int PlayerInput = 100;    // 客户端输入上传
    public const int PlayerState = 101;    // 服务端权威状态广播
    public const int Snapshot    = 102;    // 完整快照

    // ── 命令 / 关键事件（推荐走 lane 2）────────────
    public const int CmdSpawn    = 200;
    public const int CmdDespawn  = 201;
    public const int CmdDamage   = 202;

    // ── 聊天 / 杂项（lane 0 即可）──────────────────
    public const int ChatMessage = 300;

    // ── 大数据传输（推荐走 lane 3）─────────────────
    public const int BulkTransfer = 400;
}

public static partial class GameBootstrapper
{
    static partial void RegisterProjectServices(GameContext ctx)
    {
        ctx.Register(new InputService());
        // 背包系统：纯逻辑 service，不需要 Tick。EventMgr / ConfigManager 已在 BuildContext 中先行注册，
        // BagSystem.Init 里 ctx.Get 取得后接配表 + 桥接 RefreshBagList 给 UI。
        ctx.Register(new BagSystem());
        // 宝箱系统在 BagSystem 之后注册：Init 里 ctx.Get<BagSystem>() 复用其物品配置。
        ctx.Register(new ChestSystem());
        // 世界交互（靠近宝箱 + F 打开）：Init 只订阅 InputService 的 F 键，CharacterManager/ViewManager 在 Tick 里懒取。
        ctx.Register(new WorldInteractionSystem());
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
        uiConfig.RegisterLoading<LoadingPanel>(UIEnum.LoadingPanel, UILayerEnum.RayCast, "UI/Boot/LoadingPanel", 0f);
        uiConfig.Register<StartPanel>(UIEnum.StartPanel, UILayerEnum.Normal, "UI/Boot/StartPanel");
        uiConfig.Register<SaveSlotPanel>(UIEnum.SaveSlotPanel, UILayerEnum.Normal, "UI/Boot/SaveSlotPanel");
        uiConfig.Register<GameMainPanel>(UIEnum.GameMainPanel, UILayerEnum.Normal, "UI/Main/GameMainPanel");
        uiConfig.Register<FinishPanel>(UIEnum.FinishPanel, UILayerEnum.Normal, "UI/Main/FinishPanel");
        uiConfig.Register<SettingPanel>(UIEnum.SettingPanel, UILayerEnum.Normal, "UI/Setting/SettingPanel");
        uiConfig.Register<BagPanel>(UIEnum.BagPanel, UILayerEnum.Normal, "UI/Bag/BagPanel");
        uiConfig.Register<ChestPanel>(UIEnum.ChestPanel, UILayerEnum.Normal, "UI/Bag/ChestPanel");
    }

    static partial void RunProjectStartup(GameContext ctx)
    {
        // 组装层只负责"显示首屏"。物品使用处理器等 gameplay 内容由 gameplay 层(场景)注册,
        // 不在 composition root 里编码具体物品逻辑(见 GameStartScene)。
        ctx.Get<UIMgr>().Show<StartPanel>();
    }

    static partial void ConfigureProjectNetwork(MessageRegistry registry)
    {
        // 协议号取自 NetKey；新增消息时在 NetKey 加 const，然后在这里登记一次。
        registry.Register<ChatMessage>((ushort)NetKey.ChatMessage);
    }
}
