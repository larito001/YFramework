using System;
using System.Collections.Generic;

namespace YOTO.Network
{
    /// 收发包共享缓冲池。当前所有 Rent/Return 都在主线程调用（Steam 回调走主线程），所以无锁。
    /// 若未来引入后台线程 transport，需要替换成 ConcurrentBag 或加锁。
    public static class PacketBufferPool
    {
        private const int MaxPooled = 32;
        private const int MinSize = 256;
        private static readonly Stack<byte[]> Pool = new();

        public static byte[] Rent(int size)
        {
            while (Pool.Count > 0)
            {
                var b = Pool.Pop();
                if (b.Length >= size) return b;
            }
            return new byte[Math.Max(size, MinSize)];
        }

        public static void Return(byte[] buf)
        {
            if (buf == null) return;
            if (Pool.Count >= MaxPooled) return;
            Pool.Push(buf);
        }
    }
}
