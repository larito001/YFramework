# TapTap 登录 — 后续待办

> 现状:登录服务分层(`ILoginService`/`LoginManager`/`ILoginProvider`/`TapTapLoginProvider`)、登录界面(`LoginPanel` + `LoginPanelBuilder`)、启动登录门(`GameBootstrapper.GateLoginThenLobby`)代码已就绪。
> 所有 TapTap SDK 调用都在 `TAPTAP_LOGIN` 编译宏内——**未装 SDK 也能编译**:Editor 走"模拟登录成功"、真机(未开宏)走"未接入"回退。
> 下列为把它跑成"真登录 + 可上线"还需要做的事。

---

## 1. 立即要做(否则界面跑不起来)

- [ ] **生成登录界面预制体**:Unity 菜单 `Tools/UI/Build LoginPanel Prefab`(或 `Tools/UI/Build ALL UI Prefabs`)。
      不生成则启动时 `Show<LoginPanel>` 找不到 `Resources/UI/Login/LoginPanel.prefab`。
- [ ] **在 Editor 里编译一次**确认无报错(本次改动未经编译器验证)。
- [ ] Editor 联调:启动 → 弹登录界面 → 点「TapTap 登录」→(模拟成功)进大厅。确认链路通。

## 2. 接入真实 TapTap SDK(三个 unitypackage:本机当前都没有,需先下载)

> 经检查,`TapSDK_Core / TapSDK_Login / TapSDK_CloudSave` 这三个 `.unitypackage` 本机不存在,需先从 TapTap 控制台下载到本地。

- [ ] **下载并导入 SDK**(Assets → Import Package → Custom Package),三个模块:
      `TapSDK_Core.unitypackage`(核心,必选)、`TapSDK_Login.unitypackage`(登录,必选)、`TapSDK_CloudSave.unitypackage`(云存档,必选)。
      或按《Unity 集成指南》用 UPM:https://developer.taptap.cn/docs/sdk/integration-guides/unity/
- [ ] **填凭证**:`TapTapLoginProvider.cs` 顶部的 `ClientId` / `ClientToken` 换成开发者后台真实值,确认 `region`(国内 `CN` / 海外 `Overseas`)。
- [ ] **开宏**:Project Settings → Player → Other Settings → Scripting Define Symbols 添加:
      `TAPTAP_LOGIN`(开启真实登录)、`TAPTAP_CLOUDSAVE`(开启真实云存档)。两者独立,可分别开。
- [ ] **校验 `TapTapAccount` 字段映射**:`TapTapLoginProvider.Convert` 目前映射 `unionId/openId/name/avatar`;
      若后端要校验令牌,补 `accessToken` 取值(从 `account.accessToken` 取所需字段)。
- [ ] 确认 SDK 异步回调在主线程恢复(Unity SDK 一般如此);若不是,回调里改用 `MainThreadDispatcher` 切回主线程再动 UI。

## 2b. 云存档(代码已就绪,待接 SDK 后验证)

> 代码:`ICloudSaveService` + `TapTapCloudSaveService`(SDK 调用在 `TAPTAP_CLOUDSAVE` 宏内)+ `CloudArchivePacker`(纯本地打包/解包,已可编译)。已注册进 `GameContext`。
> 模型:**一个云归档 = 一个本地存档槽**,归档名 `slot_{槽id}`,打包该槽的 `slot{id}_*.json` 进度文件上传。

- [ ] **校验 SDK 字段名**:`TapTapCloudSaveService` 里 `GetUuid/GetName/GetFileId/Convert` 用的 `ArchiveData.Uuid /
      .Metadata.ArchiveName / .FileId / .ArchiveSummary` 等是按文档推断的,导入 SDK 后对照实际类型校正(代码内已用 ⚠ 标注)。
- [ ] **补 `ArchiveMetadata` 参数**:`UploadAsync` 里 playtime 现传 0,可接入真实游玩时长;`savedUnix` 若 SDK 有更新时间字段则在 `Convert` 填入(用于本地/云"谁更新"对比)。
- [ ] **触发时机**(产品决策,当前只提供 API、未自动触发):
      自动存档游戏建议在关键存档点调 `Upload`;手动存档游戏给"上传/下载"按钮。
