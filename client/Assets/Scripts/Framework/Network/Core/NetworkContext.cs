namespace YOTO.Network
{
    /// 每条入站消息附带的上下文，传给 gameplay handler。
    public readonly struct NetworkContext
    {
        public readonly PeerId Sender;
        public readonly bool IsServer;
        public readonly byte Channel;

        public NetworkContext(PeerId sender, bool isServer, byte channel)
        {
            Sender = sender;
            IsServer = isServer;
            Channel = channel;
        }
    }
}
