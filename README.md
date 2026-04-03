# YFramework

轻量级、面向服务的 Unity 游戏框架，基于依赖注入容器 **GameContext** 构建，提供开箱即用的核心系统，支持通过 partial 方法扩展业务逻辑。

> Unity 2022.3 · URP · C#

## 项目结构

```
YFramework/
├── client/                     # Unity 工程
│   ├── Assets/
│   │   ├── Scripts/
│   │   │   ├── Framework/      # 框架核心（与业务无关）
│   │   │   ├── GamePlay/       # 业务层（基于框架构建）
│   │   │   └── Editor/         # 编辑器扩展
│   │   └── ScriptGenerated/    # 配表自动生成代码
│   └── Packages/
├── excel/                      # Excel 配置表源文件
└── tools/                      # 配表发布工具链（Python）
```

## 核心架构

### 启动流程

```
GameLoop.Awake()
  → GameBootstrapper.BuildContext()    // 注册所有服务
    → GameContext.InitAll()            // 按注册顺序初始化
      → RunProjectStartup()           // 业务启动入口
```

### 帧循环

```
GameLoop.Update()       → ITickable.Tick(dt)
GameLoop.FixedUpdate()  → IFixedTickable.FixedTick(fdt)
GameLoop.LateUpdate()   → ILateTickable.LateTick(dt)
```

服务实现对应接口即可自动接收帧回调，关闭时按注册逆序执行 `Shutdown()`。

## 框架模块

| 模块 | 类 | 说明 |
|------|-----|------|
| 服务容器 | `GameContext` | 类型安全的 DI 容器，`Get<T>()` / `TryGet<T>()` |
| 事件系统 | `EventMgr` | 零分配、类型安全的泛型事件，支持最多 4 参数 |
| 资源管理 | `ResMgr` | 引用计数 `ResourceHandle<T>`，同步/异步加载，场景切换自动卸载 |
| 对象池 | `ObjectPool` | 异步模板加载，时间衰减回收，逐帧释放限制 |
| UI 系统 | `UIMgr` | 5 层 Canvas 架构（Normal/Top/RayCast/Tips/PopText），页面生命周期管理 |
| 场景管理 | `GotSceneManager` | 异步切换，`GotSceneBase` 生命周期，支持 Loading 过渡 |
| 相机 | `CameraMgr` | Cinemachine 集成，射线点击/悬停/拖拽事件分发 |
| 音频 | `SoundMgr` | 多通道（BGM/SFX/UI），交叉淡入淡出，最多 16 路 SFX |
| 数据存储 | `StoreMgr` | `DataContainer<T>` 持久化，可替换存储驱动 |
| 寻路 | `GotAStarManager` | A* Pathfinding 封装 |
| 状态机 | `YStateMachine` | 简易分层状态机，支持状态回退 |
| 定时器 | `Timers` | 全局定时器，支持无限循环 |
| 协程 | `CoroutineRunner` | 服务化协程管理 |
| 屏幕监听 | `ScreenMonitor` | 分辨率变化事件 |

## 业务扩展方式

通过 `GameBootstrapper` 中的 partial 方法接入业务，无需修改框架代码：

```csharp
// GameBootstrapper.Project.cs
partial void RegisterProjectServices(GameContext ctx)
{
    ctx.Register(new PlayerManager());
    ctx.Register(new EnemiesManager());
}

partial void ConfigureProjectScenes(GotSceneManager sceneMgr)
{
    sceneMgr.RegisterScene<GameStartScene>(GotSceneType.Startup);
    sceneMgr.RegisterScene<GameMainScene>(GotSceneType.GamePlay);
}

partial void ConfigureProjectUi(IUIService uiMgr)
{
    UIConfig.Register<StartPanel>(UIEnum.StartPanel, UILayerEnum.Normal, "Prefabs/UI/StartPanel");
}
```

## UI 页面生命周期

```
OnLoad() → BeforeShow(param) → OnShow() → OnHide() → OnResize()
```

`UIPageBase` 提供 `Context` 和 `UIManager` 注入，通过 `GetService<T>()` 解析其他服务。

## 配表工具链

Excel 配置表通过 Python 工具链发布为 Proto + C# 代码：

```
Excel (excel/) → publish_config.py → .proto → protoc → C# 类 + 二进制数据
```

配置文件：`tools/tools_config.ini`

```bash
# 发布配表
cd tools
python publish_config.py
```

## 主要依赖

- **Cinemachine** — 相机系统
- **Universal RP** — 渲染管线
- **A* Pathfinding** — 寻路
- **TextMeshPro** — UI 文本
- **DOTween** — 动画缓动
- **Entities / Burst** — 高性能计算
- **SteamWorks.NET** — Steam 集成
