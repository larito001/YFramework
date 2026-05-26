namespace YOTO.Network
{
    /// 网络层主时钟。Tick 顺序：transport.Poll 把 socket 回调灌进 MainThreadDispatcher
    /// 队列，然后 dispatcher.Process 在主线程把消息分发给 gameplay handler。
    public sealed class NetworkRuntime : IGameService, ITickable
    {
        private readonly INetworkTransport _transport;
        private readonly MainThreadDispatcher _dispatcher;

        public NetworkRuntime(INetworkTransport transport, MainThreadDispatcher dispatcher)
        {
            _transport = transport;
            _dispatcher = dispatcher;
        }

        public void Init(GameContext ctx) { }

        public void Shutdown() { }

        public void Tick(float dt)
        {
            _transport.Poll();
            _dispatcher.Process();
        }
    }
}
