#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using Google.Protobuf;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace YOTO.Net
{
    /// <summary>
    /// 房间网络管理器：把 Lobby（匹配） + Transport（P2P 数据通道）粘起来。
    /// 同时提供 protobuf 消息路由：
    ///   - 业务在 GamePlay 层定义 NetKey（建议 static class + const int，参考 GameProjectBootstrapper）
    ///   - 通过 <see cref="Subscribe{T}"/> 注册「key + protobuf 类型 → 处理函数」
    ///   - 通过 <see cref="Send{T}"/> / <see cref="Broadcast{T}"/> 投递消息
    ///
    /// 线上字节布局：[int32 LE NetKey][protobuf body]
    ///   - NetKey == 0 → 保留给 <see cref="Send(CSteamID, byte[], int, int, bool, int)"/> 的原始字节通道，
    ///                   解码后走 <see cref="MessageReceived"/> 事件（便于调试）
    ///   - NetKey != 0 → 查 <see cref="_slots"/> 表分派到注册的处理函数
    /// </summary>
    public sealed class NetManager : IGameService, ITickable
    {
        public LobbyManager Lobby { get; } = new();
        public SteamP2PTransport Transport { get; } = new();

        public bool IsHost { get; private set; }
        public bool InRoom { get; private set; }

#if !DISABLESTEAMWORKS
        // ───────── Room / Transport 级事件 ─────────

        public event Action RoomJoined;
        public event Action RoomLeft;
        public event Action<EResult> RoomFailed;
        public event Action<CSteamID> PlayerJoined;
        public event Action<CSteamID> PlayerLeft;

        /// <summary>
        /// 收到 key=0（原始字节）消息时上抛。Payload 为去掉 4 字节 key 头之后的内容，仅回调内有效。
        /// 业务用 protobuf 走 Subscribe，无需订阅本事件。
        /// </summary>
        public event Action<CSteamID, ArraySegment<byte>> MessageReceived;

        // ───────── 内部 ─────────

        private readonly Dictionary<CSteamID, HSteamNetConnection> _peerToConn = new();
        private readonly Dictionary<HSteamNetConnection, CSteamID> _connToPeer = new();

        /// <summary>NetKey(int) → 类型化分发槽。Subscribe 时以 key 为索引建槽，OnTransportMessage 读出 key 后查表。</summary>
        private readonly Dictionary<int, INetSlot> _slots = new();

        private const int RawKey = 0;
        private const int HeaderSize = 4;
#else
        public event Action RoomJoined;
        public event Action RoomLeft;
        public event Action<object> RoomFailed;
        public event Action<object> PlayerJoined;
        public event Action<object> PlayerLeft;
        public event Action<object, ArraySegment<byte>> MessageReceived;
#endif

        // ═════════════════════════════════════════════════════════════════
        //   IGameService / ITickable
        // ═════════════════════════════════════════════════════════════════

        public void Init(GameContext ctx)
        {
#if !DISABLESTEAMWORKS
            Lobby.Entered += OnLobbyEntered;
            Lobby.Failed += OnLobbyFailed;
            Transport.Connected += OnPeerConnected;
            Transport.Disconnected += OnPeerDisconnected;
            Transport.MessageReceived += OnTransportMessage;
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
            Transport.MessageReceived -= OnTransportMessage;
            Lobby.Shutdown();
            _slots.Clear();
#endif
        }

        public void Tick(float dt) => Transport.Poll();

        // ═════════════════════════════════════════════════════════════════
        //   Room API
        // ═════════════════════════════════════════════════════════════════

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
        // ═════════════════════════════════════════════════════════════════
        //   消息发送
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// 给指定 peer 发 protobuf 消息。零分配：从 ArrayPool 租用 buffer，protobuf 直接序列化进 buffer 的 body 段。
        /// reliable 默认 true（关键事件）。lane 默认 0（走 SendMessageToConnection 快路径）；
        /// 设 lane &gt; 0 前需先 _net.Transport.LaneCount = N（N 必须 &gt; lane）。
        /// </summary>
        public bool Send<T>(CSteamID target, int key, T message, bool reliable = true, int lane = 0) where T : IMessage<T>
        {
            if (message == null) return false;
            if (!_peerToConn.TryGetValue(target, out var conn)) return false;

            int size = message.CalculateSize();
            int total = HeaderSize + size;
            var buffer = ArrayPool<byte>.Shared.Rent(total);
            try
            {
                WriteInt32LE(buffer, 0, key);
                using (var ms = new MemoryStream(buffer, HeaderSize, size))
                using (var cos = new CodedOutputStream(ms))
                {
                    message.WriteTo(cos);
                    cos.Flush();
                }
                return Transport.Send(conn, buffer, 0, total, reliable, lane);
            }
            finally { ArrayPool<byte>.Shared.Return(buffer); }
        }

        /// <summary>广播 protobuf 消息给所有已连通 peer。一次序列化、多次发送。返回成功数量。</summary>
        public int Broadcast<T>(int key, T message, bool reliable = true, int lane = 0) where T : IMessage<T>
        {
            if (message == null || _peerToConn.Count == 0) return 0;

            int size = message.CalculateSize();
            int total = HeaderSize + size;
            var buffer = ArrayPool<byte>.Shared.Rent(total);
            try
            {
                WriteInt32LE(buffer, 0, key);
                using (var ms = new MemoryStream(buffer, HeaderSize, size))
                using (var cos = new CodedOutputStream(ms))
                {
                    message.WriteTo(cos);
                    cos.Flush();
                }
                int sent = 0;
                foreach (var conn in _peerToConn.Values)
                    if (Transport.Send(conn, buffer, 0, total, reliable, lane)) sent++;
                return sent;
            }
            finally { ArrayPool<byte>.Shared.Return(buffer); }
        }

        /// <summary>原始字节通道：发送时打上 key=0 头，对端从 <see cref="MessageReceived"/> 收到内层 payload。</summary>
        public bool Send(CSteamID target, byte[] data, int offset, int length, bool reliable, int lane = 0)
        {
            if (data == null || offset < 0 || length < 0 || offset + length > data.Length) return false;
            if (!_peerToConn.TryGetValue(target, out var conn)) return false;

            int total = HeaderSize + length;
            var buffer = ArrayPool<byte>.Shared.Rent(total);
            try
            {
                WriteInt32LE(buffer, 0, RawKey);
                Buffer.BlockCopy(data, offset, buffer, HeaderSize, length);
                return Transport.Send(conn, buffer, 0, total, reliable, lane);
            }
            finally { ArrayPool<byte>.Shared.Return(buffer); }
        }

        // ═════════════════════════════════════════════════════════════════
        //   消息订阅（独立路由，不走 EventMgr）
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// 注册 key 上 T 类型 protobuf 消息的处理函数。同一 key 上首次注册决定该 key 的消息类型，
        /// 后续以不同类型 Subscribe 会被拒绝并打 LogError。可重复注册同一 handler 不会重复触发。
        /// </summary>
        public void Subscribe<T>(int key, Action<CSteamID, T> handler) where T : IMessage<T>, new()
        {
            if (handler == null) return;
            if (key == RawKey) { Debug.LogError("[Net] NetKey 0 is reserved for raw bytes"); return; }

            if (!_slots.TryGetValue(key, out var slot))
            {
                slot = new NetSlot<T>();
                _slots[key] = slot;
            }
            else if (slot.MessageType != typeof(T))
            {
                Debug.LogError($"[Net] key {key} type mismatch: registered {slot.MessageType.Name}, attempted {typeof(T).Name}");
                return;
            }
            slot.Add(handler);
        }

        /// <summary>取消订阅。最后一个 handler 移除后会回收槽位。</summary>
        public void Unsubscribe<T>(int key, Action<CSteamID, T> handler) where T : IMessage<T>, new()
        {
            if (handler == null) return;
            if (!_slots.TryGetValue(key, out var slot)) return;
            if (slot.MessageType != typeof(T)) return;
            slot.Remove(handler);
            if (slot.IsEmpty) _slots.Remove(key);
        }

        // ═════════════════════════════════════════════════════════════════
        //   事件回调（来自 Lobby / Transport）
        // ═════════════════════════════════════════════════════════════════

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

        /// <summary>解包 4 字节 key 头，根据 key 决定走类型化分发还是 raw 事件。</summary>
        private void OnTransportMessage(HSteamNetConnection conn, ArraySegment<byte> data)
        {
            if (!_connToPeer.TryGetValue(conn, out var peer)) return;
            if (data.Array == null || data.Count < HeaderSize)
            {
                Debug.LogWarning($"[Net] packet malformed ({data.Count}B) from {peer}");
                return;
            }

            int key = ReadInt32LE(data.Array, data.Offset);
            int bodyOffset = data.Offset + HeaderSize;
            int bodyCount = data.Count - HeaderSize;

            if (key == RawKey)
            {
                try { MessageReceived?.Invoke(peer, new ArraySegment<byte>(data.Array, bodyOffset, bodyCount)); }
                catch (Exception ex) { Debug.LogException(ex); }
                return;
            }

            if (_slots.TryGetValue(key, out var slot))
            {
                try { slot.Dispatch(peer, data.Array, bodyOffset, bodyCount); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
            else
            {
                Debug.LogWarning($"[Net] no handler for key {key} ({bodyCount}B) from {peer}");
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //   打包 / 字节序辅助
        // ═════════════════════════════════════════════════════════════════

        private static void WriteInt32LE(byte[] dst, int offset, int value)
        {
            dst[offset]     = (byte)value;
            dst[offset + 1] = (byte)(value >> 8);
            dst[offset + 2] = (byte)(value >> 16);
            dst[offset + 3] = (byte)(value >> 24);
        }

        private static int ReadInt32LE(byte[] src, int offset)
        {
            return src[offset]
                 | (src[offset + 1] << 8)
                 | (src[offset + 2] << 16)
                 | (src[offset + 3] << 24);
        }

        // ═════════════════════════════════════════════════════════════════
        //   分发槽（类型化）
        // ═════════════════════════════════════════════════════════════════

        private interface INetSlot
        {
            Type MessageType { get; }
            bool IsEmpty { get; }
            void Add(Delegate handler);
            void Remove(Delegate handler);
            void Dispatch(CSteamID from, byte[] buffer, int offset, int count);
        }

        private sealed class NetSlot<T> : INetSlot where T : IMessage<T>, new()
        {
            // MessageParser<T> 是 thread-safe 且无状态，按类型缓存一份即可
            private static readonly MessageParser<T> Parser = new MessageParser<T>(() => new T());

            private Action<CSteamID, T> _h;

            public Type MessageType => typeof(T);
            public bool IsEmpty => _h == null;

            public void Add(Delegate d)
            {
                var t = (Action<CSteamID, T>)d;
                if (_h != null && Array.IndexOf(_h.GetInvocationList(), t) >= 0) return;
                _h = (Action<CSteamID, T>)Delegate.Combine(_h, t);
            }

            public void Remove(Delegate d) => _h = (Action<CSteamID, T>)Delegate.Remove(_h, (Action<CSteamID, T>)d);

            public void Dispatch(CSteamID from, byte[] buffer, int offset, int count)
            {
                var msg = Parser.ParseFrom(buffer, offset, count);
                var h = _h;
                if (h == null) return;
                // foreach + 单独 try/catch，避免某个 handler 抛异常导致后续 handler 被跳过
                foreach (var d in h.GetInvocationList())
                {
                    try { ((Action<CSteamID, T>)d)(from, msg); }
                    catch (Exception ex) { Debug.LogException(ex); }
                }
            }
        }
#else
        // ───────── DISABLESTEAMWORKS 平台 stubs（保留 API 表面让业务代码可编译）─────────
        public bool Send<T>(object target, int key, T message, bool reliable = true, int lane = 0) where T : IMessage<T> => false;
        public int Broadcast<T>(int key, T message, bool reliable = true, int lane = 0) where T : IMessage<T> => 0;
        public bool Send(object target, byte[] data, int offset, int length, bool reliable, int lane = 0) => false;
        public void Subscribe<T>(int key, Action<object, T> handler) where T : IMessage<T>, new() { }
        public void Unsubscribe<T>(int key, Action<object, T> handler) where T : IMessage<T>, new() { }
#endif
    }
}
