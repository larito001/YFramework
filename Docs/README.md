# YFramework 规范文档

本目录收录四份规范，描述 YFramework 仓库的工程、代码、模块、需求约定。文档相互独立，按角色阅读：

**程序 / 全员**

1. [项目规范](./项目规范.md) — 仓库结构、版本/依赖、资源约定、构建/发布、Git 协作。
2. [代码规范](./代码规范.md) — 命名、文件组织、注释、错误处理、性能、Unity 特定约定。
3. [模块规范](./模块规范.md) — 框架各模块的职责、对外 API、扩展约定、典型用法。

**策划**

4. [需求规范](./需求规范.md) — 需求文档的格式、内容、命名、协作流程。**所有需求文档必须遵循此规范。**

## 文档生成时的清理与重构

生成本规范的过程中，对一批无引用的死代码做了删除，并按规范执行了一轮结构整改：

**死代码清理**

| 路径 | 处理 | 理由 |
|---|---|---|
| `Framework/UI/UITypeBase.cs` | 删除 | 整文件被注释掉，无引用 |
| `Framework/Pool/TestPoolObject.cs` | 删除 | 测试样例，无任何引用 |
| `Framework/GameContext/TestService.cs` | 删除 | 空服务，仅在 `GameBootstrapper` 注册无逻辑 |
| `Framework/Store/GameDataManager.cs` | 删除 | 未注册到 `GameContext`，无引用 |
| `StoreMgr.cs` 内 `ExampleUsage` 区块 | 删除 | 残留示例 MonoBehaviour |
| `GameBootstrapper.cs` 中 `TestService` 注册 | 删除 | 配合上面 |
| `Framework/Log/` 空目录 | 删除 | 占位无内容，规划中的 `LogMgr` 后续按需补回 |

**结构整改**

| 整改 | 内容 |
|---|---|
| `SceneReferenceKeys` 拆分 | 框架键 `MainCamera`/`MainCameraVirtual`/`MainLight`/`AStarRoot` 迁至 `Framework/Scene/SceneRefKeys.cs`；业务键 `PlayerSpawn`/`Spline` 留在 `GamePlay/Scene/GameSceneReferenceKeys.cs` 并改类名 `GameSceneRefKeys` |
| `EventMgr` 与业务事件解耦 | `YOTOEventType` 枚举从 `Framework/Event/EventConfig.cs` 迁至 `GamePlay/Event/GameEventTypes.cs`；`EventMgr` 入参由具体枚举改为 `System.Enum`，框架不再直接引用业务枚举 |
| 文件名 = 类名 | `GotSceneBase.cs`→`YSceneBase.cs`；`GotSceneManager.cs`→`YSceneManager.cs`；`GotConfigManager.cs`→`ConfigManager.cs`（连同 `.meta` 一并 `git mv`） |
| 目录拼写修正 | `Framework/PathFinding/com.arongranberg.astar.enxtension/` → `astar.extension/` |
| `FlyText` 服务上移 | 从 `GamePlay/UI/FlyTexts/` 迁至 `Framework/UI/FlyText/`，与其在 `GameBootstrapper.BuildContext` 中作为框架服务的注册位置一致 |
| 仓库根 `README.md` | 类名 `GotSceneManager`/`GotAStarManager` → `YSceneManager`/`YAStarManager`；`partial` 业务示例签名修正为实际签名 |
