# Network Usage

本文只说明业务层如何使用网络框架，不说明内部实现。

## 入口服务

业务代码通常只需要取这几个服务：

```csharp
var ctx = GameLoop.Instance.Ctx;

var lobby = ctx.Get<ILobbyService>();
var session = ctx.Get<INetworkSession>();
var platform = ctx.Get<SteamPlatform>();
```

常用职责：

- `ILobbyService`：建房、找房、进房、退房、邀请、房间成员事件。
- `INetworkSession`：启动主机、连接主机、断开连接、收发 protobuf 消息。
- `SteamPlatform`：读取本地玩家身份，例如 `LocalPeer`、`LocalName`、`IsValid`。

## 注册网络消息

新增网络消息时，在玩法层的 `GameProjectBootstrapper.ConfigureProjectNetwork` 里注册。

```csharp
public static class NetKey
{
    public const int ChatMessage = 300;
    public const int PlayerInput = 100;
}

public static partial class GameBootstrapper
{
    static partial void ConfigureProjectNetwork(MessageRegistry registry)
    {
        registry.Register<ChatMessage>((ushort)NetKey.ChatMessage);
        registry.Register<PlayerInputMessage>((ushort)NetKey.PlayerInput);
    }
}
```

约定：

- 每个消息类型必须有稳定且唯一的 `ushort` id。
- 已上线的消息 id 不要随意改动。
- 消息类型由 `.proto` 生成，不要手改生成出来的 `.cs`。
- 业务层只注册玩法消息，不需要关心底层传输实现。

### 定义和生成消息

1. 在 `client/Proto/Net/` 下加 `.proto`，例如 `chat_message.proto`：

   ```proto
   syntax = "proto3";
   package yoto.net;
   option csharp_namespace = "YOTO.Gameplay.Net";

   message ChatMessage {
     string sender = 1;
     string text = 2;
     int64 timestamp = 3;
   }
   ```

2. 触发代码生成（任选其一）：
   - Unity Editor 菜单：**Tools → Network → Generate Proto**（推荐，跑完会自动 `AssetDatabase.Refresh`）
   - 命令行 / 双击：`client/Proto/gen_net_proto.bat`

3. 生成的 `.cs` 落到 `client/Assets/Scripts/GamePlay/Network/Messages/`，文件名是 `.proto` 文件名的 PascalCase（`chat_message.proto` → `ChatMessage.cs`）。

4. 在 `NetKey` 里加一个 `const int`；在 `GameProjectBootstrapper.ConfigureProjectNetwork` 里 `registry.Register<T>((ushort)NetKey.Xxx)`。

工具链：
- protoc 用仓库自带的 `tools/3rdparty/protobuf/protoc.exe`（libprotoc 3.6.1），对齐 `Assets/Plugins/Google.Protobuf/Google.Protobuf.dll` (3.21.12)；不走外部 PATH 上的 protoc。
- 升级时 protoc 和 runtime dll 必须一起换，否则生成代码可能引用新 runtime 才有的 API。
- 生成器仅 Windows Editor 支持（调 `.bat`）。

## Lobby 流程

### 订阅事件

建议在对象初始化时订阅，在销毁时取消订阅。

```csharp
private ILobbyService _lobby;
private INetworkSession _session;
private SteamPlatform _platform;

private void Start()
{
    var ctx = GameLoop.Instance.Ctx;
    _lobby = ctx.Get<ILobbyService>();
    _session = ctx.Get<INetworkSession>();
    _platform = ctx.Get<SteamPlatform>();

    _lobby.Created += OnLobbyCreated;
    _lobby.Found += OnLobbyFound;
    _lobby.Joined += OnLobbyJoined;
    _lobby.JoinFailed += OnLobbyJoinFailed;
    _lobby.Left += OnLobbyLeft;
    _lobby.MemberJoined += OnLobbyMemberJoined;
    _lobby.MemberLeft += OnLobbyMemberLeft;
    _lobby.InviteReceived += OnLobbyInviteReceived;
}

private void OnDestroy()
{
    if (_lobby == null) return;

    _lobby.Created -= OnLobbyCreated;
    _lobby.Found -= OnLobbyFound;
    _lobby.Joined -= OnLobbyJoined;
    _lobby.JoinFailed -= OnLobbyJoinFailed;
    _lobby.Left -= OnLobbyLeft;
    _lobby.MemberJoined -= OnLobbyMemberJoined;
    _lobby.MemberLeft -= OnLobbyMemberLeft;
    _lobby.InviteReceived -= OnLobbyInviteReceived;
}
```

