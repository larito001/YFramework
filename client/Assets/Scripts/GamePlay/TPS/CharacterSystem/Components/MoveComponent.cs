using UnityEngine;

/// <summary>
/// 俯视角第三人称射击移动（纯逻辑组件，不碰 view）：
///   - 基坐标：相机水平 forward / right。WASD 含义随相机偏航自动变化。
///   - 重力贴地，不跳。落差靠重力自由下落，地面贴附用小负向速度抑制 isGrounded 抖动。
///   - 朝向：鼠标 → 主相机射线 → 与角色等高水平面投射 → LookAt 那个点。
///   - 输出全部写到 Character：WishVelocity、Rotation（已平滑）、AnimMoveX/Y。
/// </summary>
public class MoveComponent : ICharacterComponent
{
    public float WalkSpeed = 3f;
    public float SprintMultiplier = 1.6f;
    public float Gravity = -20f;
    public float RotateSpeed = 1080f; // deg/s，俯视角瞄准要跟手
    public float GroundStickVelocity = -2f;

    private InputService input;
    private CameraManager cameraMgr;
    private float verticalVelocity;

    public override void Attach(Character owner)
    {
        base.Attach(owner);
        var ctx = GameLoop.Instance != null ? GameLoop.Instance.Ctx : null;
        if (ctx == null)
        {
            Debug.LogError("[MoveComponent] GameLoop.Ctx 未就绪");
            return;
        }
        ctx.TryGet(out input);
        ctx.TryGet(out cameraMgr);
    }

    public override void Tick(float dt)
    {
        if (input == null || Owner == null) return;

        // 1. WASD 投影到相机水平基坐标
        var m = input.Move;
        Vector3 horizontal = Vector3.zero;
        if (m.sqrMagnitude > 1e-4f)
        {
            var camFwd = cameraMgr != null ? cameraMgr.PlanarForward : Vector3.forward;
            var camRight = cameraMgr != null ? cameraMgr.PlanarRight : Vector3.right;
            horizontal = camFwd * m.y + camRight * m.x;
            if (horizontal.sqrMagnitude > 1f) horizontal.Normalize();
        }
        float speed = WalkSpeed * (input.SprintHeld ? SprintMultiplier : 1f);
        horizontal *= speed;

        // 2. 重力（贴地，不跳）
        if (Owner.IsGrounded)
        {
            if (verticalVelocity < 0f) verticalVelocity = GroundStickVelocity;
        }
        else
        {
            verticalVelocity += Gravity * dt;
        }

        Owner.WishVelocity = new Vector3(horizontal.x, verticalVelocity, horizontal.z);

        // 3. 朝向：LookAt 鼠标在地面投射点
        var aimDir = CalcAimDirection();
        if (aimDir.sqrMagnitude > 1e-4f)
        {
            var targetRot = Quaternion.LookRotation(aimDir, Vector3.up);
            Owner.Rotation = Quaternion.RotateTowards(Owner.Rotation, targetRot, RotateSpeed * dt);
        }

        // 4. 动画参数：horizontal → Owner.Rotation local 空间 → 归一化 [-1, 1]
        var localMove = Quaternion.Inverse(Owner.Rotation) * horizontal;
        float invSpeed = speed > 0.01f ? 1f / speed : 0f;
        Owner.AnimMoveX = localMove.x * invSpeed;
        Owner.AnimMoveY = localMove.z * invSpeed;
    }

    /// 鼠标 → 主相机射线 → 与角色等高水平面求交。
    /// 找不到相机或射线打不到平面就返回零向量，朝向沿用上一帧。
    private Vector3 CalcAimDirection()
    {
        var cam = cameraMgr != null ? cameraMgr.MainCamera : Camera.main;
        if (cam == null) return Vector3.zero;

        var ray = cam.ScreenPointToRay(input.MousePosition);
        var plane = new Plane(Vector3.up, new Vector3(0f, Owner.Position.y, 0f));
        if (!plane.Raycast(ray, out float enter)) return Vector3.zero;

        var hit = ray.GetPoint(enter);
        var dir = hit - Owner.Position;
        dir.y = 0f;
        return dir;
    }
}
