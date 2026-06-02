namespace YOTO
{
    /// <summary>
    /// 登录渠道。当前仅接入 <see cref="TapTap"/>;后续接其它登录模块(游客/手机/微信等)时,
    /// 在此补一个枚举值 + 一个 <see cref="ILoginProvider"/> 实现即可,登录服务与界面无需大改。
    /// </summary>
    public enum LoginChannel
    {
        None = 0,
        TapTap = 1,
        // Guest = 2,
        // Phone = 3,
    }

    /// <summary>
    /// 登录账号(渠道无关的统一视图)。各渠道把自家用户信息映射到这里,
    /// 上层(界面/存档/上报)只认本类型,不感知具体渠道 SDK。
    /// </summary>
    public class LoginAccount
    {
        public LoginChannel channel;   // 来源渠道
        public string userId;          // 跨端稳定标识(TapTap = unionId),作为账号主键
        public string openId;          // 渠道内标识(TapTap = openId)
        public string name;            // 昵称
        public string avatarUrl;       // 头像 URL
        public string accessToken;     // 渠道访问令牌(需透传后端校验时用;可为空)
    }

    /// <summary>
    /// 登录结果。成功带 <see cref="account"/>;用户主动取消时 <see cref="canceled"/>=true(非错误,
    /// 界面通常静默或提示"已取消");其余失败 <see cref="error"/> 给原因。
    /// </summary>
    public struct LoginResult
    {
        public bool success;
        public bool canceled;          // 用户主动取消(区别于真正的失败)
        public string error;           // 失败原因(success=false 且 canceled=false 时有效)
        public LoginAccount account;   // success=true 时有效

        public static LoginResult Ok(LoginAccount account) => new LoginResult { success = true, account = account };
        public static LoginResult Cancel() => new LoginResult { canceled = true };
        public static LoginResult Fail(string error) => new LoginResult { error = error };
    }
}
