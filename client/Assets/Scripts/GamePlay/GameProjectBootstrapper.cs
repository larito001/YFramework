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
        // 战斗输入闸门：注册早于 InputService，于同帧内先跑，按 UI 状态先设好 CombatEnabled——
        // 非主界面 UI（背包/宝箱/设置等）打开时屏蔽战斗按键，关闭后恢复。
        // Init 走 InitAll 延迟阶段（此时所有 service 已注册），ctx.Get<InputService>/<UIMgr> 均可取到。
        ctx.Register(new CombatInputGate());
        // 场景输入闸门:仅游戏场景开启输入总开关,菜单/启动界面屏蔽快捷键(避免非游戏场景按 B 唤起背包)。
        // 注册早于 InputService,同帧先设好 IsEnabled。
        ctx.Register(new InputSceneGate());
        ctx.Register(new InputService());
        // 画面设置服务:全屏/垂直同步/画质/分辨率,改动即应用并存盘(Settings 分类)。
        ctx.Register(new GraphicsSettings());
        // 货币系统:独立钱包(多币种),与背包解耦。纯逻辑 service,Init 里只取 EventMgr/StoreMgr(均已先注册),
        // 余额变化桥接 RefreshCurrency 给 UI。按 Progress 随存档槽存档。
        ctx.Register(new CurrencySystem());
        // 背包系统：纯逻辑 service，不需要 Tick。EventMgr / ConfigManager 已在 BuildContext 中先行注册，
        // BagSystem.Init 里 ctx.Get 取得后接配表 + 桥接 RefreshBagList 给 UI。
        ctx.Register(new BagSystem());
        // 装备系统:管理已拥有装备 + 每类出战选择(枪械/瞄准镜/子弹),读 item 配表 shopCategory。
        // 注册在 ShopSystem 之前:商店购买分类商品时 Grant 给它解锁。
        ctx.Register(new LoadoutSystem());
        // 商店系统:撮合 CurrencySystem(钱包)与 BagSystem/LoadoutSystem(物品/装备),目录读 item 配表 price>0 的物品。
        // 注册在其依赖之后,Init 里 ctx.Get 取得它们 + ConfigManager。
        ctx.Register(new ShopSystem());
        // 关卡系统:读 map 配表,记录玩家选中的关卡(选图界面用)。纯逻辑 service,Init 里取 ConfigManager(已先注册)。
        ctx.Register(new MapSystem());
        // 动物生成系统:读 animal 配表,进对局时在地面随机散布动物(Resources/Animals 下的低多边形动物,平时只 idle)。
        ctx.Register(new AnimalSystem());
        // 世界交互（靠近宝箱 + F 打开）：Init 只订阅 InputService 的 F 键，靠近参照点用主相机（旧 TPS 玩家系统已移除）。
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
        uiConfig.Register<ShopPanel>(UIEnum.ShopPanel, UILayerEnum.Normal, "UI/Shop/ShopPanel");
        uiConfig.Register<EquipPanel>(UIEnum.EquipPanel, UILayerEnum.Normal, "UI/Equip/EquipPanel");
        // 图鉴:装饰公仔 / 荣誉卡片 两类收藏,分页展示锁定/解锁卡片。从主界面「图鉴」按钮进入。
        uiConfig.Register<CodexPanel>(UIEnum.CodexPanel, UILayerEnum.Normal, "UI/Codex/CodexPanel");
        // 任务:每日/常规任务列表,从主界面「任务」按钮进入。任务走配表(task.xlsx → Task.bytes),奖励物品查 item 配表。
        uiConfig.Register<TaskPanel>(UIEnum.TaskPanel, UILayerEnum.Normal, "UI/Task/TaskPanel");
        // 选择关卡:点「准备」先进这里选关,再进装备界面。关卡走配表(map.xlsx → Map.bytes)。
        uiConfig.Register<MapSelectPanel>(UIEnum.MapSelectPanel, UILayerEnum.Normal, "UI/Map/MapSelectPanel");
        // 通用确认弹窗:放 Top 层,叠在普通界面之上。通过 ConfirmParam 传标题/内容/回调。
        uiConfig.Register<ConfirmPanel>(UIEnum.ConfirmPanel, UILayerEnum.Top, "UI/Common/ConfirmPanel");
    }

    static partial void RunProjectStartup(GameContext ctx)
    {
        // 启动即确保有"激活存档槽"。进度数据(金币/体力/背包/装备)按 slot{id}_ 前缀分槽落盘,
        // 槽=0(未激活)时读写的是无前缀的全局键。各系统 Init 时槽还是 0,读到的是新档种子值;
        // 若不在大厅可交互前激活某个槽并整体读档,大厅就永远显示种子值——存档其实写在 slot{id}_ 文件里
        // 却从不被读回,表现为"金币/体力/背包/装备都没保存"。槽清单是异步读入的,故用 WhenSlotsReady 等就绪。
        var store = ctx.Get<StoreMgr>();
        var ui = ctx.Get<UIMgr>();

        // 首次加载读档期间先盖一层加载页(RayCast 层,挡住输入与种子值闪烁),读/写完成后再进大厅。
        // 加载页"最短展示时长"由 UIMgr 统一兜底(boot 与场景切换都生效),这里读完直接进大厅即可。
        ui.ShowLoading();
        store.WhenSlotsReady(() =>
        {
            if (store.Slots.Count == 0)
            {
                // 全新存档:立刻建槽并把当前种子值落盘,整个大厅会话都读写这个槽(避免大厅内改动落到孤儿全局键)。
                store.CreateSlot();
                store.SaveAll(() => EnterLobby(ui)); // 种子值落盘后再进大厅
            }
            else
            {
                // 已有存档:激活最近游玩的槽并整体读档,大厅随即显示存档进度(读完会触发各刷新事件,UI 自动更新)。
                store.SetActiveSlot(MostRecentSlotId(store));
                store.LoadAll(() => EnterLobby(ui)); // 读回进度后再进大厅
            }
        });
    }

    /// <summary>读档完成后进入大厅:先显示主界面,再收起加载页(HideLoading 会兜底"最短展示时长")。组装层只负责"显示首屏"。</summary>
    private static void EnterLobby(UIMgr ui)
    {
        ui.Show<StartPanel>();
        ui.HideLoading();
    }

    /// <summary>最近游玩(lastPlayedUnix 最大)的存档槽 id;并列/缺省时取列表最后一个(最新创建)。调用前须确保清单已就绪且非空。</summary>
    private static int MostRecentSlotId(StoreMgr store)
    {
        var slots = store.Slots;
        int id = slots[slots.Count - 1].id;
        long best = long.MinValue;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].lastPlayedUnix >= best)
            {
                best = slots[i].lastPlayedUnix;
                id = slots[i].id;
            }
        }
        return id;
    }

    static partial void ConfigureProjectNetwork(MessageRegistry registry)
    {
        // 协议号取自 NetKey；新增消息时在 NetKey 加 const，然后在这里登记一次。
        registry.Register<ChatMessage>((ushort)NetKey.ChatMessage);
    }
}
