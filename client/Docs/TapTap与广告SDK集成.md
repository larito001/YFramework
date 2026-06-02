# TapTap 与广告 SDK 集成说明

> 本文汇总本项目 **TapTap(登录 / 云存档 / 排行榜)** 与 **Dirichlet(TapADN)激励广告** 的全部接入细节:架构、文件、编译宏、凭证、打包配置、签名注册、常见报错与待办。
> 设计原则:所有渠道 SDK 调用都包在**编译宏**内 + 提供**编辑器模拟 / 未接入回退**,未装 SDK 也能编译;框架层公开 API **不暴露 Task**,操作 fire-and-forget,结果走回调/事件。

---

## 0. 一图概览

| 模块 | 公开接口 | 实现 | 编译宏 | 启用平台 |
|------|----------|------|--------|----------|
| 登录 | `ILoginService` | `LoginManager` + `TapTapLoginProvider` | `TAPTAP_LOGIN` | Android + Standalone |
| 云存档 | `ICloudSaveService` | `TapTapCloudSaveService` (+`CloudSaveSyncService` 自动同步) | `TAPTAP_CLOUDSAVE` | Android + Standalone（同步仅 Android 真机） |
| 排行榜 | `ILeaderboardService` | `TapTapLeaderboardService` | `TAPTAP_LEADERBOARD` | Android（代码仅 Mobile）|
| 激励广告 | `IAdService` | `DirichletAdService` | `DIRICHLET_AD` | Android |

所有服务在 `GameProjectBootstrapper.RegisterProjectServices` 注册。

---

## 1. SDK 安装方式

### 1.1 TapTap（UPM,公开源,无需登录控制台）
`Packages/manifest.json` 已配置 scopedRegistries + 依赖,打开 Unity 自动下载:

```jsonc
"scopedRegistries": [
  { "name": "TapTap", "url": "https://registry.npmjs.org", "scopes": ["com.taptap"] },
  { "name": "package.openupm.com", "url": "https://package.openupm.com",
    "scopes": ["com.google.external-dependency-manager"] }
],
"dependencies": {
  "com.google.external-dependency-manager": "1.2.179",   // EDM4U,TapTap/广告解析 Android aar 用
  "com.taptap.sdk.core": "4.10.3",
  "com.taptap.sdk.login": "4.10.3",
  "com.taptap.sdk.cloudsave": "4.10.3",
  "com.taptap.sdk.leaderboard": "4.10.3",
  // Newtonsoft 3.2.1 项目已有
}
```
- 包源:`com.taptap.*` 在 npmjs;`com.google.external-dependency-manager` 在 OpenUPM。
- 包缓存在 `Library/PackageCache/com.taptap.sdk.*@4.10.3/`(只读,内含 `link.xml` 防裁剪,**勿删**)。

### 1.2 Dirichlet 广告（unitypackage,已解包进 Assets）
- 官方公开直链(无需登录):`https://tapad-platform.tapimg.com/sdk/unity/dirichlet_ad_unity_4.2.5.0.unitypackage`
- 已铺进:`Assets/DirichletAd/{Runtime,Editor}`、`Assets/Plugins/Android/DirichletAd/`(aar+java bridge)、`Assets/Plugins/iOS/`。
- 命名空间 `Dirichlet.Ad`;Runtime 是独立 asmdef `Dirichlet.Ad.Runtime`(autoReferenced)。
- ⚠ **Samples 已删**(`Assets/DirichletAd/Samples/`):demo 里的 `NetworkSecurityConfig.androidlib` 会让 AGP 回退到 build-tools 30.0.3,导致 Android 构建报"License not accepted"。**重新导包后务必再删一次 Samples。**

---

## 2. 编译宏总表（ProjectSettings → Player → Scripting Define Symbols）

| 宏 | Android | Standalone | 作用 |
|----|---------|-----------|------|
| `TAPTAP_LOGIN` | ✅ | ✅ | 启用真实 TapTap 登录 |
| `TAPTAP_CLOUDSAVE` | ✅ | ✅ | 启用真实 TapTap 云存档 |
| `TAPTAP_LEADERBOARD` | ✅ | — | 启用真实 TapTap 排行榜（SDK 仅 Mobile） |
| `DIRICHLET_AD` | ✅ | — | 启用真实 Dirichlet 广告（代码全 Android 门控） |

> 规律:**装了 SDK 才开对应宏**。没装就开宏 → 编译期 `CS0246`(找不到类型)。未开宏时:编辑器走模拟、真机走"未接入"回退。

