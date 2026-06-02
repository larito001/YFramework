using System;
using UnityEngine;
#if TAPTAP_LOGIN
using System.Threading.Tasks;
using TapSDK.Core;
using TapSDK.Login;
#endif

namespace YOTO
{
    /// <summary>
    /// TapTap 登录渠道接入(实现 <see cref="ILoginProvider"/>)。SDK 文档见
    /// https://developer.taptap.cn/docs/sdk/taptap-login/features/ 与 .../guide/。
    ///
    /// **编译开关**:所有 TapTap SDK 调用都包在 <c>TAPTAP_LOGIN</c> 宏内——未导入 SDK、未在
    /// Player Settings 的 Scripting Define Symbols 里加 <c>TAPTAP_LOGIN</c> 之前,项目照常编译:
    ///   · Editor 走"模拟登录成功"便于联调进大厅链路;
    ///   · 真机(未启用宏)走"未接入"回退,登录失败并提示。
    /// 接入步骤见文件末尾注释。
    ///
    /// **契约映射**(框架风格:操作 fire-and-forget,结果走回调):
    ///   Login → LoginWithScopes;取消(TaskCanceledException)→ <see cref="LoginResult.Cancel"/>;
    ///   TryRestore → GetCurrentTapAccount(无账号视为未登录,不弹界面);Logout → SDK Logout。
    /// </summary>
    public class TapTapLoginProvider : ILoginProvider
    {
        // ── 凭证(TapTap 开发者后台 → 应用 → 凭证管理)──
        // ⚠ serverSecret(服务端密钥)绝不能写进客户端,只在后端校验令牌时用,故不在此出现。
        private const string ClientId = "eisjc5cksbhhxis6hz";
        private const string ClientToken = "E9qVj24UioVvlsKmpNmKYSeIuVwEnyOnbWeD8F56";
        // PC 客户端公钥(仅 PC/Standalone 登录需要;移动端可忽略)。
        private const string ClientPublicKey =
            "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA1WjQMTkVC32rSx6/7aKjUFCvq8GiceZu0dmYIniQGscn5F/VDa0vsxx2gE4NLbXm2DiwZEhZL5YeMnvq0ty09PeNf8DZqbGELigpon6qwkoHHz83XrajuuoUultfx+o81aYUY4zpqla6AaTy0txPnfwZ/YcKQpbk4vqozFB7H2JpeqxvrrqfzXutJZQNj21To1m5f0H+oJiTG/83FvqegLfnReI/sRAgctMVbCzEjTHddDkYfHnhwoxj7A2MwFqbjYpD1RouXJ2ZhA5N+uDF7E6NpNeVzMmmhVa1MHrs2l76qEcEPqyI9h0n1EIvgppGurB1MRZMTeRK5GA8y3+VgQIDAQAB";

        public LoginChannel Channel => LoginChannel.TapTap;

        public void Init(GameContext ctx)
        {
#if TAPTAP_LOGIN
            var options = new TapTapSdkOptions
            {
                clientId = ClientId,
                clientToken = ClientToken,
                clientPublicKey = ClientPublicKey, // 仅 PC/Standalone 登录用;移动端无影响
                region = TapTapRegionType.CN,      // 国内版;海外用 TapTapRegionType.Overseas
                enableLog = true,                  // 联调期开日志,发布前改 false
                screenOrientation = 0,             // 0=竖屏(本游戏为竖屏);横屏填 1
            };
            TapTapSDK.Init(options);
#endif
        }

        public void Login(Action<LoginResult> onComplete)
        {
#if TAPTAP_LOGIN
            LoginAsync(onComplete);
#elif UNITY_EDITOR
            // 编辑器无 SDK:模拟登录成功,便于联调"登录 → 进大厅"链路与登录界面。
            Debug.Log("[Login] (编辑器模拟) TapTap 登录成功。");
            onComplete?.Invoke(LoginResult.Ok(MockAccount()));
#else
            Debug.LogWarning("[Login] 未启用 TAPTAP_LOGIN 宏 / 未导入 TapTap SDK,无法登录。");
            onComplete?.Invoke(LoginResult.Fail("TapTap SDK 未接入"));
#endif
        }

