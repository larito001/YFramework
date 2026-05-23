# YFramework 工作流控制台 (webCtrl)

一个本地 Web 应用，编排 Claude Code 各 skill 串成完整工作流。

```
人类需求 → /requirement-analysis → 策划案/
                                      ↓
                              人工审查（通过/打回）
                                      ↓
                          /code-planning → 代码规划/
                                      ↓
                          /code-generation → 代码
                                      ↓
                          /code-review → 代码优化规划/
                                      ↓
                          /prefab-generation → Editor/PrefabBuilders/*.cs
                                      ↓
                              （在 Unity 内点菜单产出 .prefab）
```

## 功能

- 一个浏览器页面汇总展示 `策划案/` `代码规划/` `代码优化规划/` 三个目录的 markdown 文档与 frontmatter 状态。
- 每个文档根据其 `status` / 类型显示对应的操作按钮。
- 点按钮 → 在新的 PowerShell 窗口启动一个独立 Claude Code 会话，初始指令已自动填好（如 `/code-planning GP-Combat-v1`）。
- 实时显示 `git diff` 未提交改动，一键启动 `/code-review --diff`。
- 人工审查（通过/打回）直接在网页内操作 frontmatter `status`，不开新窗口。
- 5 秒自动轮询刷新（可关）。

## 启动

第一次：

```powershell
cd C:\UnityProject\YFramework\webCtrl
npm install
npm start
```

之后只需 `npm start`。控制台输出：

```
[webCtrl] http://localhost:7777
[webCtrl] 项目根: C:\UnityProject\YFramework
[webCtrl] 策划案         → C:\UnityProject\YFramework\策划案
[webCtrl] 代码规划       → C:\UnityProject\YFramework\代码规划
[webCtrl] 代码优化规划   → C:\UnityProject\YFramework\代码优化规划
```

浏览器打开 <http://localhost:7777>。

## 工作流操作流

| 阶段 | 操作位置 | 触发 | 结果 |
|---|---|---|---|
| ① 新建需求 | 顶部 [+ 新建需求] | 文本框输入"想做什么" | 启动 `/requirement-analysis` 新窗口 |
| ① 修改需求 | 选中策划案 → [✏ 继续修改] | 文本框输入修改点 | 启动 `/requirement-analysis 基于 <id> 修改: ...` |
| ② 通过审查 | 选中策划案 → [✓ 通过审查] | 直接确认 | frontmatter `status: Approved` |
| ② 打回 | 选中策划案 → [✗ 打回] | 文本框输入理由 | `status: Draft` + 启动 `/requirement-analysis 审查打回 ...` |
| ③ 模块分析 | 选中 Approved 策划案 → [⚙ 模块分析] | 直接 | 启动 `/code-planning <id>` |
| ④ 代码生成 | 选中代码规划 → [⚡ 生成代码] | 直接 | 启动 `/code-generation <id>` |
| ⑤ 评审 diff | 选中"未提交改动" → [🔍 评审本批改动] | 直接 | 启动 `/code-review --diff` |
| ⑤ 评审范围 | 顶部 [🔍 评审任意范围] | 文本框输入路径/类名 | 启动 `/code-review <scope>` |
| ⑤ 修复优化 | 选中代码优化规划 → [🛠 按报告执行修复] | 直接 | 启动 Claude 让其按报告 §5 逐条修 |
| ⑥ 预制体（按规划） | 选中代码规划 → [🎨 生成预制体] | 直接 | 启动 `/prefab-generation <plan-id>` 写 Editor 构建器 |
| ⑥ 预制体（自由范围） | 顶部 [🎨 生成预制体] | 文本框输入 plan-id / 类名 / `--diff` | 启动 `/prefab-generation <scope>` |
| ⑥ 实际产出 prefab | 在 Unity 内 [YFramework/Build Prefabs/[All]] | Unity 菜单 | 不在 webCtrl 范围；本步由 Unity Editor 完成 |

## 多窗口协作

每次启动会开一个**独立**的 PowerShell + Claude 会话。窗口之间不共享上下文，**通过文件系统协作**：

- 阶段 ① 写出 `策划案/...md` → 阶段 ③ 在另一个窗口读取它
- 阶段 ③ 写出 `代码规划/...md`（含 `source: <策划案 id>` 反查链） → 阶段 ④ 读取它
- 阶段 ④ 写出 `client/Assets/Scripts/GamePlay/...cs` → 阶段 ⑤ 通过 git diff 看到这些改动

控制台 5 秒轮询一次三个目录与 `git status`，能在数秒内反映出新生成的文档。

## 端口

默认 7777。改：

```powershell
$env:PORT=8080; npm start
```

## 安全说明

- 仅监听 `localhost`（Express 默认行为）；外网无法访问。
- API 限定只能读 `策划案/` `代码规划/` `代码优化规划/` 三个目录；`PUT /api/file/status` 仅修改 frontmatter 的 `status` 与 `updated` 字段，不允许任意改文件。
- `POST /api/launch` 会启动新窗口执行 `claude '<prompt>'`。**prompt 由前端传入**：本工具仅供本机使用，不要把端口暴露到外网。

## 依赖

- Node.js ≥ 18（实测 v24 可用）
- `claude.exe` 在 PATH（默认安装在 `C:\Users\<you>\.local\bin\`）
- Windows PowerShell 5.1+ 或 PowerShell 7

## 故障排查

| 现象 | 原因 / 处理 |
|---|---|
| 新窗口闪一下就关 | PATH 找不到 `claude`。在 PowerShell 里执行 `Get-Command claude` 确认。 |
| 中文显示为 ??? | `webCtrl` 写入临时 `.ps1` 时已加 UTF-8 BOM，PowerShell 5.1 应能识别。若仍乱码，把窗口字体改为"NSimSun"或升级到 PowerShell 7。 |
| `npm start` 报 `EADDRINUSE` | 端口 7777 被占。改 `$env:PORT=<其他>` 启动。 |
| 左侧目录始终为空 | 确认 `策划案/` 等目录存在；空目录会显示"（无文档）"。 |
| 自动刷新没反应 | 检查右上角"自动刷新"勾选；或改长轮询周期（修改 `app.js` 的 5000ms）。 |

## 后续可扩展

当前 v0.1 不做的事项：

- 不直接编辑文档内容（避免与 Claude 写入冲突）
- 不显示 Claude 窗口实时输出（每个窗口独立，只能在窗口内看）
- 不做并发任务调度（用户自己决定何时开几个窗口）
- 不做 frontmatter 之外的字段修改

如需扩展，看 `server.js` API 定义、`public/app.js` 的 `renderActions(...)` 是改起点。
