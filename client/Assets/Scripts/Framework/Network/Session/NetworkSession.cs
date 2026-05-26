using System;
using Google.Protobuf;
using UnityEngine;

namespace YOTO.Network
{
    /// INetworkSession 默认实现。把 transport 的 PeerId 字节流 + MessageDispatcher 的 typed
    /// handler 缝合起来，附带 ConnectionState 跟踪。
    public sealed class NetworkSession : INetworkSession, IGameService
    {
        private readonly INetworkTransport _transport;
        private readonly MessageRegistry _registry;
        private readonly MessageDispatcher _dispatcher;
        private readonly IMessageSerializer _serializer;
        private readonly MainThreadDispatcher _mainThread;

        private ConnectionState _state = ConnectionState.Disconnected;

        public NetworkSession(
            INetworkTransport transport,
            MessageRegistry registry,
            MessageDispatcher dispatcher,
            IMessageSerializer serializer,
            MainThreadDispatcher mainThread)
        {
            _transport = transport;
            _registry = registry;
            _dispatcher = dispatcher;
            _serializer = serializer;
            _mainThread = mainThread;
        }

        public ConnectionState State => _state;
        public bool IsServer => _transport.IsServer;

        public event Action StateChanged;

        public void Init(GameContext ctx)
        {
            _transport.Connected += HandleConnected;
            _transport.Disconnected += HandleDisconnected;
            _mainThread.SetHandler(OnIncoming);
        }

        public void Shutdown()
        {
            _transport.Connected -= HandleConnected;
            _transport.Disconnected -= HandleDisconnected;
            _mainThread.SetHandler(null);
        }

        public void StartServer()
        {
            SetState(ConnectionState.Connected);
            _transport.StartServer();
        }

        public void Connect(in SessionInfo info)
        {
            SetState(ConnectionState.Connecting);
            _transport.Connect(info.HostPeer);
        }

        public void Disconnect()
        {
            _transport.Disconnect();
            SetState(ConnectionState.Disconnected);
        }

        public void Send<T>(PeerId peer, T message, DeliveryMode mode = DeliveryMode.Reliable, byte channel = 0)
            where T : IMessage<T>
        {
            var buffer = Encode(message, channel);
            if (buffer == null) return;
            _transport.Send(peer, buffer, mode);
        }

        public void Broadcast<T>(T message, DeliveryMode mode = DeliveryMode.Reliable, byte channel = 0)
            where T : IMessage<T>
        {
            var buffer = Encode(message, channel);
            if (buffer == null) return;
            _transport.Broadcast(buffer, mode);
        }

        public void Subscribe<T>(Action<NetworkContext, T> handler) where T : IMessage<T>, new()
            => _dispatcher.Subscribe(handler);

        public void Unsubscribe<T>() where T : IMessage<T>
            => _dispatcher.Unsubscribe<T>();

        /// 出站 buffer 走精确长度的 byte[]：Facepunch.SendMessage(byte[]) 发的是 buffer.Length，
        /// 不要让池里的 slack 跟着上线。入站才用 PacketBufferPool。
        private byte[] Encode<T>(T message, byte channel) where T : IMessage<T>
        {
            ushort id;
            try { id = _registry.GetId<T>(); }
            catch (Exception e)
            {
                Debug.LogError($"[Net] send failed: {e.Message}");
                return null;
            }

            int payload = _serializer.CalculateSize(message);
            var buffer = new byte[PacketHeader.Size + payload];
            PacketCodec.WriteHeader(buffer, 0, new PacketHeader { MessageId = id, Channel = channel });
            _serializer.Serialize(message, buffer, PacketHeader.Size);
            return buffer;
        }

        private void OnIncoming(in IncomingPacket packet)
            => _dispatcher.Dispatch(in packet, _transport.IsServer);

        private void HandleConnected(PeerId peer)
        {
            if (!_transport.IsServer) SetState(ConnectionState.Connected);
        }

        private void HandleDisconnected(PeerId peer)
        {
            if (!_transport.IsServer) SetState(ConnectionState.Disconnected);
        }

        private void SetState(ConnectionState s)
        {
            if (_state == s) return;
            _state = s;
            StateChanged?.Invoke();
        }
    }
}
