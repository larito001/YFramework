using System;
using System.Collections.Generic;

namespace YOTO.Network
{
    /// Lobby 与 Session 解耦：本接口只管 create/find/join/leave/invite，
    /// 不会自动 Connect Session。gameplay 收到 OnJoined 后自己决定要不要
    /// 用 LobbyInfo.ToSessionInfo() 喂给 INetworkSession.Connect。
    ///
    /// 全部方法 fire-and-forget，结果走事件回流（贴 Register/Unregister 风格）。
    public interface ILobbyService
    {
        bool IsInLobby { get; }
        LobbyInfo? Current { get; }
        IReadOnlyList<LobbyMember> Members { get; }

        event Action<LobbyInfo> Created;
        event Action<LobbyInfo> Joined;
        event Action<LobbyJoinFailure> JoinFailed;
        event Action<LobbyInfo> Left;
        event Action<LobbyInfo, LobbyMember> MemberJoined;
        event Action<LobbyInfo, LobbyMember> MemberLeft;
        event Action<LobbyInfo> InviteReceived;
        event Action<IReadOnlyList<LobbyInfo>> Found;

        void Create(int maxMembers = 4);
        void Find(int maxResults = 50);
        void Join(ulong lobbyId);
        void Leave();
        bool OpenInviteOverlay();

        /// 命令行 / Steam 自动入房（"+connect_lobby <id>"）解析出的 pending lobby id。
        /// gameplay 启动完成、订阅好 Joined/JoinFailed 之后调一次 TryConsumePendingAutoJoin 来触发 Join。
        /// 没有 pending 时返回 false，不做任何事。
        bool TryConsumePendingAutoJoin();
    }
}
