using UnityEngine;

/// <summary>
/// 僵尸**简单随机 AI**（纯逻辑组件，不读 InputService——它本身就是"大脑"）：
/// 每隔随机时长重选一个动作，随机在 idle / 游荡走 / 奔跑 / 释放技能（攻击 / 飞扑）之间切换。
///
/// 写入 Owner 字段（替代玩家的 Aim/Move 输入驱动）：
///   - <see cref="Character.WishVelocity"/> 的 x/z（移动意图，y 留给 GravityComponent）
///   - <see cref="Character.Rotation"/>（朝移动方向转身）
///   - <see cref="Character.AnimSpeedRatio"/>（locomotion 1D mixer 按真实 m/s blend idle/walk/run）
///   - <see cref="Character.RequestedSkillIndex"/>（请求 <see cref="SkillCastComponent"/> 释放技能）
///
/// **技能释放途中让位**：<see cref="Character.IsCastingSkill"/> 为 true 时本组件早退——移动/朝向/位移全由 SkillCastComponent 接管
/// （技能不可打断）。技能结束后继续随机行为。
///
/// Add 顺序：在 SkillCastComponent **之前**（AI 写 WishVelocity/RequestedSkillIndex，SkillCast 随后消费 + 技能段覆写 x/z），Gravity 之前。
/// </summary>
public class ZombieAIComponent : ICharacterComponent
{
    /// <summary>游荡走速度 (m/s)，对应 CharacterAnimSet 的 Walk 阈值。</summary>
    public float WalkSpeed = 1.5f;
    /// <summary>奔跑速度 (m/s)，对应 Run 阈值。</summary>
    public float RunSpeed = 4f;
    /// <summary>转身 lerp 速率（指数收敛，帧率无关）。</summary>
    public float TurnLerpRate = 6f;
    /// <summary>单个动作最短/最长持续（秒），到点重选。</summary>
    public float MinActionTime = 1.0f;
    public float MaxActionTime = 3.0f;
    /// <summary>攻击 / 飞扑技能在 SkillCastComponent.SkillPaths 里的下标。</summary>
    public int AttackSkillIndex = 0;
    public int LeapSkillIndex = 1;

    private enum Action { Idle, Wander, Run }
    private Action action;
    private float actionTimer;
    private Vector3 heading = Vector3.forward;

    public override void Attach(Character owner)
    {
        base.Attach(owner);
        PickNewAction();
    }

    public override void Detach()
    {
        if (Owner != null)
        {
            Owner.WishVelocity = new Vector3(0f, Owner.WishVelocity.y, 0f);
            Owner.AnimSpeedRatio = 0f;
        }
        base.Detach();
    }

    public override void Tick(float dt)
    {
        if (Owner == null || Owner.IsDead) return;
        // 技能释放途中：移动/朝向/位移交给 SkillCastComponent，AI 等它结束（不可打断）
        if (Owner.IsCastingSkill) return;

        actionTimer -= dt;
        if (actionTimer <= 0f) PickNewAction();

        switch (action)
        {
            case Action.Idle:
                Owner.WishVelocity = new Vector3(0f, Owner.WishVelocity.y, 0f);
                Owner.AnimSpeedRatio = 0f;
                break;
            case Action.Wander:
            case Action.Run:
                float spd = action == Action.Run ? RunSpeed : WalkSpeed;
                FaceHeading(dt);
                Owner.WishVelocity = new Vector3(heading.x * spd, Owner.WishVelocity.y, heading.z * spd);
                Owner.AnimSpeedRatio = spd;
                break;
        }
    }

    private void PickNewAction()
    {
        actionTimer = Random.Range(MinActionTime, MaxActionTime);
        int r = Random.Range(0, 5); // 0 idle / 1 wander / 2 run / 3 attack / 4 leap
        switch (r)
        {
            case 0:
                action = Action.Idle;
                break;
            case 1:
                action = Action.Wander; PickHeading();
                break;
            case 2:
                action = Action.Run; PickHeading();
                break;
            case 3:
                action = Action.Idle;
                Owner.RequestedSkillIndex = AttackSkillIndex; // SkillCast 下一 Tick 消费起技能
                break;
            case 4:
                action = Action.Idle;
                Owner.RequestedSkillIndex = LeapSkillIndex;
                break;
        }
    }

    private void PickHeading()
    {
        float yaw = Random.Range(0f, Mathf.PI * 2f);
        heading = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
    }

    private void FaceHeading(float dt)
    {
        if (heading.sqrMagnitude < 1e-4f) return;
        var target = Quaternion.LookRotation(heading, Vector3.up);
        float t = 1f - Mathf.Exp(-TurnLerpRate * dt);
        Owner.Rotation = Quaternion.Slerp(Owner.Rotation, target, t);
    }
}
