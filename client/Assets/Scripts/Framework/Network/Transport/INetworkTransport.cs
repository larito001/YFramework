using System;

namespace YOTO.Network
{
    /// 可靠性。当前只暴露底层真正实现的两档；UnreliableSequenced / ReliableOrdered 之前
    /// 都退化成普通 Unreliable / Reliable，留在 enum 里会让调用方误以为有序，删掉。
    /// 真要做有序/序列号通道，得先在 transport 里实现策略再开新成员。
    public enum DeliveryMode : byte
    {
        Unreliable = 0,
        Reliable = 1,
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
    /// StartServer/Connect 返回是否“成功投递到底层”：true 仅表示 socket/connection 已建好或在排队，
    /// 不等于已 Connected。真正的成功靠 Connected 事件，失败靠 StartFailed / ConnectFailed。
    public interface INetworkTransport
    {
        event Action<PeerId> Connected;
        event Action<PeerId> Disconnected;
        event Action<string> StartFailed;
        event Action<string> ConnectFailed;

        bool StartServer();

        bool Connect(PeerId hostPeer);

        void Disconnect();

        bool Send(PeerId peer, byte[] payload, DeliveryMode mode);

        int Broadcast(byte[] payload, DeliveryMode mode);

        void Poll();

        bool IsServer { get; }

        bool IsConnected { get; }
    }
}
