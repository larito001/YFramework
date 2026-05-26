using UnityEngine;

/// <summary>
/// 相机震屏：对一个 Transform 的 localPosition 施加衰减抖动，自身不依赖时间系统，靠外部 Tick 驱动。
/// </summary>
public class CameraShake
{
    private Transform target;
    private Vector3 originalLocalPosition;
    private float duration;
    private float timer;
    private float intensity;
    private bool isShaking;

    public bool IsShaking => isShaking;

    public void Bind(Transform t)
    {
        if (target == t) return;
        StopAndRestore();
        target = t;
    }

    public void Unbind()
    {
        StopAndRestore();
        target = null;
    }

    public void Shake(float duration = 0.3f, float intensity = 0.15f)
    {
        if (target == null) return;

        if (!isShaking)
        {
            originalLocalPosition = target.localPosition;
        }

        this.duration = duration;
        this.timer = duration;
        this.intensity = intensity;
        this.isShaking = true;
    }

    public void Tick(float dt)
    {
        if (!isShaking || target == null) return;

        if (timer > 0f)
        {
            float progress = timer / duration;
            float currentIntensity = intensity * progress;
            Vector3 offset = new Vector3(
                Random.Range(-currentIntensity, currentIntensity),
                Random.Range(-currentIntensity, currentIntensity),
                0f
            );
            target.localPosition = originalLocalPosition + offset;
            timer -= dt;
        }
        else
        {
            target.localPosition = originalLocalPosition;
            isShaking = false;
        }
    }

    private void StopAndRestore()
    {
        if (isShaking && target != null)
        {
            target.localPosition = originalLocalPosition;
        }
        isShaking = false;
    }
}
