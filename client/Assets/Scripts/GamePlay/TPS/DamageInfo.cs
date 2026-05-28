/// <summary>
/// 单次伤害的所有信息打包。<see cref="DamageRouter"/> / <see cref="HealthComponent.ApplyDamage"/>
/// / <see cref="HealthComponent.OnDamaged"/> 都用这个 struct 流转，避免每加一个反馈参数（卡肉、击退、暴击、命中点…）
/// 就改一遍所有签名。
///
/// 字段约定：
///   - <see cref="Amount"/>：基础伤害值。&lt;=0 由 HealthComponent 直接忽略。
///   - <see cref="AttackerId"/>：攻击者 actor.ID。-1 = 无主（环境伤害 / DOT 等）。HealthComponent 用它过滤自伤。
///   - <see cref="HitstopTier"/>：受击卡肉分级。攻击端在命中瞬间决定（按子弹 / 武器 / 目标类型）。默认 Long。
/// </summary>
public struct DamageInfo
{
    public float Amount;
    public int AttackerId;
    public HitstopTier HitstopTier;

    public DamageInfo(float amount, int attackerId, HitstopTier hitstopTier = HitstopTier.Long)
    {
        Amount = amount;
        AttackerId = attackerId;
        HitstopTier = hitstopTier;
    }
}
