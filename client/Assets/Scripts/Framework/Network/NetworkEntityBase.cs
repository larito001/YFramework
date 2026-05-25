using Mirror;
using YOTO;

/// <summary>
/// 联网实体基类。SyncVar / Cmd / Rpc 直接写在子类，Mirror Weaver 自动处理。
/// 业务实体（玩家、敌人、子弹等所有需要网络同步的对象）应该继承此类。
/// 通过 Ctx / GetService 访问 GameContext 内的本地服务（UI、Res、Sound 等）。
/// </summary>
public abstract class NetworkEntityBase : NetworkBehaviour
{
    protected GameContext Ctx => GameLoop.Instance != null ? GameLoop.Instance.Ctx : null;

    protected T GetService<T>() where T : class
    {
        var ctx = Ctx;
        return ctx == null ? null : ctx.Get<T>();
    }

    protected bool TryGetService<T>(out T service) where T : class
    {
        var ctx = Ctx;
        if (ctx == null)
        {
            service = null;
            return false;
        }
        return ctx.TryGet(out service);
    }
}