### 建房

```csharp
_lobby.Create(maxMembers: 4);
```

成功后会回调：

```csharp
private void OnLobbyCreated(LobbyInfo info)
{
    Debug.Log($"Created lobby: {info.Id}");
}
```

真正进入房间时会回调 `Joined`，通常在 `Joined` 里启动 Session。

### 查找房间

```csharp
_lobby.Find(maxResults: 50);
```

结果通过 `Found` 回来：

```csharp
private void OnLobbyFound(IReadOnlyList<LobbyInfo> lobbies)
{
    foreach (var lobby in lobbies)
    {
        Debug.Log($"{lobby.Id} [{lobby.MemberCount}/{lobby.MaxMembers}]");
    }
}
```

### 加入房间

```csharp
_lobby.Join(lobbyId);
```

失败会回调：

```csharp
private void OnLobbyJoinFailed(LobbyJoinFailure failure)
{
    Debug.LogWarning($"Join failed: {failure.LobbyId}, {failure.Reason}");
}
```

### 离开房间

```csharp
_lobby.Leave();
```

收到 `Left` 时建议同步断开 Session：

```csharp
private void OnLobbyLeft(LobbyInfo info)
{
    _session.Disconnect();
}
```

### 邀请覆盖层

```csharp
if (!_lobby.OpenInviteOverlay())
{
    Debug.LogWarning("Not in lobby, cannot open invite overlay.");
}
```

## Session 流程

Lobby 只负责房间。进入房间后，由业务层根据房主决定网络拓扑。

```csharp
private void OnLobbyJoined(LobbyInfo info)
{
    if (info.Owner == _platform.LocalPeer)
    {
        _session.StartServer();
    }
    else
    {
        _session.Connect(info.ToSessionInfo());
    }
}
```

监听连接状态：

```csharp
private void Start()
{
    _session.StateChanged += OnSessionStateChanged;
}

private void OnDestroy()
{
    if (_session != null)
        _session.StateChanged -= OnSessionStateChanged;
}

private void OnSessionStateChanged()
{
    Debug.Log($"Session state: {_session.State}");
}
```

常用状态：

- `Disconnected`：未连接。
- `Connecting`：正在连接主机。
- `Connected`：可以收发消息。
- `TimedOut`：预留状态。

发送业务消息前通常先判断：

```csharp
if (_session.State != ConnectionState.Connected)
{
    return;
}
```

## 自动入房

Steam 命令行可能带有：

```text
+connect_lobby <lobbyId>
```

业务层应在订阅完 Lobby 事件后调用一次：

```csharp
private void Start()
{
    // 先订阅 Joined / JoinFailed 等事件。
    _lobby.Joined += OnLobbyJoined;
    _lobby.JoinFailed += OnLobbyJoinFailed;

    if (_lobby.TryConsumePendingAutoJoin())
    {
        Debug.Log("Consumed pending auto-join.");
    }
}
```

这样可以避免自动入房太快完成，导致业务层错过 `Joined` 回调。

## 接收消息

订阅消息：

```csharp
private void Start()
{
    _session.Subscribe<ChatMessage>(OnChatReceived);
}

private void OnDestroy()
{
    if (_session != null)
        _session.Unsubscribe<ChatMessage>(OnChatReceived);
}

private void OnChatReceived(NetworkContext ctx, ChatMessage msg)
{
    Debug.Log($"from {ctx.Sender}: {msg.Text}");
}
```

`NetworkContext` 常用字段：

- `Sender`：发送方 `PeerId`。
- `IsServer`：当前本机是否是 host。
- `Channel`：业务通道号。

同一个消息类型可以被多个系统订阅。销毁时必须传入同一个 handler 取消订阅。

## 发送消息

### 发给指定 peer

