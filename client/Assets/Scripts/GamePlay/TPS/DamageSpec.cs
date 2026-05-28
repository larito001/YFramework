using System.Collections.Generic;

/// <summary>
/// 攻击者侧的"伤害配方"——配一份在攻击者组件 / 武器上，命中时调 <see cref="DamageInfo.Build"/> 一行组装 DamageInfo。
///
/// 解决"DamageInfo 字段多 + 每个攻击者组件都要 new + 多行 setter" 的样板代码问题：
/// 所有"造哪种伤害"的配置集中到本 struct，运行时只需补"谁打的"上下文（attackerId / teamId）。
///
/// 配置示例（武器 Factory）：
/// <code>
///   public DamageSpec Damage = new DamageSpec {
///       BaseDamage = 25f,
///       HitstopTier = HitstopTier.Short,
///       CritChance = 0.1f,                 // 10%
///       CritMultiplier = 1.5f,
///       Element = DamageElement.Fire,
///       AppliedBuffs = new List&lt;BuffSpec&gt; { new BuffSpec(BUFF_BURN, 5f) },
///   };
/// </code>
///
/// 命中调用：
/// <code>
///   var info = DamageInfo.Build(in Damage, attackerId, teamId);
///   DamageRouter.ApplyToActor(world, targetId, in info);
/// </code>
///
/// **字段语义对照 DamageInfo**：DamageSpec 是"模板配置"（静态），DamageInfo 是"运行时实例"（含 attacker 上下文 + roll 结果）。
/// 暴击 roll 在 Build 时做，避免散到每个攻击者组件。
/// </summary>
public struct DamageSpec
{
    /// <summary>基础伤害值。0 = 无伤害（仍会走 calculator / OnDamaged 链路，可用于"只触发反馈不扣血" 的命中）。</summary>
    public float BaseDamage;
    /// <summary>命中卡肉分级。默认 Long（重武器手感）；高频武器配 Short。</summary>
    public HitstopTier HitstopTier;
    /// <summary>暴击概率 0~1。0 = 永不暴击；DamageInfo.Build 内部 Random roll 决定是否触发。</summary>
    public float CritChance;
    /// <summary>暴击伤害倍率。&lt;=1 时 Build 兜底为 1.5（避免配置忘填 IsCritical=true 却 multiplier=0 的 bug）。</summary>
    public float CritMultiplier;
    /// <summary>伤害元素属性。默认 Physical（无元素）。</summary>
    public DamageElement Element;
    /// <summary>命中后转交目标的 buff 列表。null / 空 = 不带 buff。HealthComponent.ApplyDamage 命中后调 target.BuffComponent.AddBuff 应用。</summary>
    public List<BuffSpec> AppliedBuffs;
}
