using System.Collections.Generic;

/// <summary>
/// 单次伤害的所有信息打包。<see cref="DamageRouter"/> / <see cref="HealthComponent.ApplyDamage"/>
/// / <see cref="HealthComponent.OnDamaged"/> 都用这个 struct 流转。
///
/// 字段分两组：
///   **输入**（攻击者填）：Amount / AttackerId / AttackerTeamId / HitstopTier / IsCritical / CritMultiplier / Element / AppliedBuffs
///   **输出**（HealthComponent + DamageCalculator 算后填）：FinalAmount
///
/// 构造方法只覆盖必填的"基础 4 参数"——暴击 / 元素 / buff 是可选扩展，通过字段赋值设：
/// <code>
///   var info = new DamageInfo(Damage, Owner.ID, Owner.TeamId, HitstopTier.Long);
///   info.IsCritical = true;
///   info.CritMultiplier = 1.5f;
///   info.Element = DamageElement.Fire;
///   info.AppliedBuffs = burnBuffs;
///   DamageRouter.ApplyToActor(world, id, in info);
/// </code>
///
/// 详见 ARCHITECTURE.md "伤害系统" 小节。
/// </summary>
public struct DamageInfo
{
    // ── 输入 ──
    /// <summary>基础伤害值（攻击者侧打的"基础 damage"，未经暴击 / 抗性放缩）。&lt;=0 由 HealthComponent 忽略。</summary>
    public float Amount;
    /// <summary>攻击者 actor.ID。-1 = 无主（环境伤害 / DOT 等）。HealthComponent 用它过滤自伤。</summary>
    public int AttackerId;
    /// <summary>攻击者阵营。HealthComponent 按"双方非中立 + 同阵营" 跳过友军伤害。</summary>
    public int AttackerTeamId;
    /// <summary>受击卡肉分级。攻击端在命中瞬间决定。</summary>
    public HitstopTier HitstopTier;

    /// <summary>是否暴击。攻击端 roll 后填（默认 false）。<see cref="DamageCalculator"/> 据此决定是否放大伤害。</summary>
    public bool IsCritical;
    /// <summary>暴击倍率。1.0 = 不放大（即便 IsCritical=true 也安全）；常见 1.5 / 2.0。</summary>
    public float CritMultiplier;
    /// <summary>伤害元素类型。默认 Physical。目标方 ResistComponent 按本字段查抗性折扣（未实现时 calculator 默认 0 抗性）。</summary>
    public DamageElement Element;
    /// <summary>命中时携带应用到目标的 buff 列表。null 或空 = 无 buff。HealthComponent 命中后调 target.Get&lt;BuffComponent&gt;().AddBuff。</summary>
    public List<BuffSpec> AppliedBuffs;

    // ── 输出（calculator + HealthComponent 算后写）──
    /// <summary>最终扣的 HP 数值。<see cref="DamageCalculator.ComputeFinalDamage"/> 算后 HealthComponent 写回。
    /// view 显示飘字应读本字段（含暴击 / 抗性后），不读 Amount。</summary>
    public float FinalAmount;

    public DamageInfo(float amount, int attackerId, int attackerTeamId, HitstopTier hitstopTier = HitstopTier.Long)
    {
        Amount = amount;
        AttackerId = attackerId;
        AttackerTeamId = attackerTeamId;
        HitstopTier = hitstopTier;
        IsCritical = false;
        CritMultiplier = 1f;
        Element = DamageElement.Physical;
        AppliedBuffs = null;
        FinalAmount = 0f;
    }

    /// <summary>从攻击者侧配置的 <see cref="DamageSpec"/> + 运行时上下文（attackerId / teamId）组装。
    /// 内部 roll 暴击：spec.CritChance 命中时 IsCritical=true + CritMultiplier 从 spec 拷贝（&lt;=1 兜底为 1.5）。
    /// 把"配置 + roll" 集中在一处，攻击者侧组件只需配 spec + 调本方法，不再 new + 多行 setter。</summary>
    public static DamageInfo Build(in DamageSpec spec, int attackerId, int attackerTeamId)
    {
        var info = new DamageInfo(spec.BaseDamage, attackerId, attackerTeamId, spec.HitstopTier);
        if (spec.CritChance > 0f && UnityEngine.Random.value < spec.CritChance)
        {
            info.IsCritical = true;
            info.CritMultiplier = spec.CritMultiplier > 1f ? spec.CritMultiplier : 1.5f;
        }
        info.Element = spec.Element;
        info.AppliedBuffs = spec.AppliedBuffs;
        return info;
    }
}
