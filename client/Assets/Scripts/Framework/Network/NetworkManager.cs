#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Google.Protobuf;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
using Steamworks.Data;
#endif

namespace YOTO.Network
{
    /// <summary>
    /// SteamNetworkingSockets（SDR 中继）+ protobuf 消息层。
    /// 拓扑：Lobby Owner = Host（监听），其他成员 = Client（连接到 Host）。
    /// 包格式：[4B little-endian msgId][protobuf payload]，每次 SendMessage 一条独立帧。
    /// 客户端互通走 host 转发（业务自己写转发逻辑），底层 API 不做自动 relay。
    /// </summary>
    public sealed class NetworkManager : IGameService, ITickable
    {
        public uint AppId = 480;
        public string GameTag = "yfw";
        public int VirtualPort = 0;
        public bool AutoStartSessionOnLobbyEnter = true;

#if !DISABLESTEAMWORKS
        private bool _initialized;
        private Lobby? _currentLobby;
        private HostSocket _host;
        private ClientConn _client;
        private readonly MessageRouter _router = new();

        public bool IsInitialized => _initialized;
        public SteamId LocalId   => _initialized ? SteamClient.SteamId : default;
        public string  LocalName => _initialized ? SteamClient.Name    : string.Empty;
        public bool    IsInLobby => _currentLobby.HasValue;
        public Lobby?  CurrentLobby => _currentLobby;
        public bool    IsHost    => _currentLobby.HasValue && _currentLobby.Value.Owner.Id == SteamClient.SteamId;
        public bool    IsSessionActive => _host != null || (_client != null && _client.IsConnected);
        public IReadOnlyList<SteamId> ConnectedPeers => _host?.PeerIds ?? Array.Empty<SteamId>();

        public event Action OnInitialized;
        public event Action<string> OnInitFailed;
        public event Action<Lobby> OnLobbyCreated;
        public event Action<Lobby> OnLobbyEntered;
        public event Action<Lobby> OnLobbyLeft;
        public event Action<Lobby, Friend> OnLobbyMemberJoined;
        public event Action<Lobby, Friend> OnLobbyMemberLeave;
        public event Action<Friend, Lobby> OnLobbyInvite;
        public event Action<Lobby> OnLobbyJoinRequested;
        public event Action OnHostStarted;
        public event Action OnHostStopped;
        public event Action<SteamId> OnClientConnected;
        public event Action<SteamId> OnClientDisconnected;
        public event Action OnConnectedToHost;
        public event Action OnDisconnectedFromHost;
#else
        public bool IsInitialized => false;
        public bool IsInLobby => false;
        public bool IsHost => false;
        public bool IsSessionActive => false;
#endif

        // ════════════════════════════════════════════════
        //   IGameService / ITickable
        // ════════════════════════════════════════════════
        public void Init(GameContext ctx)
        {
#if !DISABLESTEAMWORKS
            Application.runInBackground = true;

            if (SteamClient.IsValid)
            {
                _initialized = true;
            }
            else
            {
                try
                {
                    SteamClient.Init(AppId, asyncCallbacks: false);
                    _initialized = SteamClient.IsValid;
                }
                catch (DllNotFoundException e)
                {
                    OnInitFailed?.Invoke("DllNotFound: " + e.Message);
                    Debug.LogError("[Net] DllNotFound：检查 Assets/Plugins/Facepunch.Steamworks 与 steam_appid.txt\n" + e.Message);
                    return;
                }
                catch (Exception e)
                {
                    OnInitFailed?.Invoke(e.Message);
                    Debug.LogError("[Net] SteamClient.Init 异常: " + e);
                    return;
                }
            }

            if (!_initialized) { OnInitFailed?.Invoke("SteamClient.IsValid == false"); return; }

            // 预热 SDR relay，首次连接延迟更低
            try { SteamNetworkingUtils.InitRelayNetworkAccess(); } catch { /* 老版本无此 API */ }

            Subscribe();
            Debug.Log($"[Net] Init OK appId={AppId} name={SteamClient.Name} id={SteamClient.SteamId}");
            OnInitialized?.Invoke();
            TryAutoJoinFromCommandLine();
#endif
        }

        public void Shutdown()
        {
#if !DISABLESTEAMWORKS
            if (!_initialized) return;
            StopSession();
            LeaveLobby();
            Unsubscribe();
            SteamClient.Shutdown();
            _initialized = false;
#endif
        }

        public void Tick(float dt)
        {
#if !DISABLESTEAMWORKS
            if (!_initialized) return;

            try { SteamClient.RunCallbacks(); }
            catch (Exception e) { Debug.LogWarning("[Net] RunCallbacks: " + e.Message); }

            _host?.Receive();
            _client?.Receive();
#endif
        }

#if !DISABLESTEAMWORKS
        // ════════════════════════════════════════════════
        //   协议注册 / 发送
        // ════════════════════════════════════════════════
        public void Register<T>(uint msgId, Action<SteamId, T> handler) where T : IMessage<T>, new()
            => _router.Register(msgId, handler);

