using System;
using System.Collections.Generic;

namespace YOTO.Network
{
    /// Type ↔ MessageId 显式映射。Bootstrapper 调用 Register&lt;T&gt;(id) 完成配置，
    /// 不走 hash / auto 递增，避免增删消息时悄悄改 wire 兼容性。
    public sealed class MessageRegistry
    {
        private readonly Dictionary<Type, ushort> _typeToId = new();
        private readonly Dictionary<ushort, Type> _idToType = new();

        public void Register<T>(ushort id) => Register(typeof(T), id);

        public void Register(Type type, ushort id)
        {
            if (_typeToId.TryGetValue(type, out var existing))
                throw new InvalidOperationException($"[Net] type {type.Name} already registered as id={existing}");
            if (_idToType.TryGetValue(id, out var other))
                throw new InvalidOperationException($"[Net] id={id} already taken by {other.Name}");
            _typeToId[type] = id;
            _idToType[id] = type;
        }

        public ushort GetId<T>() => GetId(typeof(T));

        public ushort GetId(Type type)
        {
            if (_typeToId.TryGetValue(type, out var id)) return id;
            throw new InvalidOperationException($"[Net] type not registered: {type.Name}");
        }

        public bool TryGetType(ushort id, out Type type) => _idToType.TryGetValue(id, out type);
    }
}
