using System;

namespace YOTO.Network
{
    /// 可靠性 / 顺序保证。具体 transport 自己映射到底层 SDK。
    public enum DeliveryMode : byte
    {
        Unreliable = 0,
        UnreliableSequenced = 1,
        Reliable = 2,
        ReliableOrdered = 3,
    }

    /// 一帧 packet。Buffer 由 PacketBufferPool 管理，Length 是有效字节数。
    /// 离开 transport 边界后 dispatcher 负责 Return 回池子。
    public readonly struct IncomingPacket
    {
        public readonly PeerId Sender;
        public readonly byte[] Buffer;
        public readonly int Length;
        public readonly byte Channel;

        public IncomingPacket(PeerId sender, byte[] buffer, int length, byte channel)
        {
            Sender = sender;
            Buffer = buffer;
            Length = length;
            Channel = channel;
        }
    }

    /// Transport 抽象。允许未来替换 Steam / Loopback / ENet / LiteNetLib，gameplay 不需要修改。
    public interface INetworkTransport
    {
        event Action<PeerId> Connected;
        event Action<PeerId> Disconnected;

        void StartServer();

        void Connect(PeerId hostPeer);

        void Disconnect();

        bool Send(PeerId peer, byte[] payload, DeliveryMode mode);

        int Broadcast(byte[] payload, DeliveryMode mode);

        void Poll();

        bool IsServer { get; }

        bool IsConnected { get; }
    }
}
