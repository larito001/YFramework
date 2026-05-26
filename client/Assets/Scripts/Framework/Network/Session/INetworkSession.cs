using System;
using Google.Protobuf;

namespace YOTO.Network
{
    /// Gameplay 唯一接触的网络入口。看不到 Steam / Socket / protobuf / Host/Client 拓扑细节。
    /// 所有方法 fire-and-forget；结果通过事件回流。
    public interface INetworkSession
    {
        ConnectionState State { get; }
        bool IsServer { get; }

        event Action StateChanged;

        void StartServer();
        void Connect(in SessionInfo info);
        void Disconnect();

        void Send<T>(PeerId peer, T message, DeliveryMode mode = DeliveryMode.Reliable, byte channel = 0)
            where T : IMessage<T>;

        void Broadcast<T>(T message, DeliveryMode mode = DeliveryMode.Reliable, byte channel = 0)
            where T : IMessage<T>;

        void Subscribe<T>(Action<NetworkContext, T> handler) where T : IMessage<T>, new();
        void Unsubscribe<T>(Action<NetworkContext, T> handler) where T : IMessage<T>, new();
    }
}
