using UnityEngine;

/// <summary>
/// 俯视角第三人称射击移动（纯逻辑组件，不碰 view）：
///   - 基坐标：相机水平 forward / right，WASD 含义随相机偏航自动变化
///   - 重力贴地，不跳。落差靠重力自由下落
///   - 输出 Owner.WishVelocity + 用最新 Owner.Rotation 算 AnimMoveX/Y
/// 朝向（Owner.Rotation）由 AimComponent 负责。本组件只读它来反算 local 动画参数。
/// 没挂 AimComponent 时 Rotation 保持 identity，AnimMoveX/Y 退化成世界 XZ。
/// </summary>
public class MoveComponent : ICharacterComponent
{
    public float WalkSpeed = 3f;
    public float SprintMultiplier = 1.6f;
    /// <summary>瞄准时减速倍率（ADS 标准做法）。0.5 = 瞄准 1.5 m/s 慢走，松开 3 m/s 跑。同时让 walk anim 不被加速到像 sprint。</summary>
    public float AimWalkMultiplier = 0.5f;
    public float Gravity = -20f;
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
        if (Owner.IsAiming) speed *= AimWalkMultiplier; // 瞄准时降速
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

        // 3. 动画参数
        //    Walk（瞄准时 2D BlendTree）：用 Owner.Rotation 反算 local 单位向量喂 MoveX/Y
        //    Run（不瞄准时 1D BlendTree）：用 AnimSpeedRatio 控制 Idle ↔ Run 插值
        var localMove = Quaternion.Inverse(Owner.Rotation) * horizontal;
        float invSpeed = speed > 0.01f ? 1f / speed : 0f;
        Owner.AnimMoveX = localMove.x * invSpeed;
        Owner.AnimMoveY = localMove.z * invSpeed;
        Owner.AnimSpeedRatio = Mathf.Clamp01(horizontal.magnitude / WalkSpeed);
    }
}
