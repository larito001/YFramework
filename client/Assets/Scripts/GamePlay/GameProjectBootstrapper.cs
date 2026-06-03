using UnityEngine;
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
        // 登录:渠道无关的登录服务(ILoginService),当前仅接入 TapTap。后续接其它登录模块时,
        // 新增一个 ILoginProvider 实现并在此多一行 AddProvider 即可(登录界面按渠道加按钮),其余无需改动。
        // SDK 调用都在 TAPTAP_LOGIN 宏内,未接入 SDK 时 Editor 走模拟、真机走"未接入"回退。详见 TapTapLoginProvider 文件末注释。
        var login = new LoginManager();
        login.AddProvider(new TapTapLoginProvider());
        ctx.Register<ILoginService>(login);
        // 排行榜(ILeaderboardService):TapTap 排行榜。对局结束提交本局总分,主界面「排行榜」按钮查看榜单。
        // SDK 调用都在 TAPTAP_LEADERBOARD 宏内,未接入时 Editor 走模拟榜单、真机走"未接入"回退。详见 TapTapLeaderboardService 文件末注释。
        ctx.Register<ILeaderboardService>(new TapTapLeaderboardService());
        // 云存档(ICloudSaveService):把当前激活存档槽的进度打包同步到 TapTap 云。依赖登录 + StoreMgr。
        // SDK 调用在 TAPTAP_CLOUDSAVE 宏内,未接入时 Editor 走模拟、真机走"未接入"回退。仅提供 API,
        // 何时上传/下载(自动存档点 / 手动按钮 / 本地云对比)由上层决定,见 TODO_TapTapLogin.md。
        ctx.Register<ICloudSaveService>(new TapTapCloudSaveService());
        // 云存档自动同步(B 方案):登录后云端较新则自动下载;任意进度存档落盘后防抖合并、自动上传当前槽。
        // 业务侧照常 Save 即可,无需感知云端。注册在登录/云存档/StoreMgr 之后。
        //
        // 仅 Android 真机启用:编辑器/PC 下 TapTap 云存档后端不认本(移动)应用(PC 云存档还要求从 TapTap PC 客户端启动),
        // 会持续报 "Client ID 不存在"。故编辑器/PC 不注册同步服务——不触发任何上传/下载,本地存档不受影响。
        // 等上 Android 真机(且后台已开通云存档服务)时,这里自动生效。
#if UNITY_ANDROID && !UNITY_EDITOR
        ctx.Register(new CloudSaveSyncService());
#endif

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
        // 任务进度系统:任务定义走 task 配表,本系统只管玩家进度(登录/连登/击杀/看广告/获武器)+ 领取发奖。
        // 注册在依赖(CurrencySystem/BagSystem/LoadoutSystem)之后,Init 里 ctx.Get 取得它们。随存档槽存档。
        ctx.Register(new TaskProgressSystem());
        // 关卡系统:读 map 配表,记录玩家选中的关卡(选图界面用)。纯逻辑 service,Init 里取 ConfigManager(已先注册)。
        ctx.Register(new MapSystem());
        // 动物生成系统:读 animal 配表,进对局时在地面随机散布动物(Resources/Animals 下的低多边形动物,平时只 idle)。
        ctx.Register(new AnimalSystem());
        // 动物图鉴系统:记录已击杀(发现)的动物 id,被杀死即解锁图鉴条目。随存档槽存档,目录读 animal 配表。
        ctx.Register(new CodexSystem());
        // 每日广告补体力:记录当天看广告补体力的次数(随存档槽 StoreMgr 存档 → 本地+云),每天上限见 DailyAdEnergySystem。
        ctx.Register(new DailyAdEnergySystem());
        // 世界交互（靠近宝箱 + F 打开）：Init 只订阅 InputService 的 F 键，靠近参照点用主相机（旧 TPS 玩家系统已移除）。
        ctx.Register(new WorldInteractionSystem());
        // 激励广告(Dirichlet / TapADN):实现框架预留的 IAdService,大厅「体力补充」按钮看完发奖。
        // 激励视频仅 Android;Editor 走"模拟看完"便于联调。PC/Steam 不注册 → StartPanel 自动走"未接入"回退、不发奖。
        // 接入细节(导包/凭证/DIRICHLET_AD 宏/Android 打包)见 DirichletAdService.cs 文件末尾注释。
