# ResourcesAssets

**用途**：AssetBundle（AB 包）预留目录。

未来通过 `AssetBundle.LoadFromFileAsync` 等 API 加载的资源放这里，与 `Resources/`（被 Unity 编辑器索引并打入主包）严格区分。

## 约束

- 当前不会被 `Resources.Load` 索引，放进来的资源 **不会** 自动打入主包；
- 接入 AB 包流程后再开放写入，否则不要往这里加资源；
- 子目录按"业务模块/资源类型"组织，例如 `ResourcesAssets/Battle/Prefabs/`、`ResourcesAssets/Lobby/Audio/`；
- 加资源时同步更新本 README 与 `Docs/项目规范.md §3`。

## 与 Resources/ 的区别

| | Resources/ | ResourcesAssets/ |
|---|---|---|
| 加载入口 | `Resources.Load(...)` / `ResMgr` | `AssetBundle.LoadFromFileAsync` |
| 是否进主包 | 是（强制） | 否（按 AB 打包策略） |
| 适合资源 | 启动期必需、轻量、跨模块共用 | 业务大资源、可热更、按需加载 |