        public void Unregister(uint msgId) => _router.Unregister(msgId);

        public bool SendToHost<T>(uint msgId, T msg, SendType sendType = SendType.Reliable) where T : IMessage<T>
        {
            if (_client == null || !_client.IsConnected)
            {
                Debug.LogWarning("[Net] SendToHost: 客户端未连接");
                return false;
            }
            var payload = MessageRouter.Encode(msgId, msg);
            return _client.Send(payload, sendType);
        }

        public bool SendToClient<T>(SteamId target, uint msgId, T msg, SendType sendType = SendType.Reliable) where T : IMessage<T>
        {
            if (_host == null) { Debug.LogWarning("[Net] SendToClient: 当前不是 host"); return false; }
            var payload = MessageRouter.Encode(msgId, msg);
            return _host.SendTo(target, payload, sendType);
        }

        public int BroadcastToClients<T>(uint msgId, T msg, SendType sendType = SendType.Reliable) where T : IMessage<T>
        {
            if (_host == null) { Debug.LogWarning("[Net] Broadcast: 当前不是 host"); return 0; }
            var payload = MessageRouter.Encode(msgId, msg);
            return _host.SendToAll(payload, sendType);
        }

        // ════════════════════════════════════════════════
        //   Lobby
        // ════════════════════════════════════════════════
        public async Task<bool> CreateLobbyAsync(int maxMembers = 4)
        {
            if (!_initialized) return false;
            var r = await SteamMatchmaking.CreateLobbyAsync(maxMembers);
            return r.HasValue;
        }

        public async Task<List<Lobby>> FindLobbiesAsync(int maxResults = 50)
        {
            if (!_initialized) return new List<Lobby>();
            var b = SteamMatchmaking.LobbyList.WithMaxResults(maxResults);
            if (!string.IsNullOrEmpty(GameTag)) b = b.WithKeyValue("game", GameTag);
            var lobbies = await b.RequestAsync();
            return lobbies != null ? new List<Lobby>(lobbies) : new List<Lobby>();
        }

        public async Task<bool> JoinLobbyAsync(Lobby lobby)
        {
            if (!_initialized) return false;
            var r = await lobby.Join();
            if (r != RoomEnter.Success) { Debug.LogWarning($"[Net] Join lobby fail: {r}"); return false; }
            return true;
        }

        public Task<bool> JoinLobbyByIdAsync(ulong rawId) => JoinLobbyAsync(new Lobby(rawId));

        public void LeaveLobby()
        {
            if (!_currentLobby.HasValue) return;
            var leaving = _currentLobby.Value;
            StopSession();
            leaving.Leave();
            _currentLobby = null;
            OnLobbyLeft?.Invoke(leaving);
        }

        public bool OpenInviteOverlay()
        {
            if (!_currentLobby.HasValue) return false;
            SteamFriends.OpenGameInviteOverlay(_currentLobby.Value.Id);
            GUIUtility.systemCopyBuffer = _currentLobby.Value.Id.Value.ToString();
            return true;
        }

        // ════════════════════════════════════════════════
        //   会话开关
        // ════════════════════════════════════════════════
        private void StartHost()
        {
            if (_host != null) return;
            _host = SteamNetworkingSockets.CreateRelaySocket<HostSocket>(VirtualPort);
            _host.Bind(this);
            Debug.Log("[Net] Host started.");
            OnHostStarted?.Invoke();
        }

        private void ConnectToHost(SteamId hostId)
        {
            if (_client != null) return;
            _client = SteamNetworkingSockets.ConnectRelay<ClientConn>(hostId, VirtualPort);
            _client.Bind(this, hostId);
            Debug.Log($"[Net] Connecting to host {hostId}...");
        }

        private void StopSession()
        {
            if (_host != null)
            {
                try { _host.Close(); }
                catch (Exception e) { Debug.LogWarning("[Net] Host.Close: " + e.Message); }
                _host = null;
                OnHostStopped?.Invoke();
            }
            if (_client != null)
            {
                try { _client.Close(); }
                catch (Exception e) { Debug.LogWarning("[Net] Client.Close: " + e.Message); }
                _client = null;
            }
        }

        // ════════════════════════════════════════════════
        //   Steam 事件
        // ════════════════════════════════════════════════
        private void Subscribe()
        {
            SteamMatchmaking.OnLobbyCreated       += HandleLobbyCreated;
            SteamMatchmaking.OnLobbyEntered       += HandleLobbyEntered;
            SteamMatchmaking.OnLobbyMemberJoined  += HandleLobbyMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave   += HandleLobbyMemberLeave;
            SteamMatchmaking.OnLobbyInvite        += HandleLobbyInvite;
            SteamFriends.OnGameLobbyJoinRequested += HandleGameLobbyJoinRequested;
        }

