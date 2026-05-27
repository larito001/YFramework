using UnityEngine;

/// <summary>
/// 俯视角瞄准 + 朝向（纯逻辑组件，不碰 view）：
///   - 右键按住 → Owner.IsAiming=true，角色 LookAt 鼠标光标在地面的投射点
///   - 右键松开 → Owner.IsAiming=false，角色朝向跟随键盘移动方向（不再面向鼠标）
/// 添加顺序：必须在 MoveComponent 之前 Add，让 MoveComponent 用最新 Rotation 算 local 动画参数。
/// </summary>
public class AimComponent : ICharacterComponent
{
    public float RotateSpeed = 1080f; // deg/s，俯视角射击要跟手

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

        Vector3 targetDir = Owner.IsAiming
            ? CalcAimDirection()
            : CalcMoveDirection();

        if (targetDir.sqrMagnitude < 1e-4f) return; // 没有目标方向，朝向沿用上一帧

        var targetRot = Quaternion.LookRotation(targetDir, Vector3.up);
        Owner.Rotation = Quaternion.RotateTowards(Owner.Rotation, targetRot, RotateSpeed * dt);
    }

    /// 鼠标 → 主相机射线 → 与角色等高水平面求交。同时写 Owner.AimTargetWorldPos 给射击/UI 用。
    private Vector3 CalcAimDirection()
    {
        var cam = cameraMgr != null ? cameraMgr.MainCamera : Camera.main;
        if (cam == null) return Vector3.zero;

        var ray = cam.ScreenPointToRay(input.MousePosition);
        var plane = new Plane(Vector3.up, new Vector3(0f, Owner.Position.y, 0f));
        if (!plane.Raycast(ray, out float enter)) return Vector3.zero;

        var hit = ray.GetPoint(enter);
        Owner.AimTargetWorldPos = hit;

        var dir = hit - Owner.Position;
        dir.y = 0f;
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
