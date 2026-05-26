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
                var h = Handler;
                if (h == null) return;
                // multicast delegate 的默认调用方式：第一个 handler 抛异常时，后续 handler 都不会执行。
                // 这里手动展开 invocation list，每个订阅者独立 try/catch，避免互相影响。
                var list = h.GetInvocationList();
                if (list.Length == 1)
                {
                    h(ctx, msg);
                    return;
                }
                foreach (var d in list)
                {
                    try { ((Action<NetworkContext, T>)d)(ctx, msg); }
                    catch (Exception e) { Debug.LogError($"[Net] handler<{typeof(T).Name}> threw: {e}"); }
                }
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
            if (handler == null) return;
            var id = _registry.GetId<T>();
            if (_handlers.TryGetValue(id, out var existing))
            {
                if (existing is Entry<T> typed)
                {
                    typed.Handler += handler;
                    return;
                }
                Debug.LogError($"[Net] Subscribe<{typeof(T).Name}>: msgId {id} already bound to incompatible type {existing.GetType().Name}");
                return;
            }
            _handlers[id] = new Entry<T> { Handler = handler, Serializer = _serializer };
        }

        public void Unsubscribe<T>(Action<NetworkContext, T> handler) where T : IMessage<T>, new()
        {
            if (handler == null) return;
            var id = _registry.GetId<T>();
            if (!_handlers.TryGetValue(id, out var existing)) return;
            if (existing is not Entry<T> typed) return;
            typed.Handler -= handler;
            if (typed.Handler == null) _handlers.Remove(id);
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
