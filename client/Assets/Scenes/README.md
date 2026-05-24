# Scenes

按场景生命周期分类。详见 `Docs/项目规范.md §4`。

| 子目录 | 用途 | 是否进 Build Settings |
|---|---|---|
| `Boot/` | 启动场景（`GameStart.unity`，挂 `GameLoop`） | 第 0 位 |
| `Persistent/` | 常驻场景（`UISceneDontDelete.unity`，UI 根） | 第 1 位 |
| `GamePlay/` | 业务玩法场景（按业务模块新建） | 按需 |
| `Sandbox/` | 验证/调试用临时场景 | **不进** |

## 新增场景流程

1. 在对应子目录新建 `.unity`；
2. 实现 `YSceneBase` 派生，在 `GameProjectBootstrapper.ConfigureProjectScenes` 中 `RegisterScene<T>()`；
3. 切换：`ctx.Get<YSceneManager>().SwitchScene(YSceneType.XXX, args)`；
4. 进入 Build Settings → Add Open Scenes（Sandbox 除外）。
