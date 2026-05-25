/// <summary>
/// 框架级网络事件。约定签名一旦确定后续订阅必须一致：
///   ServerStarted / ServerStopped / ClientStarted / ClientStopped → 无参 (Action)
///   ServerSceneChanged / ClientSceneChanged → Action&lt;string sceneName&gt;
/// </summary>
public enum YNetworkEvent
{
    ServerStarted,
    ServerStopped,
    ClientStarted,
    ClientStopped,
    ServerSceneChanged,
    ClientSceneChanged,
}
