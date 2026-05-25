using System.Collections;
using UnityEngine;

public interface ICoroutineRunner
{
    Coroutine Run(IEnumerator routine);
    void Stop(Coroutine coroutine);
}

public sealed class CoroutineRunner : MonoBehaviour, ICoroutineRunner
{
    public Coroutine Run(IEnumerator routine) => StartCoroutine(routine);

    public void Stop(Coroutine coroutine)
    {
        if (coroutine != null) StopCoroutine(coroutine);
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
    }
}
