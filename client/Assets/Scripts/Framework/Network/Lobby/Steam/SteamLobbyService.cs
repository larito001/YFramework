#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using System.Collections.Generic;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
using Steamworks.Data;
#endif

namespace YOTO.Network
{
    /// 基于 Steam Matchmaking 的 ILobbyService 实现。
    /// 所有公开方法 fire-and-forget；结果通过事件回流（Created / Joined / JoinFailed / Found / ...）。
    /// 注意：本服务不会自动 Connect Session。Lobby 与 Session 解耦，
    /// 由 gameplay 在收到 Joined 后自行决定 StartServer 或 Connect(SessionInfo)。
    public sealed class SteamLobbyService : ILobbyService, IGameService
    {
        public string GameTag = "yfw";

        private readonly SteamPlatform _platform;

#if !DISABLESTEAMWORKS
        private Lobby? _currentRaw;
#endif
        private LobbyInfo? _current;
        private readonly List<LobbyMember> _members = new();

        public bool IsInLobby => _current.HasValue;
        public LobbyInfo? Current => _current;
        public IReadOnlyList<LobbyMember> Members => _members;

        private ulong? _pendingAutoJoinLobbyId;

        public event Action<LobbyInfo> Created;
        public event Action<LobbyInfo> Joined;
        public event Action<LobbyJoinFailure> JoinFailed;
        public event Action<LobbyInfo> Left;
        public event Action<LobbyInfo, LobbyMember> MemberJoined;
        public event Action<LobbyInfo, LobbyMember> MemberLeft;
        public event Action<LobbyInfo> InviteReceived;
        public event Action<IReadOnlyList<LobbyInfo>> Found;

        public SteamLobbyService(SteamPlatform platform)
        {
            _platform = platform;
        }

        public void Init(GameContext ctx)
        {
#if !DISABLESTEAMWORKS
            SteamMatchmaking.OnLobbyCreated += HandleLobbyCreated;
            SteamMatchmaking.OnLobbyEntered += HandleLobbyEntered;
            SteamMatchmaking.OnLobbyMemberJoined += HandleLobbyMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave += HandleLobbyMemberLeave;
            SteamMatchmaking.OnLobbyInvite += HandleLobbyInvite;
            SteamFriends.OnGameLobbyJoinRequested += HandleGameLobbyJoinRequested;
            // 仅解析命令行，不在 Init 里直接 Join：那时 gameplay/UI 还没订阅 Joined / JoinFailed，
            // 自动入房如果很快完成，事件会被丢掉。gameplay 启动后调 TryConsumePendingAutoJoin 触发。
            ParsePendingAutoJoinFromCommandLine();
#endif
        }

        public void Shutdown()
        {
#if !DISABLESTEAMWORKS
            Leave();
            SteamMatchmaking.OnLobbyCreated -= HandleLobbyCreated;
            SteamMatchmaking.OnLobbyEntered -= HandleLobbyEntered;
            SteamMatchmaking.OnLobbyMemberJoined -= HandleLobbyMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave -= HandleLobbyMemberLeave;
            SteamMatchmaking.OnLobbyInvite -= HandleLobbyInvite;
            SteamFriends.OnGameLobbyJoinRequested -= HandleGameLobbyJoinRequested;
#endif
        }

        public async void Create(int maxMembers = 4)
        {
#if !DISABLESTEAMWORKS
            if (!_platform.IsValid) return;
            try
            {
                var r = await SteamMatchmaking.CreateLobbyAsync(maxMembers);
                if (!r.HasValue) Debug.LogWarning("[Net] CreateLobbyAsync returned null");
                // 成功路径走 OnLobbyCreated + OnLobbyEntered；事件分发由 handler 完成。
            }
            catch (Exception e) { Debug.LogError("[Net] CreateLobby exception: " + e); }
#endif
        }

        public async void Find(int maxResults = 50)
        {
#if !DISABLESTEAMWORKS
            if (!_platform.IsValid) { Found?.Invoke(Array.Empty<LobbyInfo>()); return; }
            try
            {
                var b = SteamMatchmaking.LobbyList.WithMaxResults(maxResults);
                if (!string.IsNullOrEmpty(GameTag)) b = b.WithKeyValue("game", GameTag);
                var lobbies = await b.RequestAsync();
                var list = new List<LobbyInfo>();
                if (lobbies != null)
                    foreach (var l in lobbies) list.Add(ToInfo(l));
                Found?.Invoke(list);
            }
            catch (Exception e)
            {
                Debug.LogError("[Net] FindLobbies exception: " + e);
                Found?.Invoke(Array.Empty<LobbyInfo>());
            }
#endif
        }

