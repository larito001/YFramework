using UnityEngine;

/// <summary>
/// 相机震屏：沿调用方传入的世界方向（一般是射击反方向）做"kickback"风格抖动，
/// 维护一个随时间衰减的 3D offset 向量，**不直接写 Transform**。
/// 由 CameraManager.UpdateFollow 在每帧设完跟随位置之后把 CurrentOffset 加上去。
///
/// 设计原因：以前直接写 target.localPosition 会和 UpdateFollow 的 position 写入打架，
///   且连续 Shake() 时 originalLocalPosition 锁在第一次调用点，跟随会"卡住"。
///   现在拆成"算偏移 + 应用偏移"两步，跟随逻辑不被打断。
/// </summary>
public class CameraShake
{
    private Vector3 direction = Vector3.right; // kickback 方向（世界，归一化），调用方按枪口反向传入
    private float duration;
    private float timer;
    private float intensity;
    private bool isShaking;

    /// <summary>当前帧应该叠加到相机位置上的抖动偏移（世界空间）。Tick 后读取。</summary>
    public Vector3 CurrentOffset { get; private set; }
    public bool IsShaking => isShaking;

    /// <summary>触发一次震屏。direction = 抖动方向（世界，非归一化也行，内部会归一化）；
    /// 一般传 -FireDirection 让相机被"推到"射击反方向。
    /// 同时多次调用会**叠加**到最大值（最近一次的强度/时长/方向生效），适合连发武器每发都调。</summary>
    public void Shake(Vector3 direction, float duration = 0.3f, float intensity = 0.15f)
    {
        // 方向归一化；如果传入零向量退回到默认 right，避免后面 0 向量导致没动
        var dir = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.right;

        if (isShaking)
        {
            // 取较大值避免新一发的弱抖动盖掉前一发未结束的强抖动；方向用最新（最近一发主导）
            this.direction = dir;
            this.duration = Mathf.Max(this.duration, duration);
            this.timer = Mathf.Max(this.timer, duration);
            this.intensity = Mathf.Max(this.intensity, intensity);
        }
        else
        {
            this.direction = dir;
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
            // 幅度 [0..current] 随机：保留"抖动"质感但**只在 kickback 方向上**，不会反向甩到枪口前方
            float mag = Random.Range(0f, current);
            CurrentOffset = direction * mag;
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
