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

## 2. 接入真实 TapTap SDK(SDK 已通过 UPM 接入,无需手动下包)

> ✅ **已用 UPM 接入**:`Packages/manifest.json` 已加好 scopedRegistries(TapTap→npmjs、EDM4U→OpenUPM)与依赖,
> **下次打开 Unity 会自动下载** `com.taptap.sdk.core/login/cloudsave@4.10.3` + `com.google.external-dependency-manager@1.2.179`。
> 无需登录 TapTap 控制台、无需手动找 `.unitypackage`。Newtonsoft 3.2.1 项目已有。

- [ ] **打开 Unity** 让 Package Manager 拉取上述包(首次约 10MB,需联网)。拉取后 EDM4U 可能提示开启 Android/iOS 依赖解析,按需同意。
- [ ] 若公司网络访问 npmjs/OpenUPM 受限:改用官方 unitypackage 离线导入(developer.taptap.cn 下载页),或配置内网镜像 registry。
- [x] **填凭证**:`TapTapLoginProvider.cs` 已填真实 `ClientId` / `ClientToken` / `clientPublicKey`(PC),`region=CN`,`screenOrientation=0`(竖屏)。
      ⚠ `serverSecret` 属服务端密钥,**未**写入客户端(只在后端校验令牌时用)。
- [x] **开宏**:已在 `ProjectSettings.asset` 的 Android 与 Standalone 加好 `TAPTAP_LOGIN;TAPTAP_CLOUDSAVE`(随 `DIRICHLET_AD` 之后)。
      ⚠ 开宏后 Editor 不再走"模拟登录",改用真实 SDK——**必须先填好凭证(下一条)再运行**,否则 `TapTapSDK.Init` 用占位值会报错。
- [ ] **校验 `TapTapAccount` 字段映射**:`TapTapLoginProvider.Convert` 目前映射 `unionId/openId/name/avatar`;
      若后端要校验令牌,补 `accessToken` 取值(从 `account.accessToken` 取所需字段)。
- [ ] 确认 SDK 异步回调在主线程恢复(Unity SDK 一般如此);若不是,回调里改用 `MainThreadDispatcher` 切回主线程再动 UI。

## 2b. 云存档(代码已就绪,待接 SDK 后验证)

> 代码:`ICloudSaveService` + `TapTapCloudSaveService`(SDK 调用在 `TAPTAP_CLOUDSAVE` 宏内,字段名已对照 4.10.3 校正)
> + `CloudArchivePacker`(纯本地打包/解包)+ `CloudSaveSyncService`(自动同步)。均已注册进 `GameContext`。
> 模型:**一个云归档 = 一个本地存档槽**,归档名 `slot_{槽id}`,打包该槽的 `slot{id}_*.json` 进度文件上传。

- [x] **校验 SDK 字段名**:已读 `Library/PackageCache/com.taptap.sdk.cloudsave@4.10.3` 源码,`ArchiveData` 字段平铺
      (`Uuid/FileId/Name/Summary/Playtime/ModifiedTime`),`Convert`/`GetName` 已改对,编译通过。
- [x] **自动同步(B 方案)**:`CloudSaveSyncService` 已接——登录后云端较新则自动下载;进度存档落盘后防抖(默认 3s)合并上传。
      钩子在 `StoreMgr.ProgressSaved` 事件(任意 Progress 存档触发)+ `ILoginService.LoggedIn` 事件。业务侧照常 `Save` 即可。
- [x] **同步仅 Android 真机启用**:注册被 `#if UNITY_ANDROID && !UNITY_EDITOR` 包裹(`GameProjectBootstrapper`)。
      原因:编辑器/PC 下 TapTap 云存档后端报 "Client ID 不存在"(PC 云存档需从 TapTap PC 客户端启动 + 后台单独开通)。
      编辑器/PC 不跑同步、不影响本地存档;上 Android 真机自动生效。要在 PC 也测云存档需另接 PC 平台云存档,暂不做。
- [ ] **调防抖窗口**:`CloudSaveSyncService.UploadDebounceSeconds`(现 3s)。想更即时调小,但别小到让一连串写盘各发一次请求(限频 60 次/分钟)。
- [ ] **补 `ArchiveMetadata.playtime`**:`UploadAsync` 现传 0,可接入真实游玩时长(用于云端展示)。
- [ ] **跨设备槽 id 不一致**:云归档名按"上传设备的槽 id"(`slot_{id}`)。换设备若本地新建的槽 id 与云端不同,
      登录自动下载可能匹配不上(当前按 `slotId` 严格匹配)。单槽游戏一般没问题;多槽需做云归档↔本地槽的映射/UI 选择。
- [ ] **时间戳单位**:`CloudSaveSyncService.NormalizeUnix` 假设云端 `ModifiedTime` 是秒(>1e12 当毫秒)。真机看一眼实际值,必要时校正。
- [ ] **(可选)云存档管理界面**:如需让玩家手动看/选/删云归档,在 `SaveSlotPanel` 加 `GetList`/`Download`/`Delete` 入口(builder 方式)。
- [ ] **限额/错误提示**:单归档 ≤10MB、≤100 归档/100MB、创建更新 ≤60 次/分钟。失败已 `Debug.LogWarning`;
      如需玩家可见提示,接 `ITapCloudSaveCallback.OnResult`(300001 需登录 / 300002 初始化失败)或上传失败回调。
- [ ] 真机验证:登录玩一会(自动上传)→ 换设备/重装 → 登录 → 自动下载还原进度。

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

- [x] **登出 UI**:设置界面 `SettingPanel` 已加「退出登录」按钮(确认弹窗 → `ILoginService.Logout()` → 关大厅、显示 `LoginPanel`)。
      ⚠ 改了 `SettingPanelBuilder`,需重跑 `Tools/UI/Build SettingPanel Prefab`(或 Build ALL)生成预制体,按钮才出现。
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
