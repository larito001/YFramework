using UnityEngine;

/// <summary>
/// 俯视角瞄准 + 朝向（纯逻辑组件，不碰 view）：
///   - 右键按住 → Owner.IsAiming=true，角色 LookAt 鼠标光标在地面的投射点
///   - 右键松开 → Owner.IsAiming=false，角色朝向跟随键盘移动方向（不再面向鼠标）
/// 添加顺序：必须在 MoveComponent 之前 Add，让 MoveComponent 用最新 Rotation 算 local 动画参数。
/// </summary>
public class AimComponent : ICharacterComponent
{
    /// <summary>非瞄准时的旋转 lerp 速率（朝移动方向）。指数收敛，帧率无关。
    /// 推荐 10~20：12 比较跟手又有缓动，5 偏软，25 接近瞬转。</summary>
    public float RotateLerpRate = 12f;
    /// <summary>瞄准时的旋转 lerp 速率（朝鼠标方向）。比非瞄准模式快——瞄准要求精度，
    /// 慢追会让枪口可见地"跟不上鼠标"，尤其在 strafe BlendTree 期间叠加上半身动画 damp 会更明显。
    /// 30 ≈ 95% 到位 5 帧（83ms）；要绝对硬切设 1e6 之类的大数（等价于 t=1）。</summary>
    public float AimRotateLerpRate = 30f;

    private InputService input;
    private CameraManager cameraMgr;

    public override void Attach(Character owner)
    {
        if (Ctx == null) { Debug.LogError("[AimComponent] GameLoop.Ctx 未就绪"); return; }
        Ctx.TryGet(out input);
        Ctx.TryGet(out cameraMgr);
    }

    public override void Detach()
    {
        // 清自己写过的 Owner 字段，避免 view 在本组件离场后继续读到死值
        if (Owner != null)
        {
            Owner.IsAiming = false;
            Owner.AimTargetWorldPos = Vector3.zero;
        }
        input = null;
        cameraMgr = null;
        base.Detach();
    }

    public override void Tick(float dt)
    {
        if (input == null || Owner == null) return;

        Owner.IsAiming = input.AimHeld;

        // 近战中锁朝向：Owner.Rotation 保持触发瞬间的值，不被鼠标/键盘改变
        if (Owner.IsMeleeing) return;

        Vector3 targetDir = Owner.IsAiming
            ? CalcAimDirection()
            : CalcMoveDirection();

        if (targetDir.sqrMagnitude < 1e-4f) return; // 没有目标方向，朝向沿用上一帧

        var targetRot = Quaternion.LookRotation(targetDir, Vector3.up);
        // 指数 lerp：每帧把当前朝向往目标朝向收 (1 - e^(-rate*dt))，帧率无关，自带 ease-out
        // 瞄准用更高 rate 让枪口紧跟鼠标，非瞄准用低 rate 保留转身缓动质感
        float rate = Owner.IsAiming ? AimRotateLerpRate : RotateLerpRate;
        float t = 1f - Mathf.Exp(-rate * dt);
        Owner.Rotation = Quaternion.Slerp(Owner.Rotation, targetRot, t);
    }

    /// 鼠标 → 主相机射线 → 与枪口高度水平面求交。同时写 Owner.AimTargetWorldPos 给射击/UI 用。
    /// 关键：平面在枪口高度（Position.y + MuzzleHeight），不是脚下。否则倾斜相机下鼠标看着指 A 实际打 B。
    private Vector3 CalcAimDirection()
    {
        var cam = cameraMgr != null ? cameraMgr.MainCamera : Camera.main;
        if (cam == null) return Vector3.zero;

        var ray = cam.ScreenPointToRay(input.MousePosition);
        var planeY = Owner.Position.y + Owner.MuzzleHeight;
        var plane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
        if (!plane.Raycast(ray, out float enter)) return Vector3.zero;

        var hit = ray.GetPoint(enter);
        Owner.AimTargetWorldPos = hit;

        var dir = hit - Owner.Position;
        dir.y = 0f; // 朝向只用水平分量
        return dir;
    }

    /// WASD → 相机基坐标的水平方向。和 MoveComponent 用同样的相机基，保持移动/朝向一致。
    /// 没输入返回零向量，朝向不变。
    private Vector3 CalcMoveDirection()
    {
        var m = input.Move;
        if (m.sqrMagnitude < 1e-4f) return Vector3.zero;

        var camFwd = cameraMgr != null ? cameraMgr.PlanarForward : Vector3.forward;
        var camRight = cameraMgr != null ? cameraMgr.PlanarRight : Vector3.right;
        var dir = camFwd * m.y + camRight * m.x;
        dir.y = 0f;
        return dir;
    }
}
