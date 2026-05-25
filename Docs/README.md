# YFramework 规范文档

本目录收录三份规范，描述 YFramework 仓库的工程、代码、模块约定。三份文档相互独立，按以下顺序阅读最高效：

1. [项目规范](./项目规范.md) — 仓库结构、版本/依赖、资源约定、构建/发布、Git 协作。
2. [代码规范](./代码规范.md) — 命名、文件组织、注释、错误处理、性能、Unity 特定约定。
3. [模块规范](./模块规范.md) — 框架各模块的职责、对外 API、扩展约定、典型用法。

## Skill 工作流

规范是死的，落地靠 `.claude/skills/` 下的 skill 链。当前两环：

| Skill | 触发 | 输入 | 输出 |
|---|---|---|---|
| `code-planning` | `/code-planning <需求描述或策划案 id>` | 自由文本需求 / `策划案/...md` | `代码规划/.../GP-Xxx-Plan-v1.md` |
| `code-generation` | `/code-generation <Plan id>` | `代码规划/...md` | `client/Assets/Scripts/` 下的 `.cs` 文件 |

约束要点：

- **Framework 改动**：两个 skill 都允许写 `Framework/` 代码，但必须先经 `code-planning` 出规划且过其 §2.10 六条 Framework 设计原则（接口先行 / 向后兼容 / 职责单一 / 依赖方向单向 / 可池化可关闭 / 注册顺序）。直接修改 Framework 不走规划 = 违规。
- **依赖方向**：仍然严格 `Editor → GamePlay → Framework → Unity`，详见项目规范 §1.1。
- **代码评审**：见代码规范 §13；Framework 改动评审参考 code-planning skill §2.10。

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
