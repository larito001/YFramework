using System;

namespace YOTO.Network
{
    /// 网络层对外的对端标识。底层是什么 transport（Steam/ENet/...）gameplay 不需要知道。
    public readonly struct PeerId : IEquatable<PeerId>
    {
        public static readonly PeerId None = default;

        public readonly ulong Value;

        public PeerId(ulong value) { Value = value; }

        public bool IsValid => Value != 0;

        public bool Equals(PeerId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PeerId p && Equals(p);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();

        public static bool operator ==(PeerId a, PeerId b) => a.Value == b.Value;
        public static bool operator !=(PeerId a, PeerId b) => a.Value != b.Value;
    }
}
