#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
using Steamworks.Data;
#endif

namespace YOTO.Network
{
    /// SteamNetworkingSockets (SDR relay) 实现的 INetworkTransport。
    /// 拓扑：Server = 监听 socket；Client = 单连到 Server 的 connection。
    /// 包是 PacketCodec 头 + protobuf payload；每次 SendMessage 一条独立帧。
    public sealed class SteamNetworkTransport : INetworkTransport, IGameService
    {
        public int VirtualPort = 0;

        private readonly SteamPlatform _platform;
        private readonly MainThreadDispatcher _inbox;

        public event Action<PeerId> Connected;
        public event Action<PeerId> Disconnected;

        public bool IsServer => _host != null;
        public bool IsConnected => _host != null || (_client != null && _client.IsConnected);

#if !DISABLESTEAMWORKS
        private HostSocket _host;
        private ClientConn _client;
#else
        private object _host, _client;
#endif

        public SteamNetworkTransport(SteamPlatform platform, MainThreadDispatcher inbox)
        {
            _platform = platform;
            _inbox = inbox;
        }

        public void Init(GameContext ctx) { }

        public void Shutdown() => Disconnect();

        public void StartServer()
        {
#if !DISABLESTEAMWORKS
            if (_host != null) return;
            _host = SteamNetworkingSockets.CreateRelaySocket<HostSocket>(VirtualPort);
            _host.Bind(this);
            Debug.Log("[Net] SteamTransport.StartServer");
#endif
        }

        public void Connect(PeerId hostPeer)
        {
#if !DISABLESTEAMWORKS
            if (_client != null) return;
            var sid = new SteamId { Value = hostPeer.Value };
            _client = SteamNetworkingSockets.ConnectRelay<ClientConn>(sid, VirtualPort);
            _client.Bind(this, hostPeer);
            Debug.Log($"[Net] SteamTransport.Connect → {hostPeer}");
#endif
        }

        public void Disconnect()
        {
#if !DISABLESTEAMWORKS
            if (_host != null)
            {
                try { _host.Close(); } catch (Exception e) { Debug.LogWarning("[Net] host.Close: " + e.Message); }
                _host = null;
            }
            if (_client != null)
            {
                try { _client.Close(); } catch (Exception e) { Debug.LogWarning("[Net] client.Close: " + e.Message); }
                _client = null;
            }
#endif
        }

        public bool Send(PeerId peer, byte[] payload, DeliveryMode mode)
        {
#if !DISABLESTEAMWORKS
            var sendType = MapSendType(mode);
            if (_host != null) return _host.SendTo(peer, payload, sendType);
            if (_client != null && _client.IsConnected) return _client.Send(payload, sendType);
            Debug.LogWarning("[Net] SteamTransport.Send: not connected");
            return false;
#else
            return false;
#endif
        }

        public int Broadcast(byte[] payload, DeliveryMode mode)
        {
#if !DISABLESTEAMWORKS
            var sendType = MapSendType(mode);
            if (_host != null) return _host.SendToAll(payload, sendType);
            // 客户端 Broadcast 默认转发给 host；P2P 全互连交给业务自己写转发。
            if (_client != null && _client.IsConnected)
                return _client.Send(payload, sendType) ? 1 : 0;
            return 0;
#else
            return 0;
#endif
        }

        public void Poll()
        {
#if !DISABLESTEAMWORKS
            _host?.Receive();
            _client?.Receive();
#endif
        }

#if !DISABLESTEAMWORKS
        private static SendType MapSendType(DeliveryMode mode) => mode switch
        {
            DeliveryMode.Unreliable => SendType.Unreliable,
            DeliveryMode.UnreliableSequenced => SendType.Unreliable,
            DeliveryMode.Reliable => SendType.Reliable,
            DeliveryMode.ReliableOrdered => SendType.Reliable,
            _ => SendType.Reliable,
        };

        internal void RaiseConnected(PeerId p) => Connected?.Invoke(p);
        internal void RaiseDisconnected(PeerId p) => Disconnected?.Invoke(p);

        internal void Enqueue(PeerId sender, IntPtr data, int size)
        {
            var buf = PacketBufferPool.Rent(size);
            Marshal.Copy(data, buf, 0, size);
            _inbox.Enqueue(new IncomingPacket(sender, buf, size, channel: 0));
        }

        internal sealed class HostSocket : SocketManager
        {
            private SteamNetworkTransport _owner;
            private readonly Dictionary<ulong, Connection> _byId = new();

            public void Bind(SteamNetworkTransport owner) => _owner = owner;

            public override void OnConnecting(Connection conn, ConnectionInfo info)
            {
                // base 会做 Accept + 把 Connection 加入本 SocketManager 的 pollGroup。
                // 之前自己写 conn.Accept() 不调 base，导致连接没进 pollGroup，
                // Receive() 走 ReceiveMessagesOnPollGroup 自然取不到任何包 →
                // 房主收不到客户端消息（客户端走 ConnectionManager 不依赖 pollGroup，所以反方向正常）。
                base.OnConnecting(conn, info);
                Debug.Log($"[Net] HostSocket OnConnecting from {info.Identity.SteamId.Value}");
            }

            public override void OnConnected(Connection conn, ConnectionInfo info)
            {
                base.OnConnected(conn, info);
                var sid = info.Identity.SteamId;
                _byId[sid.Value] = conn;
                Debug.Log($"[Net] HostSocket OnConnected from {sid.Value} (total peers={_byId.Count})");
                _owner?.RaiseConnected(new PeerId(sid.Value));
            }

            public override void OnDisconnected(Connection conn, ConnectionInfo info)
            {
                base.OnDisconnected(conn, info);
                var sid = info.Identity.SteamId;
                _byId.Remove(sid.Value);
                _owner?.RaiseDisconnected(new PeerId(sid.Value));
            }

            public override void OnMessage(Connection conn, NetIdentity identity, IntPtr data, int size, long msgNum, long recvTime, int channel)
            {
                _owner?.Enqueue(new PeerId(identity.SteamId.Value), data, size);
            }

            public bool SendTo(PeerId target, byte[] payload, SendType sendType)
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

        internal sealed class ClientConn : ConnectionManager
        {
            private SteamNetworkTransport _owner;
            private PeerId _hostPeer;
            public bool IsConnected { get; private set; }

            public void Bind(SteamNetworkTransport owner, PeerId hostPeer)
            {
                _owner = owner;
                _hostPeer = hostPeer;
            }

            public override void OnConnecting(ConnectionInfo info)
            {
                base.OnConnecting(info);
                Debug.Log($"[Net] ClientConn OnConnecting → host {_hostPeer}");
            }

            public override void OnConnected(ConnectionInfo info)
            {
                base.OnConnected(info);
                IsConnected = true;
                Debug.Log($"[Net] ClientConn OnConnected ← host {_hostPeer}");
                _owner?.RaiseConnected(_hostPeer);
            }

            public override void OnDisconnected(ConnectionInfo info)
            {
                base.OnDisconnected(info);
                IsConnected = false;
                _owner?.RaiseDisconnected(_hostPeer);
            }

            public override void OnMessage(IntPtr data, int size, long msgNum, long recvTime, int channel)
            {
                _owner?.Enqueue(_hostPeer, data, size);
            }

            public bool Send(byte[] payload, SendType sendType)
            {
                if (!IsConnected) return false;
                return Connection.SendMessage(payload, sendType) == Result.OK;
            }
        }
#endif
    }
}