---

## 3. 登录模块

**文件**(`Assets/Scripts/GamePlay/Login/`):`LoginTypes.cs`、`ILoginProvider.cs`、`ILoginService.cs`、`LoginManager.cs`、`TapTapLoginProvider.cs`;界面 `Assets/Scripts/GamePlay/UI/Login/LoginPanel.cs` + `Assets/Scripts/Editor/UI/LoginPanelBuilder.cs`。

**分层**:`ILoginService`(公开契约,聚合)→ `LoginManager`(IGameService,持当前账号、`LoggedIn` 事件)→ `ILoginProvider`(每渠道一份)→ `TapTapLoginProvider`。

**登录门**:`GameBootstrapper.RunProjectStartup` 读档后 → `GateLoginThenLobby`:
- `TryAutoLogin` 静默成功 → 直接进大厅;
- 失败 → 收 Loading、弹 `LoginPanel`,用户点「TapTap 登录」成功后 `Show<StartPanel>()` 进大厅。

**TapTap API**:`TapTapSDK.Init(TapTapSdkOptions)` + `TapTapLogin.Instance.LoginWithScopes(string[])` / `GetCurrentTapAccount()` / `Logout()`;账号 `TapTapAccount`(`unionId`/`openId`/`name`/`avatar`)。

**登出**:设置界面 `SettingPanel` 有「退出登录」按钮 → 确认弹窗 → `ILoginService.Logout()` → 回 `LoginPanel`。（改了 builder,需重跑 `Tools/UI/Build SettingPanel Prefab`）

**加新登录渠道**:加 `LoginChannel` 枚举 + 一个 `ILoginProvider` 实现 + bootstrapper 里 `login.AddProvider(...)` 一行 + builder 加按钮。`LoginManager`/登录门/`ILoginService` 不动。

---

## 4. 云存档模块

**文件**(`Assets/Scripts/GamePlay/Login/`):`CloudSaveTypes.cs`、`ICloudSaveService.cs`、`CloudArchivePacker.cs`(纯本地打包,无 SDK 也编译)、`TapTapCloudSaveService.cs`、`CloudSaveSyncService.cs`(自动同步)。

**模型**:一个云归档 = 一个本地存档槽,归档名 `slot_{槽id}`,打包该槽的 `persistentDataPath/slot{id}_*.json` 进度文件上传;下载写回文件 → `SetActiveSlot` → `LoadAll` 还原。

**TapTap API（4.10.3,字段已对齐源码）**:`TapTapCloudSave.CreateArchive/UpdateArchive/DeleteArchive/GetArchiveList/GetArchiveData`;`ArchiveData` 字段**平铺**:`Uuid`/`FileId`/`Name`/`Summary`/`Extra`/`Playtime`/`ModifiedTime`(无 `.Metadata` 子对象);`ArchiveMetadata(name, summary, extra, playtime)`。

**自动同步(B 方案)** —— `CloudSaveSyncService`,业务侧照常 `Save` 即可,无需感知云端:
- 钩子:`StoreMgr.ProgressSaved` 事件(任意 Progress 存档落盘后触发)+ `ILoginService.LoggedIn` 事件。
- 登录成功 → 拉云端列表,当前槽云归档比本地新 → 自动下载还原。
- 进度落盘 → 防抖合并(`UploadDebounceSeconds = 3s`)后上传(防 TapTap 限频 60 次/分钟);`uploading` 标记防上传内部 SaveAll 自触发死循环。
- **仅 Android 真机注册**:`#if UNITY_ANDROID && !UNITY_EDITOR`(见 bootstrapper)。原因见下。

**⚠ PC/编辑器用不了云存档**:Standalone 云存档要求"从 TapTap PC 客户端启动"(`CheckPCLaunchState`/`isLaunchedFromTapTapPC`),且 PC 后端不认移动应用 → 报 **`Client ID 不存在`**。故同步只在 Android 真机跑;本地存档不受影响。

**前置**:云存档服务需在 TapTap 后台**单独开通**;依赖登录。

---

## 5. 排行榜模块

**文件**:`Assets/Scripts/GamePlay/Leaderboard/{ILeaderboardService,TapTapLeaderboardService}.cs`;界面 `Assets/Scripts/GamePlay/UI/Leaderboard/LeaderboardPanel.cs` + `Assets/Scripts/Editor/UI/LeaderboardPanelBuilder.cs`;入口在主界面「排行榜」按钮。