- [ ] **本地 vs 云端冲突策略**:下载覆盖本地前应比对 `savedUnix`(或版本号),提示玩家选择,避免误覆盖更新的进度。
- [ ] **云存档界面**:在读档界面(`SaveSlotPanel`)或设置界面加云归档列表(`GetList`)+ 上传/下载/删除按钮,
      用 `RewardClaimPanel`/`ConfirmPanel` 同款 builder 方式新建面板。
- [ ] **限额处理**:单归档 ≤10MB、单封面 ≤512KB、每游戏每玩家 ≤100 归档/100MB、创建更新 ≤60 次/分钟——
      `Upload` 失败回调里按错误码提示(`ITapCloudSaveCallback.OnResult`:300001 需登录 / 300002 初始化失败)。
- [ ] 真机验证:登录后上传 → 换设备/重装 → 登录 → `GetList` → `Download` 还原进度。

## 3. 平台打包配置

- [ ] **Android**:合入 SDK 文档要求的 Gradle 依赖与权限(INTERNET 等),配置签名(包名/签名需与后台一致,否则登录失败)。
- [ ] **iOS**:`Info.plist` 配置 URL Scheme / `LSApplicationQueriesSchemes`(见 iOS 集成指南),用于唤起 TapTap 客户端授权。
- [ ] **PC**(如发 Steam/PC):需要 `clientPublicKey`,且 PC 登录走扫码/WebView,确认对应流程。
- [ ] 真机分别验证:已装 TapTap 客户端(快速授权)/ 未装(WebView 兜底)两条路径。

## 4. 合规(上线必需,当前未做)

- [ ] **用户中心 / 防沉迷(实名+未成年限制)**:设计图右上角「用户中心」属 TapTap **合规认证**独立模块,需单独接入。
      接入后在 `LoginPanel` 右上补「用户中心」入口,登录成功后触发合规校验。
- [ ] **版号 / 出版信息**:`LoginPanelBuilder` 里 `Copyright` 文本目前是占位,补全 著作权人 / ISBN / 出版单位 / 备案号。
- [ ] 适龄提示徽标:确认 `12+` 与实际定级一致。
- [ ] 隐私政策 / 用户协议:登录前一般需展示同意入口(按渠道/合规要求)。

## 5. 功能完善

- [ ] **登出 UI**:`ILoginService.Logout()` 已实现,但还没有界面入口(建议放设置界面 `SettingPanel`),登出后回到 `LoginPanel`。
- [ ] **账号态存档**:当前存档按设备本地(`StoreMgr` 分槽),与登录账号无关。
      如需"换账号换档/云存档",用 `LoginAccount.userId` 作为槽前缀或后端存档 key(见 `reference_storemgr_active_slot_boot`)。
- [ ] **失败/取消的用户提示**:`LoginPanel.statusText` 已显示"登录失败/已取消";按需替换为更友好的提示或 FlyText/弹窗。
- [ ] **网络异常重试**:无网/超时的提示与重试入口。
- [ ] **静默登录时机**:目前在读档后才 `TryAutoLogin`;若想登录更靠前(进度按账号隔离),需调整启动顺序。

## 6. 美术 / 体验

- [ ] 登录界面背景:`LoginPanelBuilder` 现用纯色底,替换为正式美术背景图(改 `bg.sprite`)。
- [ ] TapTap 登录按钮:现复用通用按钮 + 文案,按 TapTap **品牌规范**替换为带 logo 的标准按钮样式。
- [ ] 标题:现用 `PlayerSettings.productName`,确认即最终游戏名;如要 logo 图替换文本。

## 7. 扩展其它登录渠道(架构已兼容)

加一个新渠道的步骤(对上层透明):
1. [ ] `LoginTypes.LoginChannel` 加枚举值(如 `Guest`/`Phone`/`WeChat`)。
2. [ ] 新增一个 `ILoginProvider` 实现(参考 `TapTapLoginProvider`,SDK 调用同样用编译宏包裹)。
3. [ ] `GameBootstrapper.RegisterProjectServices` 里 `login.AddProvider(new XxxProvider())` 一行。
4. [ ] `LoginPanelBuilder` 复制一份按钮、改 name/文案;`LoginPanel` 接好字段与点击(调 `login.Login(LoginChannel.Xxx, ...)`)。
   > `LoginManager` / `GateLoginThenLobby` / `ILoginService` **无需改动**。
