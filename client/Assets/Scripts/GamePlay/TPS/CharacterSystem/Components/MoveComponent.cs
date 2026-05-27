using UnityEngine;

/// <summary>
/// 俯视角第三人称射击移动（纯逻辑组件，不碰 view）：
///   - 基坐标：相机水平 forward / right
///   - 输入直接转 WishVelocity（无加速度平滑）
///   - 重力贴地，不跳
///   - 输出 Owner.WishVelocity + AnimMoveX/Y（Walk BlendTree 用）+ AnimSpeedRatio（Sprint BlendTree 用）
/// 朝向（Owner.Rotation）由 AimComponent 负责。
/// </summary>
public class MoveComponent : ICharacterComponent
{
    public float WalkSpeed = 4f;
    public float SprintMultiplier = 2.0f;
    /// <summary>瞄准时减速倍率（ADS 标准做法）。</summary>
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
        // 近战中锁水平位移：horizontal 保持 0，重力仍照常
        Vector3 horizontal = Vector3.zero;
        float maxSpeed = 0f;
        if (!Owner.IsMeleeing)
        {
            var m = input.Move;
            if (m.sqrMagnitude > 1e-4f)
            {
                var camFwd = cameraMgr != null ? cameraMgr.PlanarForward : Vector3.forward;
                var camRight = cameraMgr != null ? cameraMgr.PlanarRight : Vector3.right;
                horizontal = camFwd * m.y + camRight * m.x;
                if (horizontal.sqrMagnitude > 1f) horizontal.Normalize();
            }
            // 瞄准时禁 sprint：Shift 不再触发 SprintMultiplier
            maxSpeed = WalkSpeed;
            if (input.SprintHeld && !Owner.IsAiming) maxSpeed *= SprintMultiplier;
            if (Owner.IsAiming) maxSpeed *= AimWalkMultiplier;
            horizontal *= maxSpeed;
        }

        // 2. 重力
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
        //    Walk（瞄准 2D）：local 单位向量 MoveX/Y
        //    Sprint（不瞄准 1D）：Speed 控制 Idle ↔ SprintLoop 插值
        var localMove = Quaternion.Inverse(Owner.Rotation) * horizontal;
        float invMax = maxSpeed > 0.01f ? 1f / maxSpeed : 0f;
        Owner.AnimMoveX = localMove.x * invMax;
        Owner.AnimMoveY = localMove.z * invMax;
        Owner.AnimSpeedRatio = Mathf.Clamp01(horizontal.magnitude / WalkSpeed);
    }
}
