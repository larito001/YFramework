using UnityEngine;

/// <summary>
/// 俯视角第三人称射击的**水平移动**（纯逻辑组件，不碰 view / 不碰输入源）：
///   - 移动方向：直接用 <see cref="InputComponentBase.MoveWorld"/>（世界空间意图，相机/AI 解释已在输入组件做完）
///   - 三档速度（Walk/Sprint/Aim）+ Acceleration 平滑 magnitude
///   - 只写 Owner.WishVelocity 的 **x/z**，y 由 <see cref="GravityComponent"/> 负责
///   - 同时写 AnimSpeedRatio（非瞄准 1D mixer，真实 m/s）+ AnimMoveX/Y（瞄准 2D strafe mixer）
/// 朝向（Owner.Rotation）由 AimComponent 负责。
/// </summary>
public class MoveComponent : ICharacterComponent
{
    /// <summary>非瞄准走路速度 (m/s)。WASD 默认。</summary>
    public float WalkSpeed = 5f;
    /// <summary>走路动画播放倍率（Animator.speed）。1 = clip 原速。脚步偏快调小，偏慢调大。</summary>
    public float WalkAnimSpeed = 1f;
    /// <summary>非瞄准冲刺速度 (m/s)。Shift 按住。</summary>
    public float SprintSpeed = 7f;
    /// <summary>冲刺动画播放倍率。SprintLoop 内禀大致 3 m/s，body=6 想跟脚 → 2x。</summary>
    public float SprintAnimSpeed = 1.5f;
    /// <summary>瞄准移动速度 (m/s)。瞄准常驻方案下这就是**默认行走速度**（CharacterFactory 覆盖成 4.5）；
    /// 按 Shift 退出瞄准进入冲刺（SprintSpeed）。字段默认 1.5 留给 AI / 不瞄准角色。</summary>
    public float AimSpeed = 1.5f;
    /// <summary>瞄准走路动画播放倍率。Walk clip 内禀大致 1.6 m/s，body=1.5 接近 → ~1x。</summary>
    public float AimAnimSpeed = 1f;
    /// <summary>水平速度加速度 (m/s²)。只平滑 magnitude，方向瞬切——转弯不受影响。
    /// 25 = 0→8 m/s 用 0.32s，slight lerp 体感。</summary>
    public float Acceleration = 25f;

    private InputComponentBase input;
    private Vector3 currentHorizontal; // 平滑后的水平速度

    public override void Attach(Character owner)
    {
        // 从同 Actor 上的输入组件读意图（玩家=InputComponent / AI=AIInputComponent），不直接碰 InputService / 相机
        input = owner.Get<InputComponentBase>();
        if (input == null) Debug.LogWarning("[MoveComponent] 找不到 InputComponentBase —— 角色不会移动。需在 MoveComponent 之前 Add 输入组件。");
    }

    public override void Detach()
    {
        // 清自己写过的 Owner 字段，view 离场后停止位移和动画驱动。
        // 只清 x/z（y 由 GravityComponent 管），动画字段全清。
        if (Owner != null)
        {
            var v = Owner.WishVelocity;
            v.x = 0f;
            v.z = 0f;
            Owner.WishVelocity = v;
            Owner.AnimMoveX = 0f;
            Owner.AnimMoveY = 0f;
            Owner.AnimSpeedRatio = 0f;
        }
        input = null;
        currentHorizontal = Vector3.zero;
        base.Detach();
    }

    public override void Tick(float dt)
    {
        if (input == null || Owner == null) return;
        // 死亡时停止任何水平/动画意图写入，让角色定在死亡位置播倒地动画，不被 WASD 滑动
        if (Owner.IsDead)
        {
            Owner.WishVelocity = Vector3.zero;
            Owner.AnimMoveX = 0f;
            Owner.AnimMoveY = 0f;
            Owner.AnimSpeedRatio = 0f;
            return;
        }

        // 1. 输入组件给的世界空间移动意图 → 期望水平速度（相机/AI 解释已在输入组件做完）
        // 技能释放 / 闪避中锁水平位移：wishHorizontal=0，重力仍照常，currentHorizontal 自然衰减
        // （位移由 SkillCastComponent / DodgeComponent 覆写 WishVelocity.xz）
        Vector3 wishHorizontal = Vector3.zero;
        if (!Owner.IsBusy)
        {
            // MoveWorld 由输入组件保证为世界空间、模 0~1、y=0，这里直接用（不重复归一/压平）
            Vector3 wishDir = input.MoveWorld;
            // 三档：瞄准 → AimSpeed；非瞄准 + Shift → SprintSpeed；非瞄准默认 → WalkSpeed
            float maxSpeed;
            if (Owner.IsAiming) maxSpeed = AimSpeed;
            else if (input.SprintHeld) maxSpeed = SprintSpeed;
            else maxSpeed = WalkSpeed;
            wishHorizontal = wishDir * maxSpeed;
        }

        // 2. 速度平滑：只平滑 magnitude，方向瞬切（转弯不受加速度影响，起步/停步有 lerp）
        float wishMag = wishHorizontal.magnitude;
        float curMag = currentHorizontal.magnitude;
        float newMag = Mathf.MoveTowards(curMag, wishMag, Acceleration * dt);
        Vector3 dir = wishMag > 0.01f
            ? wishHorizontal / wishMag
            : (curMag > 0.01f ? currentHorizontal / curMag : Vector3.zero);
        currentHorizontal = dir * newMag;

        // 3. 写 x/z 到 WishVelocity，保留 y 不动（y 由 GravityComponent 管）
        var v = Owner.WishVelocity;
        v.x = currentHorizontal.x;
        v.z = currentHorizontal.z;
        Owner.WishVelocity = v;

        // 4. 动画参数
        //    AnimMoveX/Y：本地坐标方向 [-1, 1]，给 view 的瞄准 2D strafe CartesianMixerState（按方向选 8 向 clip；非瞄准时不用）
        //    AnimSpeedRatio：**真实水平速度 (m/s)**，不归一化。view 的 LinearMixerState 用真实 m/s threshold 对齐 4 档 clip
        var localMove = Quaternion.Inverse(Owner.Rotation) * currentHorizontal;
        float invAim = AimSpeed > 0.01f ? 1f / AimSpeed : 0f;
        Owner.AnimMoveX = localMove.x * invAim;
        Owner.AnimMoveY = localMove.z * invAim;
        Owner.AnimSpeedRatio = currentHorizontal.magnitude;
    }
}
