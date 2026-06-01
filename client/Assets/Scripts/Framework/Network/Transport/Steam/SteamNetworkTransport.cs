// #if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
// #define DISABLESTEAMWORKS
// #endif
//
// using System;
// using System.Collections.Generic;
// using System.Runtime.InteropServices;
// using UnityEngine;
// #if !DISABLESTEAMWORKS
// using Steamworks;
// using Steamworks.Data;
// #endif
//
// namespace YOTO.Network
// {
//     /// SteamNetworkingSockets (SDR relay) 实现的 INetworkTransport。
//     /// 拓扑：Server = 监听 socket；Client = 单连到 Server 的 connection。
//     /// 包是 PacketCodec 头 + protobuf payload；每次 SendMessage 一条独立帧。
//     public sealed class SteamNetworkTransport : INetworkTransport, IGameService
//     {
//         public int VirtualPort = 0;
//
//         /// 入站单包最大字节数。超过直接丢弃，防止异常/恶意 peer 触发大分配或 OOM。
//         public int MaxPacketSize = 64 * 1024;
//
//         private readonly SteamPlatform _platform;
//         private readonly MainThreadDispatcher _inbox;
//
//         public event Action<PeerId> Connected;
//         public event Action<PeerId> Disconnected;
//         public event Action<string> StartFailed;
//         public event Action<string> ConnectFailed;
//
//         public bool IsServer => _host != null;
//         public bool IsConnected => _host != null || (_client != null && _client.IsConnected);
//
// #if !DISABLESTEAMWORKS
//         private HostSocket _host;
//         private ClientConn _client;
// #else
//         private object _host, _client;
// #endif
//
//         public SteamNetworkTransport(SteamPlatform platform, MainThreadDispatcher inbox)
//         {
//             _platform = platform;
//             _inbox = inbox;
//         }
//
//         public void Init(GameContext ctx) { }
//
//         public void Shutdown() => Disconnect();
//
//         public bool StartServer()
//         {
// #if !DISABLESTEAMWORKS
//             if (_host != null) return true;
//             // host 和 client 在同一 transport 上共存会让 Send/Broadcast 行为不确定（当前实现 host 分支优先），
//             // 也容易在 Steam 邀请 / 自动 join 切房时静默叠加角色。先强制要求 Disconnect。
//             if (_client != null)
//             {
//                 RaiseStartFailed("transport already in client role; Disconnect() before StartServer()");
//                 return false;
//             }
//             if (!_platform.IsValid)
//             {
//                 RaiseStartFailed("SteamPlatform not valid");
//                 return false;
//             }
//             try
//             {
//                 _host = SteamNetworkingSockets.CreateRelaySocket<HostSocket>(VirtualPort);
//             }
//             catch (Exception e)
//             {
//                 _host = null;
//                 RaiseStartFailed(e.Message);
//                 return false;
//             }
//             if (_host == null)
//             {
//                 RaiseStartFailed("CreateRelaySocket returned null");
//                 return false;
//             }
//             _host.Bind(this);
//             Debug.Log("[Net] SteamTransport.StartServer");
//             return true;
// #else
//             RaiseStartFailed("Steamworks disabled");
//             return false;
// #endif
//         }
//
//         public bool Connect(PeerId hostPeer)
//         {
// #if !DISABLESTEAMWORKS
//             // 与 StartServer 对称：禁止 host/client 角色叠加。
//             if (_host != null)
//             {
//                 RaiseConnectFailed("transport already in host role; Disconnect() before Connect()");
//                 return false;
//             }
//             if (_client != null)
//             {
//                 // 已连接到同一 host：什么都不做，告诉调用方 “已就绪”。
//                 if (_client.IsConnected && _client.HostPeer == hostPeer) return true;
//
//                 // 已连接到不同 host：拒绝。调用方应先 Disconnect。
//                 if (_client.IsConnected)
//                 {
//                     RaiseConnectFailed($"already connected to {_client.HostPeer}, refuse to switch to {hostPeer}");
//                     return false;
//                 }
//
//                 // _client 存在但未 connected：可能是上一次连接失败/远端断开还没清，关掉重建。
//                 try { _client.Close(); } catch (Exception e) { Debug.LogWarning("[Net] stale client.Close: " + e.Message); }
//                 _client = null;
//             }
//             if (!_platform.IsValid)
//             {
//                 RaiseConnectFailed("SteamPlatform not valid");
//                 return false;
//             }
//             try
//             {
//                 var sid = new SteamId { Value = hostPeer.Value };
//                 _client = SteamNetworkingSockets.ConnectRelay<ClientConn>(sid, VirtualPort);
//             }
//             catch (Exception e)
//             {
//                 _client = null;
//                 RaiseConnectFailed(e.Message);
//                 return false;
//             }
//             if (_client == null)
//             {
//                 RaiseConnectFailed("ConnectRelay returned null");
//                 return false;
//             }
//             _client.Bind(this, hostPeer);
//             Debug.Log($"[Net] SteamTransport.Connect → {hostPeer}");
//             return true;
// #else
//             RaiseConnectFailed("Steamworks disabled");
//             return false;
// #endif
//         }
//
//         private void RaiseStartFailed(string reason)
//         {
//             Debug.LogWarning("[Net] SteamTransport.StartServer failed: " + reason);
//             StartFailed?.Invoke(reason);
//         }
//
//         private void RaiseConnectFailed(string reason)
//         {
//             Debug.LogWarning("[Net] SteamTransport.Connect failed: " + reason);
//             ConnectFailed?.Invoke(reason);
//         }
//
//         public void Disconnect()
//         {
// #if !DISABLESTEAMWORKS
//             if (_host != null)
//             {
//                 try { _host.Close(); } catch (Exception e) { Debug.LogWarning("[Net] host.Close: " + e.Message); }
//                 _host = null;
//             }
//             if (_client != null)
//             {
//                 try { _client.Close(); } catch (Exception e) { Debug.LogWarning("[Net] client.Close: " + e.Message); }
//                 _client = null;
//             }
// #endif
//         }
//
//         public bool Send(PeerId peer, byte[] payload, DeliveryMode mode)
//         {
// #if !DISABLESTEAMWORKS
//             var sendType = MapSendType(mode);
//             if (_host != null) return _host.SendTo(peer, payload, sendType);
//             if (_client != null && _client.IsConnected)
//             {
//                 // 星型拓扑：client 只能往 host 发。之前实现忽略 peer 参数静默发到 host，
//                 // 调用方传错对端不会被发现。这里校验，不匹配直接拒绝。
//                 if (peer != _client.HostPeer)
//                 {
//                     Debug.LogWarning($"[Net] client.Send: peer {peer} != host {_client.HostPeer}, rejected");
//                     return false;
//                 }
//                 return _client.Send(payload, sendType);
//             }
//             Debug.LogWarning("[Net] SteamTransport.Send: not connected");
//             return false;
// #else
//             return false;
// #endif
//         }
//
//         public int Broadcast(byte[] payload, DeliveryMode mode)
//         {
// #if !DISABLESTEAMWORKS
//             var sendType = MapSendType(mode);
//             if (_host != null) return _host.SendToAll(payload, sendType);
//             // 客户端 Broadcast 默认转发给 host；P2P 全互连交给业务自己写转发。
//             if (_client != null && _client.IsConnected)
//                 return _client.Send(payload, sendType) ? 1 : 0;
//             return 0;
// #else
//             return 0;
// #endif
//         }
//
//         public void Poll()
//         {
// #if !DISABLESTEAMWORKS
//             _host?.Receive();
//             _client?.Receive();
// #endif
//         }
//
// #if !DISABLESTEAMWORKS
//         private static SendType MapSendType(DeliveryMode mode) => mode switch
//         {
//             DeliveryMode.Unreliable => SendType.Unreliable,
//             DeliveryMode.Reliable => SendType.Reliable,
//             _ => SendType.Reliable,
//         };
//
//         internal void RaiseConnected(PeerId p) => Connected?.Invoke(p);
//         internal void RaiseDisconnected(PeerId p) => Disconnected?.Invoke(p);
//
//         /// ClientConn 调用：远端断开后让 owner 把 _client 清空，否则下次 Connect 看到 stale
//         /// _client != null 会直接 return true，session 进入 Connecting 但不会真的重建连接。
//         internal void OnClientGone(ClientConn c)
//         {
//             if (ReferenceEquals(_client, c)) _client = null;
//         }
//
//         internal void Enqueue(PeerId sender, IntPtr data, int size)
//         {
//             if (size <= 0) return;
//             if (size > MaxPacketSize)
//             {
//                 Debug.LogWarning($"[Net] dropped oversize packet size={size} max={MaxPacketSize} from {sender}");
//                 return;
//             }
//             var buf = PacketBufferPool.Rent(size);
//             Marshal.Copy(data, buf, 0, size);
//             _inbox.Enqueue(new IncomingPacket(sender, buf, size, channel: 0));
//         }
//
//         internal sealed class HostSocket : SocketManager
//         {
//             private SteamNetworkTransport _owner;
//             private readonly Dictionary<ulong, Connection> _byId = new();
//
//             public void Bind(SteamNetworkTransport owner) => _owner = owner;
//
//             public override void OnConnecting(Connection conn, ConnectionInfo info)
//             {
//                 // base 会做 Accept + 把 Connection 加入本 SocketManager 的 pollGroup。
//                 // 之前自己写 conn.Accept() 不调 base，导致连接没进 pollGroup，
//                 // Receive() 走 ReceiveMessagesOnPollGroup 自然取不到任何包 →
//                 // 房主收不到客户端消息（客户端走 ConnectionManager 不依赖 pollGroup，所以反方向正常）。
//                 base.OnConnecting(conn, info);
//                 Debug.Log($"[Net] HostSocket OnConnecting from {info.Identity.SteamId.Value}");
//             }
//
//             public override void OnConnected(Connection conn, ConnectionInfo info)
//             {
//                 base.OnConnected(conn, info);
//                 var sid = info.Identity.SteamId;
//                 _byId[sid.Value] = conn;
//                 Debug.Log($"[Net] HostSocket OnConnected from {sid.Value} (total peers={_byId.Count})");
//                 _owner?.RaiseConnected(new PeerId(sid.Value));
//             }
//
//             public override void OnDisconnected(Connection conn, ConnectionInfo info)
//             {
//                 base.OnDisconnected(conn, info);
//                 var sid = info.Identity.SteamId;
//                 _byId.Remove(sid.Value);
//                 _owner?.RaiseDisconnected(new PeerId(sid.Value));
//             }
//
//             public override void OnMessage(Connection conn, NetIdentity identity, IntPtr data, int size, long msgNum, long recvTime, int channel)
//             {
//                 _owner?.Enqueue(new PeerId(identity.SteamId.Value), data, size);
//             }
//
//             public bool SendTo(PeerId target, byte[] payload, SendType sendType)
//             {
//                 if (!_byId.TryGetValue(target.Value, out var c)) return false;
//                 return c.SendMessage(payload, sendType) == Result.OK;
//             }
//
//             public int SendToAll(byte[] payload, SendType sendType)
//             {
//                 int n = 0;
//                 foreach (var c in _byId.Values)
//                     if (c.SendMessage(payload, sendType) == Result.OK) n++;
//                 return n;
//             }
//         }
//
//         internal sealed class ClientConn : ConnectionManager
//         {
//             private SteamNetworkTransport _owner;
//             public PeerId HostPeer { get; private set; }
//             public bool IsConnected { get; private set; }
//
//             public void Bind(SteamNetworkTransport owner, PeerId hostPeer)
//             {
//                 _owner = owner;
//                 HostPeer = hostPeer;
//             }
//
//             public override void OnConnecting(ConnectionInfo info)
//             {
//                 base.OnConnecting(info);
//                 Debug.Log($"[Net] ClientConn OnConnecting → host {HostPeer}");
//             }
//
//             public override void OnConnected(ConnectionInfo info)
//             {
//                 base.OnConnected(info);
//                 IsConnected = true;
//                 Debug.Log($"[Net] ClientConn OnConnected ← host {HostPeer}");
//                 _owner?.RaiseConnected(HostPeer);
//             }
//
//             public override void OnDisconnected(ConnectionInfo info)
//             {
//                 base.OnDisconnected(info);
//                 IsConnected = false;
//                 var owner = _owner;
//                 _owner?.RaiseDisconnected(HostPeer);
//                 // 通知 owner 释放对自己的引用；不放在 RaiseDisconnected 之前，避免 session 处理事件
//                 // 时再次访问 transport 看到不一致状态。
//                 owner?.OnClientGone(this);
//             }
//
//             public override void OnMessage(IntPtr data, int size, long msgNum, long recvTime, int channel)
//             {
//                 _owner?.Enqueue(HostPeer, data, size);
//             }
//
//             public bool Send(byte[] payload, SendType sendType)
//             {
//                 if (!IsConnected) return false;
//                 return Connection.SendMessage(payload, sendType) == Result.OK;
//             }
//         }
// #endif
//     }
// }
