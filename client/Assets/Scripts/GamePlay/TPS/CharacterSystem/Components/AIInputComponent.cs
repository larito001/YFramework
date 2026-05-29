using UnityEngine;

/// <summary>
/// **AI / 僵尸**输入组件：和玩家 <see cref="InputComponent"/> 同一意图面（<see cref="InputComponentBase"/>），
/// 但意图由 AI 产出而非键鼠。**目前是随机占位**——定时随机在 idle / 游荡 / 奔跑 / 释放技能 之间切换。
/// 只产"输入"（MoveWorld / SprintHeld / OnCastSkill），下游 Aim/Move/Skill 组件把它翻译成转身/位移/动画/伤害，
/// 跟玩家完全同一套玩法管线。
///
/// 后续接真·行为树/状态机时：替换 <see cref="Pick"/> 的随机逻辑为决策输出（写 MoveWorld 朝目标、按需 RaiseCastSkill），
/// 下游组件零改动。AI 不瞄准/不开火（AimHeld / FireHeld 恒 false）。
/// </summary>
public class AIInputComponent : InputComponentBase
{
    /// <summary>单个动作最短/最长持续（秒），到点重选。</summary>
    public float MinActionTime = 1.0f;
    public float MaxActionTime = 3.0f;
    /// <summary>随机释放技能的下标范围 [0, SkillCount)，应 = SkillCastComponent.SkillPaths 数量。</summary>
    public int SkillCount = 2;

    private enum Act { Idle, Wander, Run }
    private Act act;
    private float timer;
    private Vector3 heading = Vector3.forward;

    public override void Attach(Character owner)
    {
        base.Attach(owner);
        act = Act.Idle;
        timer = Random.Range(MinActionTime, MaxActionTime); // 首次决策延后到第一个 Tick 之后（此时 SkillCast 已订阅）
    }

    public override void Tick(float dt)
    {
        if (Owner == null || Owner.IsDead)
        {
            MoveWorld = Vector3.zero;
            SprintHeld = false;
            return;
        }
        // 技能释放途中让位：SkillCast 锁全身 + 接管位移，AI 不产生移动意图、不重选
        if (Owner.IsCastingSkill)
        {
            MoveWorld = Vector3.zero;
            SprintHeld = false;
            return;
        }

        timer -= dt;
        if (timer <= 0f) Pick();

        switch (act)
        {
            case Act.Idle:
                MoveWorld = Vector3.zero;
                SprintHeld = false;
                break;
            case Act.Wander:
                MoveWorld = heading;
                SprintHeld = false;
                break;
            case Act.Run:
                MoveWorld = heading;
                SprintHeld = true;
                break;
        }
    }

    private void Pick()
    {
        timer = Random.Range(MinActionTime, MaxActionTime);
        int r = Random.Range(0, 5); // 0 idle / 1 wander / 2 run / 3,4 cast
        switch (r)
        {
            case 0:
                act = Act.Idle;
                break;
            case 1:
                act = Act.Wander; PickHeading();
                break;
            case 2:
                act = Act.Run; PickHeading();
                break;
            default: // 3 / 4 → 释放随机技能，原地起手
                act = Act.Idle;
                RaiseCastSkill(Random.Range(0, Mathf.Max(1, SkillCount)));
                break;
        }
    }

    private void PickHeading()
    {
        float yaw = Random.Range(0f, Mathf.PI * 2f);
        heading = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
    }
}
