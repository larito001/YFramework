using System;
using System.Collections.Generic;
using Google.Protobuf;
using UnityEngine;

namespace YOTO.Network
{
    /// 把解码后的 IncomingPacket 路由到 gameplay 注册的 typed handler 上。
    /// 已假定 Process 在主线程调用（由 MainThreadDispatcher 保证）。
    public sealed class MessageDispatcher
    {
        private interface IEntry
        {
            void Invoke(in NetworkContext ctx, byte[] buf, int offset, int length);
        }

        private sealed class Entry<T> : IEntry where T : IMessage<T>, new()
        {
            public Action<NetworkContext, T> Handler;
            public IMessageSerializer Serializer;

            public void Invoke(in NetworkContext ctx, byte[] buf, int offset, int length)
            {
                var msg = Serializer.Deserialize<T>(buf, offset, length);
                Handler?.Invoke(ctx, msg);
            }
        }

        private readonly Dictionary<ushort, IEntry> _handlers = new();
        private readonly MessageRegistry _registry;
        private readonly IMessageSerializer _serializer;

        public MessageDispatcher(MessageRegistry registry, IMessageSerializer serializer)
        {
            _registry = registry;
            _serializer = serializer;
        }

        public void Subscribe<T>(Action<NetworkContext, T> handler) where T : IMessage<T>, new()
        {
            var id = _registry.GetId<T>();
            _handlers[id] = new Entry<T> { Handler = handler, Serializer = _serializer };
        }

        public void Unsubscribe<T>() where T : IMessage<T>
        {
            var id = _registry.GetId<T>();
            _handlers.Remove(id);
        }

        public void Dispatch(in IncomingPacket packet, bool isServer)
        {
            if (!PacketCodec.TryReadHeader(packet.Buffer, 0, packet.Length, out var header))
            {
                Debug.LogWarning($"[Net] packet too small (len={packet.Length}) from {packet.Sender}");
                return;
            }

            if (!_handlers.TryGetValue(header.MessageId, out var entry))
            {
                Debug.LogWarning($"[Net] no handler for msgId={header.MessageId} from {packet.Sender}");
                return;
            }

            var ctx = new NetworkContext(packet.Sender, isServer, header.Channel);
            try
            {
                entry.Invoke(in ctx, packet.Buffer, PacketHeader.Size, packet.Length - PacketHeader.Size);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Net] handler msgId={header.MessageId} threw: {e}");
            }
        }
    }
}
