using Mirror;
using UnityEngine.SceneManagement;
using YOTO;

public enum YNetworkMode
{
    Offline,
    Server,
    Client,
    Host,
}

/// <summary>
/// Mirror NetworkManager 的项目封装。把生命周期事件转发到 EventMgr，
/// 其它服务通过 ctx.Get&lt;EventMgr&gt;().Add(YNetworkEvent.XXX, ...) 订阅。
/// 该组件挂在每个 Unity Scene 的 NetworkManager GameObject 上（或常驻 GO）。
/// </summary>
public class YNetworkManager : NetworkManager
{
    public new static YNetworkManager singleton => (YNetworkManager)NetworkManager.singleton;

    public YNetworkMode Mode
    {
        get
        {
            if (NetworkServer.active && NetworkClient.active) return YNetworkMode.Host;
            if (NetworkServer.active) return YNetworkMode.Server;
            if (NetworkClient.active) return YNetworkMode.Client;
            return YNetworkMode.Offline;
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        Fire(YNetworkEvent.ServerStarted);
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        Fire(YNetworkEvent.ServerStopped);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        Fire(YNetworkEvent.ClientStarted);
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        Fire(YNetworkEvent.ClientStopped);
    }

    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);
        FireScene(YNetworkEvent.ServerSceneChanged, sceneName);
    }

    public override void OnClientSceneChanged()
    {
        base.OnClientSceneChanged();
        FireScene(YNetworkEvent.ClientSceneChanged, SceneManager.GetActiveScene().name);
    }

    private static void Fire(YNetworkEvent ev)
    {
        if (TryGetEvents(out var events))
        {
            events.Trigger(ev);
        }
    }

    private static void FireScene(YNetworkEvent ev, string sceneName)
    {
        if (TryGetEvents(out var events))
        {
            events.Trigger(ev, sceneName);
        }
    }

    private static bool TryGetEvents(out EventMgr events)
    {
        events = null;
        var ctx = GameLoop.Instance != null ? GameLoop.Instance.Ctx : null;
        return ctx != null && ctx.TryGet(out events);
    }
}