        public async void Join(ulong lobbyId)
        {
#if !DISABLESTEAMWORKS
            if (!_platform.IsValid)
            {
                JoinFailed?.Invoke(new LobbyJoinFailure(lobbyId, "platform not valid"));
                return;
            }
            try
            {
                var lobby = new Lobby(lobbyId);
                var r = await lobby.Join();
                if (r != RoomEnter.Success)
                {
                    Debug.LogWarning($"[Net] Join lobby fail: {r}");
                    JoinFailed?.Invoke(new LobbyJoinFailure(lobbyId, r.ToString()));
                }
                // 成功路径走 OnLobbyEntered。
            }
            catch (Exception e)
            {
                Debug.LogError("[Net] Join exception: " + e);
                JoinFailed?.Invoke(new LobbyJoinFailure(lobbyId, e.Message));
            }
#endif
        }

        public void Leave()
        {
#if !DISABLESTEAMWORKS
            if (!_currentRaw.HasValue) return;
            var leaving = _current.Value;
            var raw = _currentRaw.Value;
            _currentRaw = null;
            _current = null;
            _members.Clear();
            raw.Leave();
            Left?.Invoke(leaving);
#endif
        }

        public bool OpenInviteOverlay()
        {
#if !DISABLESTEAMWORKS
            if (!_currentRaw.HasValue) return false;
            SteamFriends.OpenGameInviteOverlay(_currentRaw.Value.Id);
            GUIUtility.systemCopyBuffer = _currentRaw.Value.Id.Value.ToString();
            return true;
#else
            return false;
#endif
        }

        public bool TryConsumePendingAutoJoin()
        {
#if !DISABLESTEAMWORKS
            if (!_pendingAutoJoinLobbyId.HasValue) return false;
            var id = _pendingAutoJoinLobbyId.Value;
            _pendingAutoJoinLobbyId = null;
            Join(id);
            return true;
#else
            return false;
#endif
        }

#if !DISABLESTEAMWORKS
        private LobbyInfo ToInfo(Lobby lobby)
        {
            return new LobbyInfo(
                id: lobby.Id.Value,
                owner: new PeerId(lobby.Owner.Id.Value),
                memberCount: lobby.MemberCount,
                maxMembers: lobby.MaxMembers,
                tag: lobby.GetData("game"));
        }

        private void HandleLobbyCreated(Result r, Lobby lobby)
        {
            if (r != Result.OK) { Debug.LogWarning($"[Net] CreateLobby fail: {r}"); return; }
            lobby.SetPublic();
            lobby.SetJoinable(true);
            if (!string.IsNullOrEmpty(GameTag)) lobby.SetData("game", GameTag);
            Created?.Invoke(ToInfo(lobby));
        }

        private void HandleLobbyEntered(Lobby lobby)
        {
            _currentRaw = lobby;
            _current = ToInfo(lobby);
            RefreshMembers(lobby);
            Joined?.Invoke(_current.Value);
        }

        private void HandleLobbyMemberJoined(Lobby l, Friend f)
        {
            if (!_current.HasValue) return;
            var member = new LobbyMember(new PeerId(f.Id.Value), f.Name);
            if (_currentRaw.HasValue) RefreshMembers(_currentRaw.Value);
            MemberJoined?.Invoke(_current.Value, member);
        }

        private void HandleLobbyMemberLeave(Lobby l, Friend f)
        {
            if (!_current.HasValue) return;
            var member = new LobbyMember(new PeerId(f.Id.Value), f.Name);
            if (_currentRaw.HasValue) RefreshMembers(_currentRaw.Value);
            MemberLeft?.Invoke(_current.Value, member);
        }

        private void RefreshMembers(Lobby lobby)
        {
            _members.Clear();
            foreach (var m in lobby.Members)
                _members.Add(new LobbyMember(new PeerId(m.Id.Value), m.Name));
        }

        private void HandleLobbyInvite(Friend inviter, Lobby lobby)
        {
            InviteReceived?.Invoke(ToInfo(lobby));
        }

        private void HandleGameLobbyJoinRequested(Lobby lobby, SteamId from)
        {
            // 接受邀请 = fire-and-forget Join(id)，结果走 Joined / JoinFailed。
            Join(lobby.Id.Value);
        }

        private void ParsePendingAutoJoinFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "+connect_lobby", StringComparison.OrdinalIgnoreCase) &&
                    ulong.TryParse(args[i + 1], out var raw))
                {
                    _pendingAutoJoinLobbyId = raw;
                    Debug.Log($"[Net] auto-join pending: lobby {raw} — call TryConsumePendingAutoJoin after subscribing Joined");
                    return;
                }
            }
        }
#endif
    }
}
