#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace YOTO.Network
{
    /// SteamClient.Init / RunCallbacks / Shutdown 的归属。Lobby 与 Transport 都依赖此服务，
    /// 但 gameplay 不需要直接接触它。
    public sealed class SteamPlatform : IGameService, ITickable
    {
        public uint AppId = 480;

        public bool IsValid { get; private set; }

        public event Action Initialized;
        public event Action<string> InitFailed;

#if !DISABLESTEAMWORKS
        public SteamId LocalSteamId => IsValid ? SteamClient.SteamId : default;
        public string LocalName => IsValid ? SteamClient.Name : string.Empty;
#endif

        public PeerId LocalPeer
        {
            get
            {
#if !DISABLESTEAMWORKS
                return IsValid ? new PeerId(SteamClient.SteamId.Value) : PeerId.None;
#else
                return PeerId.None;
#endif
            }
        }

        public void Init(GameContext ctx)
        {
#if !DISABLESTEAMWORKS
            Application.runInBackground = true;

            if (SteamClient.IsValid)
            {
                IsValid = true;
            }
            else
            {
                try
                {
                    SteamClient.Init(AppId, asyncCallbacks: false);
                    IsValid = SteamClient.IsValid;
                }
                catch (DllNotFoundException e)
                {
                    InitFailed?.Invoke("DllNotFound: " + e.Message);
                    Debug.LogError("[Net] DllNotFound：检查 Assets/Plugins/Facepunch.Steamworks 与 steam_appid.txt\n" + e.Message);
                    return;
                }
                catch (Exception e)
                {
                    InitFailed?.Invoke(e.Message);
                    Debug.LogError("[Net] SteamClient.Init 异常: " + e);
                    return;
                }
            }

            if (!IsValid) { InitFailed?.Invoke("SteamClient.IsValid == false"); return; }

            try { SteamNetworkingUtils.InitRelayNetworkAccess(); } catch { /* 老版本无此 API */ }

            Debug.Log($"[Net] SteamPlatform OK appId={AppId} name={SteamClient.Name} id={SteamClient.SteamId}");
            Initialized?.Invoke();
#endif
        }

        public void Shutdown()
        {
#if !DISABLESTEAMWORKS
            if (!IsValid) return;
            SteamClient.Shutdown();
            IsValid = false;
#endif
        }

        public void Tick(float dt)
        {
#if !DISABLESTEAMWORKS
            if (!IsValid) return;
            try { SteamClient.RunCallbacks(); }
            catch (Exception e) { Debug.LogWarning("[Net] SteamClient.RunCallbacks: " + e.Message); }
#endif
        }
    }
}