#if UNITY_ANDROID || UNITY_EDITOR
        ctx.Register<IAdService>(new DirichletAdService());
#endif
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
        // 登录界面:进大厅前的登录门(仅 TapTap)。静默自动登录成功则不展示,失败才弹出等待用户登录。
        uiConfig.Register<LoginPanel>(UIEnum.LoginPanel, UILayerEnum.Normal, "UI/Login/LoginPanel");
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
        // 排行榜:从主界面「排行榜」按钮进入。优先调起 TapTap 内置榜单 UI,不可用(编辑器/未接入)则用自绘列表兜底。
        uiConfig.Register<LeaderboardPanel>(UIEnum.LeaderboardPanel, UILayerEnum.Normal, "UI/Leaderboard/LeaderboardPanel");
        // 通用确认弹窗:放 Top 层,叠在普通界面之上。通过 ConfirmParam 传标题/内容/回调。
        uiConfig.Register<ConfirmPanel>(UIEnum.ConfirmPanel, UILayerEnum.Top, "UI/Common/ConfirmPanel");
        // 通用奖励领取弹窗:Top 层,横排展示道具/货币,默认弹出 1 秒自动消失。通过 RewardClaimParam 传奖励清单。
        uiConfig.Register<RewardClaimPanel>(UIEnum.RewardClaimPanel, UILayerEnum.Top, "UI/Common/RewardClaimPanel");
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
                store.SaveAll(() => { OnLobbyReady(ctx); GateLoginThenLobby(ctx, ui); }); // 种子值落盘后过登录门再进大厅
            }
            else
            {
                // 已有存档:激活最近游玩的槽并整体读档,大厅随即显示存档进度(读完会触发各刷新事件,UI 自动更新)。
                store.SetActiveSlot(MostRecentSlotId(store));
                store.LoadAll(() => { OnLobbyReady(ctx); GateLoginThenLobby(ctx, ui); }); // 读回进度后过登录门再进大厅
            }
        });
    }

    /// <summary>读档完成后、进大厅前的一次性结算:任务登录结算(每日登录/连续登录/每日重置)需在进度读回后跑。</summary>
    private static void OnLobbyReady(GameContext ctx)
    {
        ctx.Get<TaskProgressSystem>().RegisterLogin();
    }

    /// <summary>
    /// 登录门:进大厅前先过登录。启动时尝试静默自动登录——
    ///   · 已有有效会话:直接进大厅(用户无感);
    ///   · 无会话/失败:收起加载页、展示登录界面,由用户点「TapTap 登录」,成功后界面内 Show&lt;StartPanel&gt; 进大厅。
    /// 存档已在本步骤前读好,故登录成功后直接显示大厅即可,无需再次读档。
    /// </summary>
    private static void GateLoginThenLobby(GameContext ctx, UIMgr ui)
    {
        if (!ctx.TryGet<ILoginService>(out var login))
        {
            // 兜底:未注册登录服务时不应卡死在加载页,直接进大厅(理论上不会发生)。
            Debug.LogWarning("[GameBootstrapper] 未注册 ILoginService,跳过登录直接进大厅。");
            EnterLobby(ui);
            return;
        }

        login.TryAutoLogin(res =>
        {
            if (res.success)
            {
                EnterLobby(ui); // 已登录:直接进大厅
            }
            else
            {
                ui.HideLoading();      // 收起加载页,露出登录界面
                ui.Show<LoginPanel>(); // 等待用户登录(成功后界面内进大厅)
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