        private void Unsubscribe()
        {
            SteamMatchmaking.OnLobbyCreated       -= HandleLobbyCreated;
            SteamMatchmaking.OnLobbyEntered       -= HandleLobbyEntered;
            SteamMatchmaking.OnLobbyMemberJoined  -= HandleLobbyMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave   -= HandleLobbyMemberLeave;
            SteamMatchmaking.OnLobbyInvite        -= HandleLobbyInvite;
            SteamFriends.OnGameLobbyJoinRequested -= HandleGameLobbyJoinRequested;
        }

        private void HandleLobbyCreated(Result r, Lobby lobby)
        {
            if (r != Result.OK) { Debug.LogWarning($"[Net] CreateLobby fail: {r}"); return; }
            lobby.SetPublic();
            lobby.SetJoinable(true);
            if (!string.IsNullOrEmpty(GameTag)) lobby.SetData("game", GameTag);
            OnLobbyCreated?.Invoke(lobby);
        }

        private void HandleLobbyEntered(Lobby lobby)
        {
            _currentLobby = lobby;
            OnLobbyEntered?.Invoke(lobby);

            if (!AutoStartSessionOnLobbyEnter) return;
            if (lobby.Owner.Id == SteamClient.SteamId) StartHost();
            else ConnectToHost(lobby.Owner.Id);
        }

        private void HandleLobbyMemberJoined(Lobby l, Friend f) => OnLobbyMemberJoined?.Invoke(l, f);
        private void HandleLobbyMemberLeave(Lobby l, Friend f)  => OnLobbyMemberLeave?.Invoke(l, f);
        private void HandleLobbyInvite(Friend i, Lobby l)       => OnLobbyInvite?.Invoke(i, l);

        private async void HandleGameLobbyJoinRequested(Lobby lobby, SteamId from)
        {
            OnLobbyJoinRequested?.Invoke(lobby);
            await JoinLobbyAsync(lobby);
        }

        private void TryAutoJoinFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "+connect_lobby", StringComparison.OrdinalIgnoreCase) &&
                    ulong.TryParse(args[i + 1], out var raw))
                {
                    _ = JoinLobbyByIdAsync(raw);
                    return;
                }
            }
        }

        // ════════════════════════════════════════════════
        //   Host / Client 回调
        // ════════════════════════════════════════════════
        internal void NotifyClientConnected(SteamId id)    => OnClientConnected?.Invoke(id);
        internal void NotifyClientDisconnected(SteamId id) => OnClientDisconnected?.Invoke(id);
        internal void NotifyConnectedToHost()              => OnConnectedToHost?.Invoke();
        internal void NotifyDisconnectedFromHost()
        {
            OnDisconnectedFromHost?.Invoke();
            _client = null;
        }
        internal void Dispatch(SteamId from, byte[] buf, int size) => _router.Dispatch(from, buf, size);
#endif
    }

