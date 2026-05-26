using System;

namespace YOTO.Network
{
    /// 线协议头：5 字节 little-endian。
    /// [0..2)  MessageId  (ushort) — MessageRegistry 中的稳定 id
    /// [2..4)  Flags      (ushort) — 预留位
    /// [4..5)  Channel    (byte)   — 业务通道，配合 DeliveryMode 使用
    public struct PacketHeader
    {
        public const int Size = 5;

        public ushort MessageId;
        public ushort Flags;
        public byte Channel;
    }

    public static class PacketCodec
    {
        public static void WriteHeader(byte[] buf, int offset, in PacketHeader h)
        {
            buf[offset + 0] = (byte)(h.MessageId);
            buf[offset + 1] = (byte)(h.MessageId >> 8);
            buf[offset + 2] = (byte)(h.Flags);
            buf[offset + 3] = (byte)(h.Flags >> 8);
            buf[offset + 4] = h.Channel;
        }

        public static bool TryReadHeader(byte[] buf, int offset, int length, out PacketHeader h)
        {
            if (length < PacketHeader.Size)
            {
                h = default;
                return false;
            }
            h = new PacketHeader
            {
                MessageId = (ushort)(buf[offset + 0] | (buf[offset + 1] << 8)),
                Flags = (ushort)(buf[offset + 2] | (buf[offset + 3] << 8)),
                Channel = buf[offset + 4],
            };
            return true;
        }
    }
}
