using System;
using UnityEngine;
#if DIRICHLET_AD && UNITY_ANDROID && !UNITY_EDITOR
using Dirichlet.Ad;
#endif

namespace YOTO
{
    /// <summary>
    /// Dirichlet(TapADN / TapTap 广告)激励视频接入,实现框架预留的 <see cref="IAdService"/>。
    /// 由 <see cref="GameBootstrapper.RegisterProjectServices"/> 注册;注册后大厅「体力补充」按钮即生效。
    ///
    /// **编译开关**:所有 SDK 调用都包在 <c>DIRICHLET_AD</c> 宏内——未导入 SDK(.unitypackage)、未在
    /// Android Player Settings 的 Scripting Define Symbols 里加 <c>DIRICHLET_AD</c> 之前,项目照常编译,
    /// 只是走"模拟/不可用"分支。接入步骤见文件末尾注释。
    ///
    /// **平台**:激励视频 auto-ad 仅 Android。Editor 走"模拟看完发奖"便于联调;其余平台(PC/Steam)
    /// 不会注册本服务(见注册处),<see cref="StartPanel"/> 自动走"广告未接入"回退、不发奖。
    ///
    /// **契约映射**(框架风格:操作 fire-and-forget,结果走回调):
    ///   OnRewardVerify(IsVerified) → 记下应否发奖;OnAdClose → 回调 onClosed(rewarded);OnError → onClosed(false)。
    ///   每次 ShowRewardedAd 用独立 listener,内部一次性 guard 保证 onClosed 恰好回调一次。
    /// </summary>
    public class DirichletAdService : IAdService, IGameService
    {
        // ── 媒体凭证 / 广告位 ──
        // ⚠ 当前为 Dirichlet 官方【测试】凭证(取自 SDK Demo),仅供真机联调拉测试广告,不计收益、不可上线。
        //   上线前务必换成自己在 Dirichlet 媒体管理后台(ssp.dirichlet.cn)申请的正式值:
        //   新建媒体 → 得 MediaId + MediaKey;媒体下新建激励视频推广位 → 得 SpaceId。
        private const long MediaId = 1000007L;            // 测试媒体 ID(Android 联盟正式-测试)
        private const string MediaKey = "1AjDOjD0F3SDDmgTuBQHbCRULSizYPHV17viZObHvhDjf7Pq1rlarueOX1cYBucn"; // 测试媒体密钥
        private const string RewardSpaceId = "1001253";   // 测试激励视频广告位 ID(SpaceId)

        // 渠道凭证(TapTap 接入需要;测试凭证配套值,正式接入按后台/渠道配置替换)。
        private const string GameChannel = "taptap2";
        private const string SubChannel = "release";
        private const string TapClientId = "0RiAlMny7jiz086FaU";

#if DIRICHLET_AD && UNITY_ANDROID && !UNITY_EDITOR
        private DirichletAdNative _adNative;
        private bool _showing; // 同一时刻只放一条,避免并发请求
#endif

        public void Init(GameContext ctx)
        {
#if DIRICHLET_AD && UNITY_ANDROID && !UNITY_EDITOR
            var config = new DirichletAdConfig.Builder()
                .WithMediaId(MediaId)
                .WithMediaKey(MediaKey)
                .WithMediaName("YFramework")
                .WithGameChannel(GameChannel)
                .WithSubChannel(SubChannel)
                .WithTapClientId(TapClientId)
                .EnableDebug(true) // 联调期开日志,发布前改回 false
                .Build();

            DirichletAdSdk.Init(
                config,
                onSuccess: result => Debug.Log($"[Ad] Dirichlet 初始化成功: {result.Message}"),
                onFailure: error => Debug.LogError($"[Ad] Dirichlet 初始化失败: {error.Code} / {error.Message}"));

            DirichletAdSdk.RequestPermissionIfNecessary();
            _adNative = DirichletAdManager.CreateAdNative();
#endif
        }

        public void Shutdown()
        {
#if DIRICHLET_AD && UNITY_ANDROID && !UNITY_EDITOR
            _adNative = null;
            _showing = false;
#endif
        }

