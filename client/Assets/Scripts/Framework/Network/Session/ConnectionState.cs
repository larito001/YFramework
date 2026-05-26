namespace YOTO.Network
{
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        TimedOut,
    }

    /// 跨 Lobby ↔ Session 边界的握手描述。Lobby 负责产出，Session 消费。
    /// 当前只用 HostPeer，未来需要 endpoint / token 等再扩字段。
    public readonly struct SessionInfo
    {
        public readonly PeerId HostPeer;

        public SessionInfo(PeerId hostPeer) { HostPeer = hostPeer; }
    }
}