**接口**:`Submit(long score, cb)` / `LoadTop(count, cb)` / `TryOpenNative()`(调起 TapTap 原生榜单 UI,不可用则用自绘 `LeaderboardPanel` 兜底)。提交时机:对局结束(`GameMainPanel.ShowResult`)。

**平台**:TapTap 排行榜 SDK **仅 Mobile(Android/iOS)**,无 Standalone 桥接。编辑器走"模拟榜单";真机 + `TAPTAP_LEADERBOARD` 走真实;PC 回退空榜。

**配置**:`TapTapLeaderboardService.LeaderboardId`(现为占位 `"default"`)→ 替换为 TapTap 后台「游戏服务 → 排行榜」创建后分配的真实 ID。

---

## 6. 激励广告模块（Dirichlet / TapADN）

**文件**:`Assets/Scripts/GamePlay/Ad/{IAdService,DirichletAdService}.cs`;大厅「体力广告补充」按钮调用。

**4.2.5.0 API 是"加载-展示两步"**(repo 原代码曾照老 API 写,已改对):
```
DirichletAdSdk.Init(config, onSuccess, onFailure);     // config 用 DirichletAdConfig.Builder
var adNative = DirichletAdManager.CreateAdNative();
var req = new DirichletAdRequest.Builder().WithSpaceId(long).Build();   // SpaceId 是 long
adNative.LoadRewardVideoAd(req, onLoaded: ad => { ad.SetInteractionListener(listener); ad.Show(); }, onFailure: err => {...});
// listener: IDirichletRewardAdInteractionListener(OnAdShow/OnAdClick/OnAdClose/OnRewardVerify),无 OnError
// 发奖以 OnRewardVerify(args).IsVerified 为准,OnAdClose 结算
```

**平台**:所有 SDK 调用 `#if DIRICHLET_AD && UNITY_ANDROID && !UNITY_EDITOR`。编辑器模拟看完发奖、其它平台不发奖。

**凭证**(`DirichletAdService` 顶部,**当前为官方测试值,上线必换**):`MediaId`/`MediaKey`/`RewardSpaceId`/`GameChannel`/`SubChannel`/`TapClientId`。

---

## 7. 凭证清单（TapTap 开发者后台 → 应用 → 凭证管理）