```csharp
_session.Send(peerId, message);
```

指定可靠性和业务通道：

```csharp
_session.Send(peerId, message, DeliveryMode.Unreliable, channel: 1);
```

当前可用可靠性：

- `DeliveryMode.Reliable`
- `DeliveryMode.Unreliable`

### Broadcast

```csharp
_session.Broadcast(message);
```

在当前 Steam 房间模式下：

- host 调用 `Broadcast`：发给所有已连接客户端。
- client 调用 `Broadcast`：发给 host。

如果需要“客户端发一条，全房间都收到”，通常做法是 client 发给 host，host 收到后再广播给其他客户端。

## 完整最小示例

```csharp
using System.Collections.Generic;
using UnityEngine;
using YOTO.Gameplay.Net;
using YOTO.Network;

public sealed class NetworkExample : MonoBehaviour
{
    private ILobbyService _lobby;
    private INetworkSession _session;
    private SteamPlatform _platform;

    private void Start()
    {
        var ctx = GameLoop.Instance.Ctx;
        _lobby = ctx.Get<ILobbyService>();
        _session = ctx.Get<INetworkSession>();
        _platform = ctx.Get<SteamPlatform>();

        _lobby.Joined += OnLobbyJoined;
        _lobby.JoinFailed += OnLobbyJoinFailed;
        _lobby.Left += OnLobbyLeft;
        _lobby.Found += OnLobbyFound;

        _session.StateChanged += OnSessionStateChanged;
        _session.Subscribe<ChatMessage>(OnChatReceived);

        _lobby.TryConsumePendingAutoJoin();
    }

    private void OnDestroy()
    {
        if (_lobby != null)
        {
            _lobby.Joined -= OnLobbyJoined;
            _lobby.JoinFailed -= OnLobbyJoinFailed;
            _lobby.Left -= OnLobbyLeft;
            _lobby.Found -= OnLobbyFound;
        }

        if (_session != null)
        {
            _session.StateChanged -= OnSessionStateChanged;
            _session.Unsubscribe<ChatMessage>(OnChatReceived);
        }
    }

    public void CreateRoom()
    {
        _lobby.Create(4);
    }

    public void FindRooms()
    {
        _lobby.Find();
    }

    public void JoinRoom(ulong lobbyId)
    {
        _lobby.Join(lobbyId);
    }

    public void LeaveRoom()
    {
        _lobby.Leave();
    }

    public void SendChat(string text)
    {
        if (_session.State != ConnectionState.Connected) return;

        var msg = new ChatMessage
        {
            Sender = _platform.LocalName,
            Text = text,
            Timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        _session.Broadcast(msg);
    }

    private void OnLobbyJoined(LobbyInfo info)
    {
        if (info.Owner == _platform.LocalPeer)
            _session.StartServer();
        else
            _session.Connect(info.ToSessionInfo());
    }

    private void OnLobbyJoinFailed(LobbyJoinFailure failure)
    {
        Debug.LogWarning($"Join failed: {failure.Reason}");
    }

    private void OnLobbyLeft(LobbyInfo info)
    {
        _session.Disconnect();
    }

    private void OnLobbyFound(IReadOnlyList<LobbyInfo> lobbies)
    {
        Debug.Log($"Found {lobbies.Count} rooms.");
    }

    private void OnSessionStateChanged()
    {
        Debug.Log($"Session: {_session.State}");
    }

    private void OnChatReceived(NetworkContext ctx, ChatMessage msg)
    {
        Debug.Log($"[{msg.Sender}] {msg.Text}");
    }
}
```

## 推荐使用习惯

- 先订阅 Lobby / Session / Message 事件，再触发 `Create`、`Join`、`TryConsumePendingAutoJoin`。
- 进入 Lobby 后，在 `Joined` 里决定 `StartServer` 或 `Connect`。
- 离开 Lobby 时同步调用 `_session.Disconnect()`。
- 发送消息前检查 `_session.State == ConnectionState.Connected`。
- 每个 `Subscribe<T>` 都要在销毁时用同一个 handler `Unsubscribe<T>`。
- 网络消息 id 只增不乱改，避免线上协议不兼容。
