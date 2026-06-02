# TapTap 排行榜 — 接入与上线待办

> 现状:排行榜服务分层(`ILeaderboardService` / `TapTapLeaderboardService`)、排行榜界面(`LeaderboardPanel` + `LeaderboardPanelBuilder`)、主界面入口(`StartPanel` 右下「排行榜」按钮)、分数提交(`GameMainPanel.ShowResult` 对局结束提交本局总分)代码已就绪。
> 所有 TapTap 排行榜 SDK 调用都在 `TAPTAP_LEADERBOARD` 宏内,且**仅移动真机**才走真 SDK——Editor 永远走"模拟榜单"、PC 走"未接入"回退(SDK 只有 Mobile 实现,无 PC 桥接)。
> 下列为把它跑成"真排行榜 + 可上线"还需要做的事。相关记忆见 `reference_taptap_leaderboard`。

---

## 1. 立即要做(否则界面/功能跑不起来)

- [ ] **生成排行榜界面预制体**:Unity 菜单 `Tools/UI/Build LeaderboardPanel Prefab`(或 `Tools/UI/Build ALL UI Prefabs`)。
      不生成则点「排行榜」时 `Show<LeaderboardPanel>` 找不到 `Resources/UI/Leaderboard/LeaderboardPanel.prefab`(已修过一次)。
- [ ] **重建主界面预制体**:`Tools/UI/Build StartPanel Prefab`(让右下「排行榜」按钮 + `btn_leaderboard` 接线生效)。
- [ ] Editor 联调:大厅点「排行榜」→ 看到**模拟榜单**(猎手_01~20);打一局结束后再开,「我」按本局分数插进榜。确认链路通。

## 2. 接入真实排行榜(SDK 已通过 UPM 接入)

> ✅ **已用 UPM 接入**:`Packages/manifest.json` 已加 `com.taptap.sdk.leaderboard@4.10.3`(与 core/login/cloudsave 同版本,共用 com.taptap scoped registry)。
> ✅ **已开宏**:`ProjectSettings.asset` 的 **Android** 已加 `TAPTAP_LEADERBOARD`(随 login/cloudsave 之后)。
> ✅ **Standalone 故意不加** `TAPTAP_LEADERBOARD`:排行榜 SDK 无 PC 实现,PC 上代码自动走"未接入"回退。
> ✅ **字段已对齐 4.10.3**:`Score.rank/score` 为 `long?`;`Score.User.openid`(小写)/`name`;头像 `User.avatar` 是 `Image` 对象非 URL(面板不显示,已略过)。

- [x] **后台建榜 + 填 ID**:已填 `LeaderboardId = "a54n16ndiu1jn6h79j"`(`TapTapLeaderboardService.cs`)。
      ⚠ 仍需在后台确认该榜的**排序方式**(本游戏分数越大越好)与**统计周期**(常驻/每日/每周)配置正确。
      未配置真实 ID 时 `IsConfigured()` 守卫会拦下(提交回 false、拉取回"排行榜未配置"、原生界面回退自绘),现已配置故放行。
- [ ] **依赖登录**:排行榜读写要求 TapTap 登录已成功(`ILoginService.IsLoggedIn`),否则提交/拉取直接回失败、SDK 侧回 `500102 未登录`。

## 3. 必须真机验证(编辑器/PC 看不到正式数据)

| 环境 | 数据来源 |
|------|---------|
| Editor(开/不开宏都一样) | 模拟榜单(`BuildMockResult`) |
| PC 包 | "排行榜未接入"空榜(无 PC 桥接) |
| **Android / iOS 真机包** | **正式数据**(SubmitScores 上报 + LoadLeaderboardScores 拉取) |

- [ ] 出 **Android/iOS 真机包**(Android 宏已配;iOS 需确认 `ProjectSettings.asset` iOS 平台也加上 `TAPTAP_LEADERBOARD`)。
- [ ] 真机用真实账号登录 → 打一局结束 → 自动 `Submit` 本局总分 → 打开排行榜看真实榜单 + 自己排名。
- [ ] 确认 SDK 异步回调在主线程恢复(与登录/云存档同);若不是,回调里切回主线程再动 UI。

## 4. 功能完善(可选)

- [ ] **优先用 TapTap 内置榜单 UI**:`LeaderboardPanel.OnShow` 已先调 `TryOpenNative()`(真机+SDK+已配置 → 调起官方 UI 并关掉自绘面板);若想统一只用自绘列表,去掉这段即可。
- [ ] **好友榜**:`CollectionPublic` 现固定 "public"(全球榜)。要好友榜传 "friends",可在面板加页签切换 collection。
- [ ] **多分数/分段**:`SubmitScores` 一批最多 5 个分数,可同时上报多个排行榜(如总分榜 + 单局最高连杀榜)。
- [ ] **翻页**:`LoadLeaderboardScores` 返回 `nextPage`,现只取单页前 N。要超过单页上限,用 `nextPage` 续拉累积。
- [ ] **头像**:`Score.User.avatar`(`Image` 对象)如需显示,取其图片 URL/纹理接 `LeaderboardEntry.avatarUrl` 并在面板加头像位。
- [ ] **分享**:SDK 有 `SetShareCallback` / 分享截图能力,按需接。

## 5. 提交时机/口径(按玩法定)

- [ ] 现在每次对局结束都提交**本局总分**(TapTap 按"更优分数保留"聚合,无需客户端比最高分)。
      若要改成"累计总分""周榜清零"等口径,在 `GameMainPanel.ShowResult` 调整提交值,或后台改统计周期。
