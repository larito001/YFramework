using UnityEngine;

/// <summary>
/// 俯视角瞄准 + 朝向（纯逻辑组件，不碰 view / 不碰输入源）：
///   - 瞄准（input.AimHeld） → Owner.IsAiming=true，角色面向 <see cref="InputComponentBase.AimWorldPoint"/>（玩家=鼠标投射点；AI 不瞄准）
///   - 非瞄准 → 角色朝 <see cref="InputComponentBase.MoveWorld"/>（移动方向）
/// 相机/鼠标的输入解释已在输入组件做完，本组件只消费世界空间意图。
/// 添加顺序：必须在 MoveComponent 之前、输入组件之后 Add。
/// </summary>
public class AimComponent : ICharacterComponent
{
    /// <summary>非瞄准时的旋转 lerp 速率（朝移动方向）。指数收敛，帧率无关。
    /// 推荐 10~20：12 比较跟手又有缓动，5 偏软，25 接近瞬转。</summary>
    public float RotateLerpRate = 12f;
    /// <summary>瞄准时的旋转 lerp 速率（朝瞄准点）。比非瞄准模式快——瞄准要求精度。
    /// 30 ≈ 95% 到位 5 帧（83ms）；要绝对硬切设 1e6 之类的大数（等价于 t=1）。</summary>
    public float AimRotateLerpRate = 30f;

    private InputComponentBase input;

    public override void Attach(Character owner)
    {
        input = owner.Get<InputComponentBase>();
        if (input == null) Debug.LogWarning("[AimComponent] 找不到 InputComponentBase —— 角色不会转身/瞄准。需在 AimComponent 之前 Add 输入组件。");
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
        base.Detach();
    }

    public override void Tick(float dt)
    {
        if (input == null || Owner == null) return;
        // dt<=0 = 卡肉/局部冻结。冻结期间继续转身会"动画停了还在转"，穿帮。早退。
        if (dt <= 0f) return;
        if (Owner.IsDead)
        {
            Owner.IsAiming = false;
            return;
        }

        Owner.IsAiming = input.AimHeld;

        // 技能释放 / 闪避中锁朝向：Owner.Rotation 保持触发瞬间的值
        // （SkillCastComponent 锁前向做位移；DodgeComponent 保持朝向、用 4 向 clip + 世界向位移表达闪避方向）
        if (Owner.IsBusy) return;

        Vector3 targetDir;
        if (Owner.IsAiming)
        {
            // 瞄准：面向输入组件给的瞄准世界点（同时把它透传给射击/UI 用）
            Owner.AimTargetWorldPos = input.AimWorldPoint;
            targetDir = input.AimWorldPoint - Owner.Position;
            targetDir.y = 0f;
        }
        else
        {
            // 非瞄准：朝移动方向
            targetDir = input.MoveWorld;
            targetDir.y = 0f;
        }

        if (targetDir.sqrMagnitude < 1e-4f) return; // 没有目标方向，朝向沿用上一帧

        var targetRot = Quaternion.LookRotation(targetDir, Vector3.up);
        float rate = Owner.IsAiming ? AimRotateLerpRate : RotateLerpRate;
        float t = 1f - Mathf.Exp(-rate * dt);
        Owner.Rotation = Quaternion.Slerp(Owner.Rotation, targetRot, t);
    }
}