#if !DISABLESTEAMWORKS
    // ════════════════════════════════════════════════
    //   Host：监听并维护 SteamId -> Connection 映射
    // ════════════════════════════════════════════════
    internal sealed class HostSocket : SocketManager
    {
        private NetworkManager _owner;
        private readonly Dictionary<ulong, Connection> _byId = new();
        private readonly List<SteamId> _peers = new();
        public IReadOnlyList<SteamId> PeerIds => _peers;

        public void Bind(NetworkManager owner) => _owner = owner;

        public override void OnConnecting(Connection conn, ConnectionInfo info)
        {
            // 仅接受当前 lobby 内成员的连接
            if (_owner != null && _owner.CurrentLobby.HasValue)
            {
                bool inLobby = false;
                foreach (var m in _owner.CurrentLobby.Value.Members)
                    if (m.Id == info.Identity.SteamId) { inLobby = true; break; }
                if (!inLobby) { conn.Close(); return; }
            }
            conn.Accept();
        }

        public override void OnConnected(Connection conn, ConnectionInfo info)
        {
            var sid = info.Identity.SteamId;
            _byId[sid.Value] = conn;
            if (_peers.FindIndex(x => x.Value == sid.Value) < 0) _peers.Add(sid);
            _owner?.NotifyClientConnected(sid);
        }

        public override void OnDisconnected(Connection conn, ConnectionInfo info)
        {
            var sid = info.Identity.SteamId;
            _byId.Remove(sid.Value);
            _peers.RemoveAll(x => x.Value == sid.Value);
            _owner?.NotifyClientDisconnected(sid);
        }

        public override void OnMessage(Connection conn, NetIdentity identity, IntPtr data, int size, long msgNum, long recvTime, int channel)
        {
            var buf = MessageRouter.Rent(size);
            Marshal.Copy(data, buf, 0, size);
            _owner?.Dispatch(identity.SteamId, buf, size);
            MessageRouter.Return(buf);
        }

        public bool SendTo(SteamId target, byte[] payload, SendType sendType)
        {
            if (!_byId.TryGetValue(target.Value, out var c)) return false;
            return c.SendMessage(payload, sendType) == Result.OK;
        }

        public int SendToAll(byte[] payload, SendType sendType)
        {
            int n = 0;
            foreach (var c in _byId.Values)
                if (c.SendMessage(payload, sendType) == Result.OK) n++;
            return n;
        }
    }

    // ════════════════════════════════════════════════
    //   Client：单条连接到 host
    // ════════════════════════════════════════════════
    internal sealed class ClientConn : ConnectionManager
    {
        private NetworkManager _owner;
        private SteamId _hostId;
        public bool IsConnected { get; private set; }

        public void Bind(NetworkManager owner, SteamId hostId)
        {
            _owner = owner;
            _hostId = hostId;
        }

        public override void OnConnecting(ConnectionInfo info) { }

        public override void OnConnected(ConnectionInfo info)
        {
            IsConnected = true;
            _owner?.NotifyConnectedToHost();
        }

        public override void OnDisconnected(ConnectionInfo info)
        {
            IsConnected = false;
            _owner?.NotifyDisconnectedFromHost();
        }

        public override void OnMessage(IntPtr data, int size, long msgNum, long recvTime, int channel)
        {
            var buf = MessageRouter.Rent(size);
            Marshal.Copy(data, buf, 0, size);
            _owner?.Dispatch(_hostId, buf, size);
            MessageRouter.Return(buf);
        }

        public bool Send(byte[] payload, SendType sendType)
        {
            if (!IsConnected) return false;
            return Connection.SendMessage(payload, sendType) == Result.OK;
        }
    }

    // ════════════════════════════════════════════════
    //   消息编码 / 路由
    //   线协议：[uint32 LE msgId][protobuf payload]
    // ════════════════════════════════════════════════
    internal sealed class MessageRouter
    {
        private interface IHandlerEntry
        {
            void Invoke(SteamId from, byte[] buf, int offset, int len);
        }

        private sealed class HandlerEntry<T> : IHandlerEntry where T : IMessage<T>, new()
        {
            public Action<SteamId, T> Handler;
            private readonly MessageParser<T> _parser = new(() => new T());

            public void Invoke(SteamId from, byte[] buf, int offset, int len)
            {
                using var ms = new MemoryStream(buf, offset, len, writable: false);
                var msg = _parser.ParseFrom(ms);
                Handler?.Invoke(from, msg);
            }
        }

        private readonly Dictionary<uint, IHandlerEntry> _handlers = new();

        public void Register<T>(uint msgId, Action<SteamId, T> handler) where T : IMessage<T>, new()
        {
            _handlers[msgId] = new HandlerEntry<T> { Handler = handler };
        }

        public void Unregister(uint msgId) => _handlers.Remove(msgId);

        public void Dispatch(SteamId from, byte[] buf, int size)
        {
            if (size < 4) return;
            uint id = (uint)(buf[0] | (buf[1] << 8) | (buf[2] << 16) | (buf[3] << 24));
            if (!_handlers.TryGetValue(id, out var h))
            {
                Debug.LogWarning($"[Net] 未注册 msgId={id} 丢弃 from={from.Value}");
                return;
            }
            try { h.Invoke(from, buf, 4, size - 4); }
            catch (Exception e) { Debug.LogError($"[Net] msgId={id} handler 抛异常: {e}"); }
        }

        public static byte[] Encode<T>(uint msgId, T msg) where T : IMessage<T>
        {
            int payload = msg.CalculateSize();
            var buf = new byte[4 + payload];
            buf[0] = (byte)(msgId);
            buf[1] = (byte)(msgId >> 8);
            buf[2] = (byte)(msgId >> 16);
            buf[3] = (byte)(msgId >> 24);
            using var ms = new MemoryStream(buf, 4, payload);
            msg.WriteTo(ms);
            return buf;
        }

        // 收包缓冲池：Tick 单线程使用，无锁
        private const int MaxPooled = 32;
        private static readonly Stack<byte[]> Pool = new();
        public static byte[] Rent(int size)
        {
            while (Pool.Count > 0)
            {
                var b = Pool.Pop();
                if (b.Length >= size) return b;
            }
            return new byte[Math.Max(size, 256)];
        }
        public static void Return(byte[] buf)
        {
            if (buf != null && Pool.Count < MaxPooled) Pool.Push(buf);
        }
    }
#endif
}