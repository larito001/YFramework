#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace YOTO.Net
{
    /// <summary>
    /// Steam P2P 传输（基于 SteamNetworkingSockets + Steam relay）。
    /// 前置：SteamManager 已 Init，SteamAPI.RunCallbacks 每帧被驱动。
    ///
    /// 多路复用（lane）：
    ///   - 设置 <see cref="LaneCount"/> &gt; 1 后，新建/已有连接都会 ConfigureConnectionLanes。
    ///   - <see cref="Send"/> 的 lane=0 走 SendMessageToConnection 快路径（默认）。
    ///   - lane &gt; 0 走 SendMessages，配合 SteamNetworkingMessage_t.m_idxLane。
    ///     不同 lane 的 reliable 消息互不阻塞，可分别配置优先级 / 带宽权重。
    /// </summary>
    public sealed class SteamP2PTransport
    {
        /// <summary>状态变更日志开关。生产关掉减少噪声；排错时打开。</summary>
        public bool VerboseLog = false;

#if !DISABLESTEAMWORKS
        public event Action<HSteamNetConnection, CSteamID> Connected;
        public event Action<HSteamNetConnection, CSteamID> Disconnected;
        public event Action<HSteamNetConnection, ArraySegment<byte>> MessageReceived;

        private readonly IntPtr[] _recvBuf = new IntPtr[64];
        private readonly Dictionary<HSteamNetConnection, CSteamID> _peers = new();

        // SendMessages 复用的单条数组，避免每次发包都新建。
        // 非线程安全：所有 Send 调用必须在主线程，与 Steam callback 串行。
        private readonly IntPtr[] _sendMsgs = new IntPtr[1];
        private readonly long[]   _sendResults = new long[1];

        private Callback<SteamNetConnectionStatusChangedCallback_t> _statusCb;
        private HSteamListenSocket _listen = HSteamListenSocket.Invalid;
        private HSteamNetPollGroup _pollGroup = HSteamNetPollGroup.Invalid;

        private int _laneCount = 1;

        /// <summary>
        /// 通道数（&gt;= 1）。改变时会立即对所有现存连接应用 ConfigureConnectionLanes，
        /// 新连接在 Connect/Accept 后也会自动应用。优先级/权重用 Steam 默认值。
        /// </summary>
        public int LaneCount
        {
            get => _laneCount;
            set
            {
                int v = Math.Max(1, value);
                if (v == _laneCount) return;
                _laneCount = v;
                foreach (var conn in _peers.Keys)
                    SteamNetworkingSockets.ConfigureConnectionLanes(conn, _laneCount, null, null);
            }
        }

        public void Host(int virtualPort = 0)
        {
            EnsureInfra();
            if (_listen != HSteamListenSocket.Invalid)
            {
                Debug.LogWarning($"[Transport] Host called but already listening on {(ulong)_listen}; ignored");
                return;
            }
            SteamNetworkingUtils.InitRelayNetworkAccess();
            _listen = SteamNetworkingSockets.CreateListenSocketP2P(virtualPort, 0, null);
            Debug.Log($"[Transport] Host → listen={(ulong)_listen} vport={virtualPort}");
        }

        public HSteamNetConnection Connect(CSteamID peer, int virtualPort = 0)
        {
            EnsureInfra();
            SteamNetworkingUtils.InitRelayNetworkAccess();

            var identity = default(SteamNetworkingIdentity);
            identity.SetSteamID(peer);
            var conn = SteamNetworkingSockets.ConnectP2P(ref identity, virtualPort, 0, null);
            if (conn == HSteamNetConnection.Invalid)
            {
                Debug.LogError($"[Transport] ConnectP2P({(ulong)peer}) returned Invalid handle");
                return conn;
            }
            Debug.Log($"[Transport] ConnectP2P → peer={(ulong)peer} conn={(uint)conn}");
            SteamNetworkingSockets.SetConnectionPollGroup(conn, _pollGroup);
            if (_laneCount > 1)
                SteamNetworkingSockets.ConfigureConnectionLanes(conn, _laneCount, null, null);
            _peers[conn] = peer;
            return conn;
        }

        /// <summary>
        /// 发送字节。lane=0（默认）走 SendMessageToConnection，零额外分配（GCHandle pin 即用）。
        /// lane&gt;0 走 SendMessages，会额外用 SteamNetworkingUtils.AllocateMessage 拿一段非托管内存
        /// 并 Marshal.Copy 进去（Steam 自己负责释放）。
        /// </summary>
        public bool Send(HSteamNetConnection conn, byte[] data, int offset, int length, bool reliable, int lane = 0)
        {
            if (data == null || offset < 0 || length < 0 || offset + length > data.Length) return false;
            if (!_peers.ContainsKey(conn)) return false;
            if (lane < 0 || lane >= _laneCount)
            {
                Debug.LogError($"[Transport] lane {lane} out of range (LaneCount={_laneCount})");
                return false;
            }

            int flags = reliable
                ? Constants.k_nSteamNetworkingSend_Reliable
                : Constants.k_nSteamNetworkingSend_Unreliable;

            if (lane == 0) return SendOnLane0(conn, data, offset, length, flags);
            return SendOnLaneN(conn, data, offset, length, flags, lane);
        }

        private static bool SendOnLane0(HSteamNetConnection conn, byte[] data, int offset, int length, int flags)
        {
            var gch = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                var ptr = IntPtr.Add(gch.AddrOfPinnedObject(), offset);
                return SteamNetworkingSockets.SendMessageToConnection(
                    conn, ptr, (uint)length, flags, out _) == EResult.k_EResultOK;
            }
            finally { gch.Free(); }
        }

        private bool SendOnLaneN(HSteamNetConnection conn, byte[] data, int offset, int length, int flags, int lane)
        {
            IntPtr msgPtr = SteamNetworkingUtils.AllocateMessage(length);
            if (msgPtr == IntPtr.Zero) return false;

            // 取出预分配好的 message 头（含已指向缓冲区的 m_pData 与析构函数指针），写入数据后整体写回
            var m = SteamNetworkingMessage_t.FromIntPtr(msgPtr);
            Marshal.Copy(data, offset, m.m_pData, length);
            m.m_conn = conn;
            m.m_nFlags = flags;
            m.m_idxLane = (ushort)lane;
            Marshal.StructureToPtr(m, msgPtr, false);

            _sendMsgs[0] = msgPtr;
            _sendResults[0] = 0;
            SteamNetworkingSockets.SendMessages(1, _sendMsgs, _sendResults);
            // 失败时 results[0] = -EResult，所有权回到调用方，必须 Release 否则泄漏；
            // 成功时是 message number（>0），Steam 接管所有权。
            if (_sendResults[0] > 0) return true;
            SteamNetworkingMessage_t.Release(msgPtr);
            return false;
        }

        public void Poll()
        {
            if (_pollGroup == HSteamNetPollGroup.Invalid) return;
            int n = SteamNetworkingSockets.ReceiveMessagesOnPollGroup(_pollGroup, _recvBuf, _recvBuf.Length);
            for (int i = 0; i < n; i++)
            {
                var ptr = _recvBuf[i];
                try
                {
                    var msg = SteamNetworkingMessage_t.FromIntPtr(ptr);
                    if (msg.m_cbSize <= 0 || !_peers.ContainsKey(msg.m_conn)) continue;

                    // ArrayPool 替代 new byte[] —— 上层 MessageReceived 同步处理完即归还，
                    // 文档已要求 segment 仅回调期间有效。
                    var buf = ArrayPool<byte>.Shared.Rent(msg.m_cbSize);
                    try
                    {
                        Marshal.Copy(msg.m_pData, buf, 0, msg.m_cbSize);
                        try { MessageReceived?.Invoke(msg.m_conn, new ArraySegment<byte>(buf, 0, msg.m_cbSize)); }
                        catch (Exception ex) { Debug.LogException(ex); }
                    }
                    finally { ArrayPool<byte>.Shared.Return(buf); }
                }
                finally { SteamNetworkingMessage_t.Release(ptr); }
            }
        }

        public void Close()
        {
            _statusCb?.Dispose();
            _statusCb = null;

            foreach (var conn in _peers.Keys)
                SteamNetworkingSockets.CloseConnection(conn, 0, "Close", false);
            _peers.Clear();

            if (_listen != HSteamListenSocket.Invalid)
            {
                SteamNetworkingSockets.CloseListenSocket(_listen);
                _listen = HSteamListenSocket.Invalid;
            }
            if (_pollGroup != HSteamNetPollGroup.Invalid)
            {
                SteamNetworkingSockets.DestroyPollGroup(_pollGroup);
                _pollGroup = HSteamNetPollGroup.Invalid;
            }
        }

        private void EnsureInfra()
        {
            _statusCb ??= Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnStatus);
            if (_pollGroup == HSteamNetPollGroup.Invalid)
                _pollGroup = SteamNetworkingSockets.CreatePollGroup();
        }

        private void OnStatus(SteamNetConnectionStatusChangedCallback_t cb)
        {
            var conn = cb.m_hConn;
            var state = cb.m_info.m_eState;
            bool isOurListen = _listen != HSteamListenSocket.Invalid
                               && cb.m_info.m_hListenSocket == _listen;

            if (VerboseLog || state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally)
            {
                Debug.Log($"[Transport] OnStatus conn={(uint)conn} {cb.m_eOldState}→{state} " +
                          $"isOurListen={isOurListen} peer={(ulong)cb.m_info.m_identityRemote.GetSteamID()} " +
                          $"endReason={cb.m_info.m_eEndReason} debug={cb.m_info.m_szEndDebug}");
            }

            switch (state)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                    if (isOurListen && !_peers.ContainsKey(conn))
                    {
                        if (SteamNetworkingSockets.AcceptConnection(conn) != EResult.k_EResultOK)
                        {
                            SteamNetworkingSockets.CloseConnection(conn, 0, "Accept failed", false);
                            return;
                        }
                        SteamNetworkingSockets.SetConnectionPollGroup(conn, _pollGroup);
                        if (_laneCount > 1)
                            SteamNetworkingSockets.ConfigureConnectionLanes(conn, _laneCount, null, null);
                        _peers[conn] = cb.m_info.m_identityRemote.GetSteamID();
                    }
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                    if (_peers.TryGetValue(conn, out var peer))
                    {
                        try { Connected?.Invoke(conn, peer); }
                        catch (Exception ex) { Debug.LogException(ex); }
                    }
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                    if (_peers.TryGetValue(conn, out var lostPeer))
                    {
                        _peers.Remove(conn);
                        try { Disconnected?.Invoke(conn, lostPeer); }
                        catch (Exception ex) { Debug.LogException(ex); }
                    }
                    SteamNetworkingSockets.CloseConnection(conn, 0, "Terminal", false);
                    break;
            }
        }
#else
        public event Action<object, object> Connected;
        public event Action<object, object> Disconnected;
        public event Action<object, ArraySegment<byte>> MessageReceived;
        public int LaneCount { get; set; } = 1;
        public void Host(int virtualPort = 0) { }
        public object Connect(object peer, int virtualPort = 0) => null;
        public bool Send(object conn, byte[] data, int offset, int length, bool reliable, int lane = 0) => false;
        public void Poll() { }
        public void Close() { }
#endif
    }
}
