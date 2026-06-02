using System;
using System.Collections.Generic;
using UnityEngine;

namespace YOTO
{
    /// <summary>
    /// 登录服务实现:聚合多个 <see cref="ILoginProvider"/>,按渠道分发登录/登出/会话恢复,并持有当前账号。
    /// 由 <see cref="GameBootstrapper.RegisterProjectServices"/> 构造时 <see cref="AddProvider"/> 接入各渠道,
    /// 以 <see cref="ILoginService"/> 注册进 <see cref="GameContext"/>。
    ///
    /// **接新登录模块**:新增一个 <see cref="ILoginProvider"/> 实现 + 在注册处多一行 <see cref="AddProvider"/> 即可,
    /// 本类与界面逻辑无需改动(界面按 <see cref="AvailableChannels"/> 自动决定按钮)。
    /// </summary>
    public class LoginManager : ILoginService, IGameService
    {
        private readonly List<ILoginProvider> providers = new List<ILoginProvider>();
        private readonly List<LoginChannel> channels = new List<LoginChannel>();
        private LoginAccount current;

        public bool IsLoggedIn => current != null;
        public LoginAccount Current => current;
        public IReadOnlyList<LoginChannel> AvailableChannels => channels;

        /// <summary>接入一个登录渠道(须在 <see cref="Init"/> 之前调用,即 bootstrapper 构造期)。返回 this 便于链式。</summary>
        public LoginManager AddProvider(ILoginProvider provider)
        {
            if (provider == null) return this;
            if (Find(provider.Channel) != null)
            {
                Debug.LogWarning($"[LoginManager] 渠道 {provider.Channel} 已接入,忽略重复 AddProvider。");
                return this;
            }
            providers.Add(provider);
            channels.Add(provider.Channel);
            return this;
        }

        public void Init(GameContext ctx)
        {
            for (int i = 0; i < providers.Count; i++) providers[i].Init(ctx);
        }

        public void Shutdown()
        {
            current = null;
            providers.Clear();
            channels.Clear();
        }

        public void Login(LoginChannel channel, Action<LoginResult> onComplete)
        {
            var provider = Find(channel);
            if (provider == null)
            {
                onComplete?.Invoke(LoginResult.Fail($"未接入的登录渠道: {channel}"));
                return;
            }
            provider.Login(res =>
            {
                if (res.success) current = res.account;
                onComplete?.Invoke(res);
            });
        }

        public void Logout()
        {
            if (current != null) Find(current.channel)?.Logout();
            current = null;
        }

        public void TryAutoLogin(Action<LoginResult> onComplete)
        {
            TryRestoreFrom(0, onComplete);
        }

        /// <summary>从第 i 个渠道开始尝试会话恢复:成功即采用并回调;失败则递归试下一个;全部失败回非成功。</summary>
        private void TryRestoreFrom(int i, Action<LoginResult> onComplete)
        {
            if (i >= providers.Count)
            {
                onComplete?.Invoke(LoginResult.Fail("无可恢复的登录会话"));
                return;
            }
            providers[i].TryRestore(res =>
            {
                if (res.success)
                {
                    current = res.account;
                    onComplete?.Invoke(res);
                }
                else
                {
                    TryRestoreFrom(i + 1, onComplete);
                }
            });
        }

        private ILoginProvider Find(LoginChannel channel)
        {
            for (int i = 0; i < providers.Count; i++)
                if (providers[i].Channel == channel) return providers[i];
            return null;
        }
    }
}
