using System;
using System.Collections.Concurrent;

namespace YOTO.Network
{
    /// 收包队列。Transport 收到 packet 后 Enqueue，NetworkRuntime.Tick 中 Process 在主线程逐条派发。
    /// 即使当前 SteamTransport 的回调本身就在主线程，这层抽象也确保未来换成多线程 transport 时
    /// gameplay handler 仍然只在主线程被调用。
    public sealed class MainThreadDispatcher
    {
        public delegate void IncomingHandler(in IncomingPacket packet);

        /// 每帧最多处理多少条入站包，避免突发流量把单帧打爆。剩余的留到下一帧。
        public int MaxPacketsPerFrame = 256;

        private readonly ConcurrentQueue<IncomingPacket> _queue = new();
        private IncomingHandler _handler;

        public void SetHandler(IncomingHandler handler) => _handler = handler;

        public void Enqueue(in IncomingPacket packet) => _queue.Enqueue(packet);

        public void Process()
        {
            var handler = _handler;
            if (handler == null)
            {
                while (_queue.TryDequeue(out var drop)) PacketBufferPool.Return(drop.Buffer);
                return;
            }

            int budget = MaxPacketsPerFrame;
            while (budget-- > 0 && _queue.TryDequeue(out var pkt))
            {
                try { handler(in pkt); }
                catch (Exception e) { UnityEngine.Debug.LogError($"[Net] dispatcher: {e}"); }
                finally { PacketBufferPool.Return(pkt.Buffer); }
            }
        }
    }
}
