using System.Collections.Generic;

namespace YOTO.Network
{
    /// Lobby 在 gameplay 视角的快照。底层是 Steam Lobby 还是别的 matchmaking 服务都被屏蔽掉。
    public readonly struct LobbyInfo
    {
        public readonly ulong Id;
        public readonly PeerId Owner;
        public readonly int MemberCount;
        public readonly int MaxMembers;
        public readonly string Tag;

        public LobbyInfo(ulong id, PeerId owner, int memberCount, int maxMembers, string tag)
        {
            Id = id;
            Owner = owner;
            MemberCount = memberCount;
            MaxMembers = maxMembers;
            Tag = tag;
        }

        public SessionInfo ToSessionInfo() => new SessionInfo(Owner);
    }

    public readonly struct LobbyMember
    {
        public readonly PeerId Peer;
        public readonly string DisplayName;

        public LobbyMember(PeerId peer, string displayName)
        {
            Peer = peer;
            DisplayName = displayName;
        }
    }

    public readonly struct LobbyJoinFailure
    {
        public readonly ulong LobbyId;
        public readonly string Reason;

        public LobbyJoinFailure(ulong lobbyId, string reason)
        {
            LobbyId = lobbyId;
            Reason = reason;
        }
    }
}
