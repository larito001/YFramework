using UnityEngine;

/// <summary>
/// 俯视角第三人称射击的**水平移动**（纯逻辑组件，不碰 view）：
///   - 基坐标：相机水平 forward / right
///   - 三档速度（Walk/Sprint/Aim）+ Acceleration 平滑 magnitude
///   - 只写 Owner.WishVelocity 的 **x/z**，y 由 <see cref="GravityComponent"/> 负责
///   - 同时写 AnimMoveX/Y（Walk BlendTree）+ AnimSpeedRatio（Sprint BlendTree）+ AnimPlaybackRate
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
    /// <summary>瞄准移动速度 (m/s)。瞄准时 Shift 失效。</summary>
    public float AimSpeed = 1.5f;
    /// <summary>瞄准走路动画播放倍率。Walk clip 内禀大致 1.6 m/s，body=1.5 接近 → ~1x。</summary>
    public float AimAnimSpeed = 1f;
    /// <summary>水平速度加速度 (m/s²)。只平滑 magnitude，方向瞬切——转弯不受影响。
    /// 25 = 0→8 m/s 用 0.32s，slight lerp 体感。</summary>
    public float Acceleration = 25f;

    private InputService input;
    private CameraManager cameraMgr;
    private Vector3 currentHorizontal; // 平滑后的水平速度

    public override void Attach(Character owner)
    {
        if (Ctx == null) { Debug.LogError("[MoveComponent] GameLoop.Ctx 未就绪"); return; }
        Ctx.TryGet(out input);
        Ctx.TryGet(out cameraMgr);
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
            Owner.AnimPlaybackRate = 1f;
        }
        input = null;
        cameraMgr = null;
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
            Owner.AnimPlaybackRate = 1f;
            return;
        }

        // 1. WASD → 期望水平速度（相机基坐标）
        // 近战中锁水平位移：wishHorizontal=0，重力仍照常，currentHorizontal 自然衰减
        Vector3 wishHorizontal = Vector3.zero;
        if (!Owner.IsMeleeing)
        {
            var m = input.Move;
            Vector3 wishDir = Vector3.zero;
            if (m.sqrMagnitude > 1e-4f)
            {
                var camFwd = cameraMgr != null ? cameraMgr.PlanarForward : Vector3.forward;
                var camRight = cameraMgr != null ? cameraMgr.PlanarRight : Vector3.right;
                wishDir = camFwd * m.y + camRight * m.x;
                if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();
            }
            // 三档：瞄准 → AimSpeed；非瞄准 + Shift → SprintSpeed；非瞄准默认 → WalkSpeed
            // 各档同时绑定对应的动画播放倍率，供 view 直接套用
            float maxSpeed;
            float animRate;
            if (Owner.IsAiming) { maxSpeed = AimSpeed; animRate = AimAnimSpeed; }
            else if (input.SprintHeld) { maxSpeed = SprintSpeed; animRate = SprintAnimSpeed; }
            else { maxSpeed = WalkSpeed; animRate = WalkAnimSpeed; }
            wishHorizontal = wishDir * maxSpeed;
            Owner.AnimPlaybackRate = animRate;
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

        // 4. 动画参数（用平滑后的 currentHorizontal，BlendTree 跟随真实速度衰减/爬升）
        //    Walk（瞄准 2D）：localMove / aim 最大速度，全速 = 单位向量
        //    Sprint（不瞄准 1D）：Speed = horiz/WalkSpeed，范围 [0, 2]，对应 Idle/SprintLoop@1x/SprintLoop@2x
        var localMove = Quaternion.Inverse(Owner.Rotation) * currentHorizontal;
        // Walk BlendTree 用 AimSpeed 归一化（只在瞄准时该 BlendTree 才被使用）
        float invAim = AimSpeed > 0.01f ? 1f / AimSpeed : 0f;
        Owner.AnimMoveX = localMove.x * invAim;
        Owner.AnimMoveY = localMove.z * invAim;
        // Sprint BlendTree 用 WalkSpeed 归一化到 [0, 1]，view 用 Anim.speed 再缩放匹配脚步
        //   walk full（horiz=WalkSpeed）   → ratio=1 → BlendTree 满血 SprintLoop + Anim.speed=1
        //   sprint full（horiz=SprintSpeed）→ ratio=clamp 1 → BlendTree 满血 SprintLoop + Anim.speed=SprintSpeed/ref
        Owner.AnimSpeedRatio = WalkSpeed > 0.01f ? Mathf.Clamp01(currentHorizontal.magnitude / WalkSpeed) : 0f;
    }
}
