/// <summary>
/// Buff 配方：命中时由 <see cref="DamageInfo.AppliedBuffs"/> 携带，<see cref="HealthComponent.ApplyDamage"/>
/// 命中后转交给目标 <see cref="BuffComponent"/>.AddBuff 应用。
///
/// 当前是占位 struct（BuffId + Duration），buff 系统未实现时 BuffComponent.AddBuff 只 log。
/// 未来 Buff 完整化时本字段扩展为：BuffConfig 引用 / Stack 数 / Caster ID / 自定义参数 dict 等。
/// </summary>
public struct BuffSpec
{
    /// <summary>Buff 配置表 ID。0 = 无效（不会被应用）。</summary>
    public int BuffId;
    /// <summary>持续时长（秒）。&lt;=0 = 永久（直到被覆盖 / 移除）。</summary>
    public float Duration;

    public BuffSpec(int buffId, float duration)
    {
        BuffId = buffId;
        Duration = duration;
    }
}