        public void ShowRewardedAd(string placement, Action<bool> onClosed)
        {
#if DIRICHLET_AD && UNITY_ANDROID && !UNITY_EDITOR
            if (!DirichletAdSdk.IsInitialized || _adNative == null)
            {
                Debug.LogWarning("[Ad] SDK 未就绪,激励广告暂不可用。");
                onClosed?.Invoke(false);
                return;
            }
            if (_showing)
            {
                Debug.LogWarning("[Ad] 已有激励广告在播放,忽略本次请求。");
                onClosed?.Invoke(false);
                return;
            }

            if (!long.TryParse(RewardSpaceId, out var spaceId))
            {
                Debug.LogError($"[Ad] 广告位 SpaceId 非法: {RewardSpaceId}");
                onClosed?.Invoke(false);
                return;
            }

            _showing = true;
            // 单次结算 guard:加载失败 / 播放失败 / 正常关闭,只回调一次并复位 _showing。
            bool finished = false;
            Action<bool> done = ok =>
            {
                if (finished) return;
                finished = true;
                _showing = false;
                onClosed?.Invoke(ok);
            };

            var request = new DirichletAdRequest.Builder()
                .WithSpaceId(spaceId)
                .Build();

            // 4.2.5.0 为"加载-展示"两步:先加载,成功后挂交互监听并展示。
            _adNative.LoadRewardVideoAd(
                request,
                onLoaded: ad =>
                {
                    ad.SetInteractionListener(new RewardListener(placement, done));
                    if (!ad.Show())
                    {
                        Debug.LogWarning($"[Ad] 激励广告展示失败 placement={placement}。");
                        done(false);
                    }
                },
                onFailure: error =>
                {
                    Debug.LogError($"[Ad] 激励广告加载失败 placement={placement}: {error.Code} {error.Message}");
                    done(false); // 加载失败 / 无填充 → 不发奖
                });
#elif UNITY_EDITOR
            // 编辑器无真广告:直接模拟"完整观看"以便联调体力发奖链路。
            Debug.Log($"[Ad] (编辑器模拟) 激励广告 placement={placement},模拟看完发奖。");
            onClosed?.Invoke(true);
#else
            Debug.LogWarning($"[Ad] 当前平台无激励广告实现 placement={placement},不发奖。");
            onClosed?.Invoke(false);
#endif
        }

#if DIRICHLET_AD && UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// 单次激励广告交互监听器(4.2.5.0 <see cref="IDirichletRewardAdInteractionListener"/>)。
        /// rewarded 由 OnRewardVerify 置位,OnAdClose 时把结果回传给 onClosed(看完=true,提前关=false)。
        /// 加载失败由 <see cref="ShowRewardedAd"/> 的 onFailure 处理;onClosed 的单次保证在那里的 done guard 里。
        /// </summary>
        private sealed class RewardListener : IDirichletRewardAdInteractionListener
        {
            private readonly string _placement;
            private readonly Action<bool> _onClosed;
            private bool _rewarded;

            public RewardListener(string placement, Action<bool> onClosed)
            {
                _placement = placement;
                _onClosed = onClosed;
            }

            public void OnAdShow() => Debug.Log($"[Ad] 激励广告展示 placement={_placement}");

            public void OnAdClick() => Debug.Log($"[Ad] 激励广告点击 placement={_placement}");

            public void OnRewardVerify(DirichletRewardVerificationEventArgs args)
            {
                _rewarded = args.IsVerified; // 仅以服务端校验通过为准
            }

            public void OnAdClose() => _onClosed?.Invoke(_rewarded); // 关闭结算:看完=true,提前关=false
        }
#endif
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 接入步骤(代码侧已就绪,以下为工程/打包侧手动操作):
//
// 1. 导入 SDK:Assets → Import Package → Custom Package,选 dirichlet_ad_unity_4.2.5.0.unitypackage。
//    导入后删除 Assets/DirichletAd/sample 目录(发布前)。
// 2. 填凭证:把本文件顶部的 MediaId / MediaKey / RewardSpaceId 换成 Dirichlet 后台分配的真实值。
// 3. 开启编译宏:Project Settings → Player → Android → Other Settings → Scripting Define Symbols
//    添加 DIRICHLET_AD(此后 Android 真机走真实 SDK;Editor 仍走模拟发奖)。
// 4. Android 打包配置:
//    - 勾选 Custom Main Gradle Template,合入文档给出的 mainTemplate.gradle 依赖(aar/okhttp/glide 等)。
//    - 在 Assets/Plugins/Android/AndroidManifest.xml 加 INTERNET/ACCESS_NETWORK_STATE/READ_PHONE_STATE/
//      QUERY_ALL_PACKAGES 权限,以及 TapADFileProvider 的 <provider> 节点。
//    - minSdk 21+,Gradle 7.x + AGP 4.0.1+。
// 5. 注册已在 GameProjectBootstrapper.RegisterProjectServices 接好(UNITY_ANDROID || UNITY_EDITOR 时注册)。
// ─────────────────────────────────────────────────────────────────────────────