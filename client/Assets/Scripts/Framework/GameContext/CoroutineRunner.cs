using System.Collections;
using UnityEngine;

public interface ICoroutineRunner
{
    Coroutine Run(IEnumerator routine);
    void Stop(Coroutine coroutine);

    /// <summary>宿主是否还能启动协程。关闭/销毁阶段(GameRoot 已 inactive)为 false,调用方应改走同步兜底。</summary>
    bool IsAlive { get; }
}

public sealed class CoroutineRunner : MonoBehaviour, IGameService, ICoroutineRunner
{
    public void Init(GameContext ctx) { }
    public void Shutdown() { StopAllCoroutines(); }

    // GameObject 失活(应用退出 / GameLoop.OnDestroy)后 StartCoroutine 会抛 "inactive" 异常,此时返回 false。
    public bool IsAlive => this != null && isActiveAndEnabled;

    public Coroutine Run(IEnumerator routine) => StartCoroutine(routine);
    public void Stop(Coroutine coroutine)
    {
        if (coroutine != null) StopCoroutine(coroutine);
    }
}