| 凭证 | 值 | 用在哪 |
|------|----|--------|
| Client ID | `eisjc5cksbhhxis6hz` | `TapTapLoginProvider.ClientId` |
| Client Token | `E9qVj24UioVvlsKmpNmKYSeIuVwEnyOnbWeD8F56` | `TapTapLoginProvider.ClientToken` |
| Client Public Key | `MIIBIjAN...AQAB`(完整见代码) | `TapTapLoginProvider.ClientPublicKey`(仅 PC 登录用） |
| region | `CN` | `TapTapSdkOptions.region` |
| **serverSecret** | `tqWw1r6eD4yY0icfMmCd6MJEyuVqyEJ8` | **⚠ 仅后端校验令牌用,严禁写进客户端** |

---

## 8. Android 打包配置

### 8.1 Player Settings（已配,`ProjectSettings.asset`）
| 项 | 值 |
|----|----|
| Company Name | 火车工作室 |
| Product Name | 打猎模拟器 |
| Package Name (applicationId) | **`com.yoto.dalie`**（`overrideDefaultApplicationIdentifier=1`,因中文名不能自动拼包名） |
| Version / Code | 1.0.0 / 1 |
| 屏幕方向 | 锁定竖屏（`defaultScreenOrientation=0`） |
| Min SDK | 22 |
| 图标 | 暂默认(待补 PNG) |

### 8.2 Gradle 国内镜像（`Assets/Plugins/Android/settingsTemplate.gradle`）
`dl.google.com` 下 AGP 超时,已在 `pluginManagement` 与 `dependencyResolutionManagement` 仓库块**最前**加 aliyun 镜像(官方源保留兜底,且在 EDM4U 管理区之外,不会被覆盖):
```
maven { url 'https://maven.aliyun.com/repository/gradle-plugin' }
maven { url 'https://maven.aliyun.com/repository/google' }
maven { url 'https://maven.aliyun.com/repository/public' }
```

### 8.3 AndroidX / Jetifier（`Assets/Plugins/Android/gradleTemplate.properties`）
EDM4U 已自动写入(Dirichlet 依赖老 support 库,靠 Jetifier 转 AndroidX):
```
android.useAndroidX=true
android.enableJetifier=true
```

### 8.4 依赖（EDM4U 自动写入 `mainTemplate.gradle`）
TapTap 四个 `com.taptap.sdk:tap-*-unity:4.10.3` + Dirichlet 的 okhttp/glide/support-v* + androidx。

---

## 9. 真机签名注册（解决"包名、签名错误")

TapTap 真机登录校验 **包名 + 签名 MD5**。在 **TapTap 开发者中心 → 游戏 → 游戏服务 → 应用配置** 填:
- **包名**:`com.yoto.dalie`
- **Android 签名(MD5)**:见下

**当前 debug 签名**(未配自定义 keystore,用 `~/.android/debug.keystore`):
- MD5:`DEE4FFBE8871759101CE4A533722D585`
- SHA1:`21:2E:50:5E:C5:A2:78:35:66:44:F1:93:E4:D3:C9:C6:8F:36:9B:9F`

取 MD5 命令(JDK 11+ keytool 不再默认打印 MD5,故导出证书算):
```bash
keytool -exportcert -keystore <ks> -alias <alias> -storepass <pwd> -file cert.der
md5sum cert.der    # 去冒号大写即 TapTap 要的签名 MD5
```

⚠ **debug 签名是每台机器一份**,换机/正式发布签名会变 → 登录失败。正式发布须建 **release keystore**、用它签名、把它的 MD5 也登记到后台。

---

## 10. 待办 / 未完成

- [ ] **TapTap 后台登记包名 `com.yoto.dalie` + 签名 MD5**（当前登录报"包名、签名错误"的直接原因）。
- [ ] **云存档服务在后台开通**;只能 Android 真机验证(PC/编辑器不可用)。
- [ ] **排行榜真实 ID**:替换 `TapTapLeaderboardService.LeaderboardId` 占位 `"default"`。
- [ ] **广告正式凭证**:`DirichletAdService` 顶部测试值换成 Dirichlet 后台正式 MediaId/MediaKey/SpaceId。
- [ ] **release keystore**:Publishing Settings 配置 + 登记其 MD5 到 TapTap 后台。
- [ ] **防沉迷 / 合规认证**(anti-addiction):尚未接入。文档 https://developer.taptap.cn/docs/sdk/anti-addiction/practice/ 。登录界面右上「用户中心」入口也属此模块。
- [ ] **重建受影响预制体**(改过 builder):`LoginPanel`(标题显示产品名)、`SettingPanel`(退出登录按钮)、`LeaderboardPanel` → `Tools/UI/Build ALL UI Prefabs`。
- [ ] **应用图标**:补 PNG,配 Android 自适应图标。
- [ ] **版号 / 出版信息**:`LoginPanel` 底部合规文本(著作权人/ISBN/出版单位)上线前补全。
- [ ] **Unity 启动 Logo**:Personal 版强制显示,Pro 可关。

---

## 11. 常见报错与排查

| 现象 | 原因 | 解决 |
|------|------|------|
| 编译 `CS0246: 找不到类型`(TapSDK/Dirichlet) | 开了宏但没装 SDK / SDK API 与代码不匹配 | 装 SDK;核对 SDK 实际类型(去 `Library/PackageCache` 读源码) |
| 真机登录 `TapSDK: 包名、签名错误` | 后台未登记包名+签名,或签名不符 | 登记 `com.yoto.dalie` + 签名 MD5(见 §9) |
| 云存档 `Client ID 不存在` | PC/编辑器不支持 / 后台未开通云存档 | 云存档只在 Android 真机测;后台开通服务 |
| Gradle `Read timed out`(dl.google.com) | 国内访问 Google Maven 超时 | aliyun 镜像(§8.2),已配 |
| Gradle `build-tools;30.0.3 License not accepted` | Dirichlet Samples 的 androidlib 拉旧 build-tools | 删 `Assets/DirichletAd/Samples/`(§1.2) |
| Shader `undeclared identifier 'LerpWhiteTo'`(移动端) | URP ShadowCaster 缺 `CommonMaterial.hlsl` | `AnimalGold.shader` ShadowCaster 已补该 include |
| duplicate class / AndroidX 冲突 | 老 support 库与 AndroidX 混用 | Jetifier(§8.3),EDM4U 已开 |
| iOS resolver DLL 警告(`UnityEditor.iOS.Extensions.Xcode`) | 未装 iOS Build Support 模块 | 不出 iOS 可忽略;要出 iOS 则 Unity Hub 装 iOS 模块 |

---

*相关 Builder 风格、存档分槽等见代码注释与 CLAUDE 记忆库。本文档随集成推进更新。*
