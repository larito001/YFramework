using UnityEngine;

/// <summary>
/// 伤害公式集中处。所有"基础伤害 → 最终伤害" 的算法走这里：暴击放大、元素抗性折扣、未来的减伤 buff 等。
///
/// **职责边界**：
///   - 只算最终数值，不读写 Owner.CurHealth（那是 HealthComponent 的事）
///   - 不触发副作用（事件 / 飘字 / 卡肉），那些由 HealthComponent / 订阅方处理
///   - 不应用 buff，buff 应用由 HealthComponent 走 BuffComponent.AddBuff
///
/// **公式协议**：所有放大 / 折扣 / 减免效果都按"乘法叠加"：final = base * crit * (1 - resist) * (1 - mitigation) * ...
/// 加法叠加易堆出 0/负值；乘法在每一项独立可调，策划友好。
///
/// **未来扩展点**：
///   - 暴击 roll 在攻击者侧（FireComponent / 技能 / AI）写到 DamageInfo.IsCritical / CritMultiplier，本类只消费不 roll
///   - 元素抗性查 target.Get&lt;ResistComponent&gt;()（未实现），按 DamageInfo.Element 选对应抗性值
///   - 防御 / 韧性 / 易伤 buff 查 target.Get&lt;BuffComponent&gt;() 累乘减伤系数（未实现）
///
/// 为什么 static class 不是 service：无状态纯函数 + 调用极频繁（每次伤害都走），免去 service 取 indirection。
/// 未来需要配置化（公式系数从配表读）再升级 service。
/// </summary>
public static class DamageCalculator
{
    /// <summary>算最终伤害值。target 用于查抗性 / 减伤组件（当前未实现时返回 base * crit）。
    /// 返回保证 &gt;= 0；返回 0 表示完全免疫（HealthComponent 会跳过扣血）。</summary>
    public static float ComputeFinalDamage(in DamageInfo info, Actor target)
    {
        float final = info.Amount;

        // 1. 暴击：CritMultiplier 默认 1.0 = 无放大（即便 IsCritical=true 也安全）
        if (info.IsCritical && info.CritMultiplier > 1f)
            final *= info.CritMultiplier;

        // 2. 元素抗性：未来加 ResistComponent 后接入
        //    float resist = target?.Get<ResistComponent>()?.GetResist(info.Element) ?? 0f;
        //    final *= Mathf.Max(0f, 1f - resist);

        // 3. 减伤 buff：未来加 BuffComponent.GetDamageMitigation() 后接入
        //    float mit = target?.Get<BuffComponent>()?.GetDamageMitigation(info.Element) ?? 0f;
        //    final *= Mathf.Max(0f, 1f - mit);

        return Mathf.Max(0f, final);
    }
}
