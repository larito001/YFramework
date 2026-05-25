#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace YOTO.Net
{
    /// <summary>
    /// Steam 大厅管理：创建 / 加入 / 查找。
    /// 完成事件统一由 <see cref="Entered"/> / <see cref="Failed"/> / <see cref="Found"/> 上抛。
    /// </summary>
    public sealed class LobbyManager
    {
#if !DISABLESTEAMWORKS
        public event Action<CSteamID> Entered;            // 创建或加入成功（自己已在大厅里）
        public event Action<EResult> Failed;              // 创建 / 加入失败
        public event Action<CSteamID[]> Found;            // FindLobby 结果
        public event Action<CSteamID> MemberJoined;       // 有成员进入当前大厅
        public event Action<CSteamID> MemberLeft;         // 有成员离开当前大厅

        /// <summary>
        /// 自定义游戏标签：CreateLobby 时写入 lobby data["game"]=GameTag，
        /// FindLobby 时按相同标签过滤，避免和共享 AppID（如 Spacewar 480）的其他玩家混淆。
        /// 改成空字符串则不过滤。
        /// </summary>
        public string GameTag { get; set; } = "yfw";

        private const string TagKey = "game";

        public CSteamID CurrentLobby { get; private set; } = CSteamID.Nil;
        public CSteamID Owner => CurrentLobby == CSteamID.Nil ? CSteamID.Nil : SteamMatchmaking.GetLobbyOwner(CurrentLobby);
        public bool IsOwner => Owner == SteamUser.GetSteamID();

        private CallResult<LobbyCreated_t> _crCreate;
        private CallResult<LobbyEnter_t> _crJoin;
        private CallResult<LobbyMatchList_t> _crFind;
        private Callback<LobbyChatUpdate_t> _cbChat;

        public void CreateLobby(int maxMembers = 8, ELobbyType type = ELobbyType.k_ELobbyTypePublic)
        {
            EnsureCallbacks();
            var h = SteamMatchmaking.CreateLobby(type, maxMembers);
            (_crCreate ??= CallResult<LobbyCreated_t>.Create()).Set(h, OnLobbyCreated);
        }

        public void JoinLobby(CSteamID lobbyId)
        {
            EnsureCallbacks();
            var h = SteamMatchmaking.JoinLobby(lobbyId);
            (_crJoin ??= CallResult<LobbyEnter_t>.Create()).Set(h, OnLobbyEntered);
        }

        public void FindLobby()
        {
            EnsureCallbacks();
            // Worldwide distance + 大上限，避免默认地区过滤把自己刚建的 lobby 漏掉
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(50);
            if (!string.IsNullOrEmpty(GameTag))
                SteamMatchmaking.AddRequestLobbyListStringFilter(TagKey, GameTag, ELobbyComparison.k_ELobbyComparisonEqual);
            var h = SteamMatchmaking.RequestLobbyList();
            (_crFind ??= CallResult<LobbyMatchList_t>.Create()).Set(h, OnLobbyList);
        }

        public void Leave()
        {
            if (CurrentLobby == CSteamID.Nil) return;
            SteamMatchmaking.LeaveLobby(CurrentLobby);
            CurrentLobby = CSteamID.Nil;
        }

        private void EnsureCallbacks()
        {
            _cbChat ??= Callback<LobbyChatUpdate_t>.Create(OnChatUpdate);
        }

        private void OnLobbyCreated(LobbyCreated_t r, bool ioFailure)
        {
            if (ioFailure || r.m_eResult != EResult.k_EResultOK)
            {
                Failed?.Invoke(ioFailure ? EResult.k_EResultIOFailure : r.m_eResult);
                return;
            }
            CurrentLobby = (CSteamID)r.m_ulSteamIDLobby;
            if (!string.IsNullOrEmpty(GameTag))
                SteamMatchmaking.SetLobbyData(CurrentLobby, TagKey, GameTag);
            Entered?.Invoke(CurrentLobby);
        }

        private void OnLobbyEntered(LobbyEnter_t r, bool ioFailure)
        {
            var resp = (EChatRoomEnterResponse)r.m_EChatRoomEnterResponse;
            if (ioFailure || resp != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                Failed?.Invoke(ioFailure ? EResult.k_EResultIOFailure : EResult.k_EResultFail);
                return;
            }
            CurrentLobby = (CSteamID)r.m_ulSteamIDLobby;
            Entered?.Invoke(CurrentLobby);
        }

        private void OnLobbyList(LobbyMatchList_t r, bool ioFailure)
        {
            if (ioFailure) { Found?.Invoke(Array.Empty<CSteamID>()); return; }
            int n = (int)r.m_nLobbiesMatching;
            var list = new CSteamID[n];
            for (int i = 0; i < n; i++) list[i] = SteamMatchmaking.GetLobbyByIndex(i);
            Found?.Invoke(list);
        }

        private void OnChatUpdate(LobbyChatUpdate_t r)
        {
            if ((CSteamID)r.m_ulSteamIDLobby != CurrentLobby) return;
            var who = (CSteamID)r.m_ulSteamIDUserChanged;
            var change = (EChatMemberStateChange)r.m_rgfChatMemberStateChange;
            if ((change & EChatMemberStateChange.k_EChatMemberStateChangeEntered) != 0)
            {
                MemberJoined?.Invoke(who);
            }
            else if ((change & (EChatMemberStateChange.k_EChatMemberStateChangeLeft
                                 | EChatMemberStateChange.k_EChatMemberStateChangeDisconnected
                                 | EChatMemberStateChange.k_EChatMemberStateChangeKicked
                                 | EChatMemberStateChange.k_EChatMemberStateChangeBanned)) != 0)
            {
                MemberLeft?.Invoke(who);
            }
        }
#else
        public event Action<object> Entered;
        public event Action<object> Failed;
        public event Action<object[]> Found;
        public event Action<object> MemberJoined;
        public event Action<object> MemberLeft;
        public void CreateLobby(int maxMembers = 8) { }
        public void JoinLobby(object lobbyId) { }
        public void FindLobby() { }
        public void Leave() { }
#endif
    }
}
