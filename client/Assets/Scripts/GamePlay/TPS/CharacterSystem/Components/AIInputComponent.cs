using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// **AI / 僵尸**输入组件：和玩家 <see cref="InputComponent"/> 同一意图面（<see cref="InputComponentBase"/>），
/// 意图由简单 AI 产出而非键鼠。当前是"巡逻 → 侦测 → 追击 → 近身攻击"的状态机（无寻路，直线朝目标）：
///   - **巡逻**：无目标时随机游荡 / 待机。
///   - **侦测**：扫 ActorWorld 找最近敌人（非己方、非中立、有 HealthComponent、未死），进入 <see cref="DetectRange"/> 即锁定。
///   - **追击**：目标在侦测范围内但 &gt; <see cref="AttackRange"/> → MoveWorld 直指目标（走过去）。
///   - **攻击**：≤ AttackRange → 停下 + 释放技能（<see cref="AttackSkillIndex"/>），按 <see cref="AttackCooldown"/> 节流。
/// 只产"输入"（MoveWorld / SprintHeld / OnCastSkill），转身/位移/动画/伤害交给下游 Aim/Move/SkillCast——和玩家同一套管线。
/// AI 不瞄准/不开火（AimHeld / FireHeld 恒 false）。技能释放途中（IsCastingSkill）让位。
///
/// 后续接行为树/状态机只需替换本类的决策逻辑（写 MoveWorld / RaiseCastSkill），下游零改动。
/// </summary>
public class AIInputComponent : InputComponentBase
{
    [Header("侦测 / 攻击")]
    /// <summary>侦测半径（米）：玩家进入此范围开始追击。水平距离。</summary>
    public float DetectRange = 5f;
    /// <summary>攻击半径（米）：进入此范围停下、面向玩家并随机释放技能。水平距离。</summary>
    public float AttackRange = 2f;
    /// <summary>两次攻击之间的最小间隔（秒，技能放完后再算）。避免贴脸时每帧狂刷 Cast。</summary>
    public float AttackCooldown = 0.6f;

    [Header("巡逻")]
    public float MinPatrolTime = 1.0f;
    public float MaxPatrolTime = 3.0f;

    private ActorWorld world;
    private SkillCastComponent skillCast; // 懒解析（AIInput 在 SkillCast 之前 Add，Attach 时还拿不到）
    private float patrolTimer;
    private bool patrolMoving;     // true=游荡走，false=待机
    private Vector3 patrolHeading = Vector3.forward;
    private float attackTimer;     // >0 时不发起新攻击

    // 扫敌 buffer：复用避免每帧 alloc（单线程顺序 Tick，所有 AI 共享）
    private static readonly List<Actor> scanBuf = new List<Actor>(64);

    public override void Attach(Character owner)
    {
        base.Attach(owner);
        Ctx?.TryGet(out world);
        RepickPatrol();
    }

    public override void Detach()
    {
        world = null;
        base.Detach();
    }

    public override void Tick(float dt)
    {
        if (Owner == null || Owner.IsDead)
        {
            MoveWorld = Vector3.zero;
            SprintHeld = false;
            return;
        }
        // 技能释放途中让位：SkillCast 锁全身 + 接管位移，AI 不产生移动意图
        if (Owner.IsCastingSkill)
        {
            MoveWorld = Vector3.zero;
            return;
        }

        if (attackTimer > 0f) attackTimer -= dt;

        var target = FindNearestEnemy(DetectRange);
        if (target != null)
        {
            var to = target.Position - Owner.Position;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist <= AttackRange)
            {
                // 近身：停下、**面向玩家**（复用 aim 朝向路径：AimComponent 会转向 AimWorldPoint），随机放技能
                MoveWorld = Vector3.zero;
                SprintHeld = false;
                AimHeld = true;
                AimWorldPoint = target.Position;
                if (attackTimer <= 0f)
                {
                    if (skillCast == null) skillCast = Owner.Get<SkillCastComponent>();
                    int n = skillCast != null ? skillCast.SkillCount : 0;
                    if (n > 0)
                    {
                        RaiseCastSkill(Random.Range(0, n)); // 随机技能
                        attackTimer = AttackCooldown;
                    }
                }
            }
            else
            {
                // 追击：直线朝目标走（无寻路）；MoveWorld 指向玩家 → AimComponent 自然朝玩家转身
                AimHeld = false;
                MoveWorld = dist > 1e-4f ? to / dist : Vector3.zero;
                SprintHeld = false;
            }
            return;
        }

        // 无目标 → 巡逻
        AimHeld = false;
        patrolTimer -= dt;
        if (patrolTimer <= 0f) RepickPatrol();
        MoveWorld = patrolMoving ? patrolHeading : Vector3.zero;
        SprintHeld = false;
    }

    private void RepickPatrol()
    {
        patrolTimer = Random.Range(MinPatrolTime, MaxPatrolTime);
        patrolMoving = Random.value < 0.6f; // 六成时间在走，四成待机
        if (patrolMoving)
        {
            float yaw = Random.Range(0f, Mathf.PI * 2f);
            patrolHeading = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
        }
    }

    /// <summary>扫 ActorWorld 找最近敌人（水平距离 ≤ range）。筛选：非自己 + 非中立 + 阵营不同 + 有 HealthComponent + 未死。
    /// 复用 TowerTargetingComponent 的同款扫敌模式。</summary>
    private Actor FindNearestEnemy(float range)
    {
        if (world == null) return null;
        scanBuf.Clear();
        world.AppendAll(scanBuf);

        float bestSqr = range * range;
        Actor best = null;
        var selfPos = Owner.Position;
        int selfTeam = Owner.TeamId;

        for (int i = 0; i < scanBuf.Count; i++)
        {
            var a = scanBuf[i];
            if (a == null || a == Owner) continue;
            if (a.TeamId == 0 || a.TeamId == selfTeam) continue;
            if (a.IsDead) continue;
            if (a.Get<HealthComponent>() == null) continue;

            var d = a.Position - selfPos;
            d.y = 0f; // 水平距离
            float sqr = d.x * d.x + d.z * d.z;
            if (sqr < bestSqr) { bestSqr = sqr; best = a; }
        }
        return best;
    }
}
