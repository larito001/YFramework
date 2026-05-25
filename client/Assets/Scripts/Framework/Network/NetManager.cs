#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using System.Collections.Generic;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace YOTO.Net
{
    /// <summary>
    /// 房间网络管理器：把 Lobby（匹配） + Transport（P2P 数据通道）粘起来。
    /// CreateRoom = 建大厅 + Host；JoinRoom = 进大厅 + 连主机；LeaveRoom = 反向拆。
    /// </summary>
    public sealed class NetManager : IGameService, ITickable
    {
        public LobbyManager Lobby { get; } = new();
        public SteamP2PTransport Transport { get; } = new();

        public bool IsHost { get; private set; }
        public bool InRoom { get; private set; }

#if !DISABLESTEAMWORKS
        public event Action RoomJoined;
        public event Action RoomLeft;
        public event Action<EResult> RoomFailed;
        public event Action<CSteamID> PlayerJoined;
        public event Action<CSteamID> PlayerLeft;
        public event Action<CSteamID, ArraySegment<byte>> MessageReceived;

        private readonly Dictionary<CSteamID, HSteamNetConnection> _peerToConn = new();
        private readonly Dictionary<HSteamNetConnection, CSteamID> _connToPeer = new();
#else
        public event Action RoomJoined;
        public event Action RoomLeft;
        public event Action<object> RoomFailed;
        public event Action<object> PlayerJoined;
        public event Action<object> PlayerLeft;
        public event Action<object, ArraySegment<byte>> MessageReceived;
#endif

        public void Init(GameContext ctx)
        {
#if !DISABLESTEAMWORKS
            Lobby.Entered += OnLobbyEntered;
            Lobby.Failed += OnLobbyFailed;
            Transport.Connected += OnPeerConnected;
            Transport.Disconnected += OnPeerDisconnected;
            Transport.MessageReceived += OnMessage;
#endif
        }

        public void Shutdown()
        {
#if !DISABLESTEAMWORKS
            if (InRoom) LeaveRoom();
            Lobby.Entered -= OnLobbyEntered;
            Lobby.Failed -= OnLobbyFailed;
            Transport.Connected -= OnPeerConnected;
            Transport.Disconnected -= OnPeerDisconnected;
            Transport.MessageReceived -= OnMessage;
#endif
        }

        public void Tick(float dt) => Transport.Poll();

        public void CreateRoom(int maxMembers = 8)
        {
#if !DISABLESTEAMWORKS
            Lobby.CreateLobby(maxMembers);
#endif
        }

#if !DISABLESTEAMWORKS
        public void JoinRoom(CSteamID lobbyId) => Lobby.JoinLobby(lobbyId);
#else
        public void JoinRoom(object lobbyId) { }
#endif

        public void LeaveRoom()
        {
#if !DISABLESTEAMWORKS
            if (!InRoom) return;
            Transport.Close();
            Lobby.Leave();
            _peerToConn.Clear();
            _connToPeer.Clear();
            IsHost = false;
            InRoom = false;
            RoomLeft?.Invoke();
#endif
        }

#if !DISABLESTEAMWORKS
        public bool Send(CSteamID target, byte[] data, int offset, int length, bool reliable)
            => _peerToConn.TryGetValue(target, out var conn)
               && Transport.Send(conn, data, offset, length, reliable);

        private void OnLobbyEntered(CSteamID lobby)
        {
            InRoom = true;
            IsHost = Lobby.IsOwner;
            if (IsHost) Transport.Host();
            else Transport.Connect(Lobby.Owner);
            RoomJoined?.Invoke();
        }

        private void OnLobbyFailed(EResult r) => RoomFailed?.Invoke(r);

        private void OnPeerConnected(HSteamNetConnection conn, CSteamID peer)
        {
            _peerToConn[peer] = conn;
            _connToPeer[conn] = peer;
            PlayerJoined?.Invoke(peer);
        }

        private void OnPeerDisconnected(HSteamNetConnection conn, CSteamID peer)
        {
            _peerToConn.Remove(peer);
            _connToPeer.Remove(conn);
            PlayerLeft?.Invoke(peer);
        }

        private void OnMessage(HSteamNetConnection conn, ArraySegment<byte> data)
        {
            if (_connToPeer.TryGetValue(conn, out var peer))
                MessageReceived?.Invoke(peer, data);
        }
#else
        public bool Send(object target, byte[] data, int offset, int length, bool reliable) => false;
#endif
    }
}
