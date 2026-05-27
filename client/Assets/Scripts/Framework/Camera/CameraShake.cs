using UnityEngine;

/// <summary>
/// 相机震屏：维护一个随时间衰减的随机 offset 向量，**不直接写 Transform**。
/// 由 CameraManager.UpdateFollow 在每帧设完跟随位置之后把 CurrentOffset 加上去。
///
/// 设计原因：以前直接写 target.localPosition 会和 UpdateFollow 的 position 写入打架，
///   且连续 Shake() 时 originalLocalPosition 锁在第一次调用点，跟随会"卡住"。
///   现在拆成"算偏移 + 应用偏移"两步，跟随逻辑不被打断。
/// </summary>
public class CameraShake
{
    private float duration;
    private float timer;
    private float intensity;
    private bool isShaking;

    /// <summary>当前帧应该叠加到相机位置上的抖动偏移。Tick 后读取。</summary>
    public Vector3 CurrentOffset { get; private set; }
    public bool IsShaking => isShaking;

    /// <summary>触发一次震屏。同时多次调用会**叠加**到最大值（最近一次的强度和剩余时长生效）。
    /// 适合连发武器：每发都调，强度和时长以最新一发为准。</summary>
    public void Shake(float duration = 0.3f, float intensity = 0.15f)
    {
        // 取较大值，避免新一发的弱抖动盖掉前一发未结束的强抖动
        if (isShaking)
        {
            this.duration = Mathf.Max(this.duration, duration);
            this.timer = Mathf.Max(this.timer, duration);
            this.intensity = Mathf.Max(this.intensity, intensity);
        }
        else
        {
            this.duration = duration;
            this.timer = duration;
            this.intensity = intensity;
            this.isShaking = true;
        }
    }

    public void Tick(float dt)
    {
        if (!isShaking)
        {
            CurrentOffset = Vector3.zero;
            return;
        }

        if (timer > 0f)
        {
            float progress = timer / Mathf.Max(duration, 0.0001f);  // 1 → 0 衰减
            float current = intensity * progress;
            CurrentOffset = new Vector3(
                Random.Range(-current, current),
                Random.Range(-current, current),
                0f
            );
            timer -= dt;
        }
        else
        {
            CurrentOffset = Vector3.zero;
            isShaking = false;
        }
    }

    // ── 保留兼容旧 API ──（Bind/Unbind 现在是 no-op，外部不再依赖 target）
    public void Bind(Transform t) { /* no-op: 不再持有 Transform */ }
    public void Unbind() { /* no-op */ }
}
