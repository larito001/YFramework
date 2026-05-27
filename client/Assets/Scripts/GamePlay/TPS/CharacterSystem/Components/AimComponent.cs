using UnityEngine;

/// <summary>
/// 俯视角瞄准 + 朝向（纯逻辑组件，不碰 view）：
///   - 右键按住 → Owner.IsAiming=true，角色 LookAt 鼠标光标在地面的投射点
///   - 右键松开 → Owner.IsAiming=false，角色朝向跟随键盘移动方向（不再面向鼠标）
/// 添加顺序：必须在 MoveComponent 之前 Add，让 MoveComponent 用最新 Rotation 算 local 动画参数。
/// </summary>
public class AimComponent : ICharacterComponent
{
    /// <summary>旋转 lerp 速率（指数收敛）。值越大越紧跟，帧率无关。
    /// 推荐 10~20：12 比较跟手又有缓动，5 偏软，25 接近瞬转。</summary>
    public float RotateLerpRate = 12f;

    private InputService input;
    private CameraManager cameraMgr;

    public override void Attach(Character owner)
    {
        base.Attach(owner);
        var ctx = GameLoop.Instance != null ? GameLoop.Instance.Ctx : null;
        if (ctx == null)
        {
            Debug.LogError("[AimComponent] GameLoop.Ctx 未就绪");
            return;
        }
        ctx.TryGet(out input);
        ctx.TryGet(out cameraMgr);
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
        float t = 1f - Mathf.Exp(-RotateLerpRate * dt);
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
