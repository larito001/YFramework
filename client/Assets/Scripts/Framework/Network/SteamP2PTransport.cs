#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
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
    /// </summary>
    public sealed class SteamP2PTransport
    {
#if !DISABLESTEAMWORKS
        public event Action<HSteamNetConnection, CSteamID> Connected;
        public event Action<HSteamNetConnection, CSteamID> Disconnected;
        public event Action<HSteamNetConnection, ArraySegment<byte>> MessageReceived;

        private readonly IntPtr[] _recvBuf = new IntPtr[64];
        private readonly Dictionary<HSteamNetConnection, CSteamID> _peers = new();
        private Callback<SteamNetConnectionStatusChangedCallback_t> _statusCb;
        private HSteamListenSocket _listen = HSteamListenSocket.Invalid;
        private HSteamNetPollGroup _pollGroup = HSteamNetPollGroup.Invalid;

        public void Host(int virtualPort = 0)
        {
            EnsureInfra();
            if (_listen != HSteamListenSocket.Invalid) return;
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
            _peers[conn] = peer;
            return conn;
        }

        public bool Send(HSteamNetConnection conn, byte[] data, int offset, int length, bool reliable)
        {
            if (data == null || offset < 0 || length < 0 || offset + length > data.Length) return false;
            if (!_peers.ContainsKey(conn)) return false;

            int flags = reliable
                ? Constants.k_nSteamNetworkingSend_Reliable
                : Constants.k_nSteamNetworkingSend_Unreliable;

            var gch = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                var ptr = IntPtr.Add(gch.AddrOfPinnedObject(), offset);
                return SteamNetworkingSockets.SendMessageToConnection(
                    conn, ptr, (uint)length, flags, out _) == EResult.k_EResultOK;
            }
            finally { gch.Free(); }
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

                    var buf = new byte[msg.m_cbSize];
                    Marshal.Copy(msg.m_pData, buf, 0, msg.m_cbSize);
                    try { MessageReceived?.Invoke(msg.m_conn, new ArraySegment<byte>(buf)); }
                    catch (Exception ex) { Debug.LogException(ex); }
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

            Debug.Log($"[Transport] OnStatus conn={(uint)conn} {cb.m_eOldState}→{state} " +
                      $"isOurListen={isOurListen} peer={(ulong)cb.m_info.m_identityRemote.GetSteamID()} " +
                      $"endReason={cb.m_info.m_eEndReason} debug={cb.m_info.m_szEndDebug}");

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
        public void Host(int virtualPort = 0) { }
        public object Connect(object peer, int virtualPort = 0) => null;
        public bool Send(object conn, byte[] data, int offset, int length, bool reliable) => false;
        public void Poll() { }
        public void Close() { }
#endif
    }
}