        public void Logout()
        {
#if TAPTAP_LOGIN
            TapTapLogin.Instance.Logout();
#endif
        }

        public void TryRestore(Action<LoginResult> onComplete)
        {
#if TAPTAP_LOGIN
            RestoreAsync(onComplete);
#else
            // 编辑器/未接入:不做静默登录,直接回未登录,使每次启动都能看到登录界面以便联调。
            onComplete?.Invoke(LoginResult.Fail("无 TapTap 会话"));
#endif
        }

#if TAPTAP_LOGIN
        // 授权范围:基础档案(昵称/头像)。如需更多权限,按文档追加 scope。
        private static readonly string[] Scopes = { TapTapLogin.TAP_LOGIN_SCOPE_PUBLIC_PROFILE };

        // async void:把 SDK 的 Task API 收进内部,对外仍是 fire-and-forget + 回调(不泄漏 Task)。
        private async void LoginAsync(Action<LoginResult> onComplete)
        {
            try
            {
                TapTapAccount account = await TapTapLogin.Instance.LoginWithScopes(Scopes);
                onComplete?.Invoke(LoginResult.Ok(Convert(account)));
            }
            catch (TaskCanceledException)
            {
                onComplete?.Invoke(LoginResult.Cancel()); // 用户主动取消
            }
            catch (Exception e)
            {
                Debug.LogError($"[Login] TapTap 登录失败: {e}");
                onComplete?.Invoke(LoginResult.Fail(e.Message));
            }
        }

        private async void RestoreAsync(Action<LoginResult> onComplete)
        {
            try
            {
                TapTapAccount account = await TapTapLogin.Instance.GetCurrentTapAccount();
                if (account == null) onComplete?.Invoke(LoginResult.Fail("无 TapTap 会话"));
                else onComplete?.Invoke(LoginResult.Ok(Convert(account)));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Login] TapTap 会话恢复失败: {e.Message}");
                onComplete?.Invoke(LoginResult.Fail(e.Message));
            }
        }

        /// <summary>把 TapTap 账号映射为渠道无关的 <see cref="LoginAccount"/>。
        /// accessToken 若需透传后端校验,从 account.accessToken 自行取所需字段填入。</summary>
        private static LoginAccount Convert(TapTapAccount account) => new LoginAccount
        {
            channel = LoginChannel.TapTap,
            userId = account.unionId,
            openId = account.openId,
            name = account.name,
            avatarUrl = account.avatar,
        };
#endif

#if UNITY_EDITOR && !TAPTAP_LOGIN
        private static LoginAccount MockAccount() => new LoginAccount
        {
            channel = LoginChannel.TapTap,
            userId = "editor_mock_unionid",
            openId = "editor_mock_openid",
            name = "编辑器测试账号",
        };
#endif
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// TapTap 登录接入步骤(代码侧已就绪,以下为工程/打包侧手动操作):
//
// 1. 导入 SDK:按官方《Unity 集成指南》用 UPM(git/OpenUPM)或导入 .unitypackage,
//    至少需要 TapSDK Core 与 TapSDK Login 两个模块。
//    文档:https://developer.taptap.cn/docs/sdk/integration-guides/unity/
// 2. 填凭证:把本文件顶部的 ClientId / ClientToken 换成开发者后台分配的真实值,
//    并确认 region(国内 CN / 海外 Overseas)。
// 3. 开启编译宏:Project Settings → Player → Other Settings → Scripting Define Symbols
//    添加 TAPTAP_LOGIN(此后走真实 SDK;Editor 仍可用模拟分支联调,真机走真实登录)。
// 4. 平台配置:
//    - Android:合入 SDK 文档要求的 Gradle 依赖与权限(INTERNET 等),配置签名。
//    - iOS:在 Info.plist 配置 URL Scheme / LSApplicationQueriesSchemes(见 iOS 集成指南)。
// 5. 注册已在 GameBootstrapper.RegisterProjectServices 接好(LoginManager.AddProvider)。
// ─────────────────────────────────────────────────────────────────────────────
