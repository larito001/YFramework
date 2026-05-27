using UnityEngine;

/// <summary>
/// 重力 + 贴地组件：管 Character.WishVelocity 的 **y 分量**。
/// 横向位移（x/z）由 MoveComponent / MeleeComponent 等负责，本组件只动 y、不读 input。
///
/// 工作流（每帧）：
///   - IsGrounded=true：维持一个小的负速度（GroundStickVelocity），让 CC 紧贴地面、
///     避免在台阶/斜坡上 isGrounded 闪烁。
///   - IsGrounded=false：自由落体，vy += Gravity * dt。
///   - 最后把 vy 写到 Owner.WishVelocity.y，CharacterView.LateUpdate 用 Controller.Move 应用。
///
/// Add 顺序：放在所有写 x/z 的组件（Move / Melee）**之后**，保证 Tick 末 WishVelocity.y 由本组件最终决定。
/// 没有 Move 的"站桩 actor"（Dummy）只需 Add 本组件就能正确落地。
///
/// 后续要做"跳跃"：新建 JumpComponent 在事件回调里设 Character.VerticalVelocityImpulse 字段，
/// 本组件优先消费该字段覆写 vy 即可（暂未实现）。
/// </summary>
public class GravityComponent : ICharacterComponent
{
    /// <summary>重力加速度 (m/s²)，负值表示朝下。</summary>
    public float Gravity = -20f;
    /// <summary>贴地维持的负速度 (m/s)。绝对值越大越"紧贴"，太大会让落地像被吸住。</summary>
    public float GroundStickVelocity = -2f;

    private float verticalVelocity;

    public override void Tick(float dt)
    {
        if (Owner == null) return;
        if (Owner.IsDead) return;

        if (Owner.IsGrounded)
        {
            // 上一帧若是空中下落，落地后只保留一个小的负贴地速度，不带着大下落速度继续撞地
            if (verticalVelocity < 0f) verticalVelocity = GroundStickVelocity;
        }
        else
        {
            verticalVelocity += Gravity * dt;
        }

        var v = Owner.WishVelocity;
        v.y = verticalVelocity;
        Owner.WishVelocity = v;
    }

    public override void Detach()
    {
        // 清自己写过的 y 分量。x/z 由其他组件管。
        if (Owner != null)
        {
            var v = Owner.WishVelocity;
            v.y = 0f;
            Owner.WishVelocity = v;
        }
        verticalVelocity = 0f;
        base.Detach();
    }
}
