using System;
using UnityEngine;

/// <summary>
/// 生命值组件：管 Actor.MaxHealth / CurHealth / IsDead / Die / DeathVariant 五个**Actor 基类字段**，
/// 提供受击/治疗 API + 事件。不 Tick（被动接受调用）。
///
/// **通用组件**：直接继承 IActorComponent，Owner=Actor。可挂在任何 Actor 子类上（Character / 未来的
/// NPC / 可破坏物 / Boss 部位 / Vehicle 等），不再限定 Character。
///
/// **职责边界（重要）**：
///   - 本组件**只做 HP 数学 + 事件广播**，不直接调任何 service / UI / Manager。
///   - 视觉反馈（飘字 / 闪烁 / 溶解）走 view 订阅 OnDamaged / OnDied。
///   - 卡肉触发走 <see cref="HitstopOnDamageComponent"/> 订阅 OnDamaged。
///   - 死亡自动清理走 <see cref="AutoDespawnComponent"/> 订阅 OnDied。
///   不要再往本组件里加 service 依赖——破坏分层。
///
/// 谁可以打它：SkillCastComponent（技能命中窗）/ 子弹 BulletMoveComponent / Hitscan / 任意外部伤害源都通过
/// <c>actor.Get&lt;HealthComponent&gt;()?.ApplyDamage(in info)</c> 统一入口。
///
/// 死亡：CurHealth&lt;=0 时一次性置 IsDead，触发 OnDied 事件。组件不负责销毁 Actor，
/// 由订阅方（AutoDespawnComponent / 关卡逻辑）决定何时清理。
/// </summary>
public class HealthComponent : IActorComponent
{
    /// <summary>初始最大生命值。Attach 时写到 Owner.MaxHealth。</summary>
    public float InitialMaxHealth = 100f;
    /// <summary>Attach 时是否把 Owner.CurHealth 重置为满血。false 用于"切换持有者保留 HP"等场景。</summary>
    public bool ResetOnAttach = true;

    /// <summary>受伤事件 (info)。HUD / 飞字 / 受击闪烁 / 卡肉触发等所有反馈在这订阅。</summary>
    public event Action<DamageInfo> OnDamaged;
    /// <summary>死亡事件 (attackerId)。AutoDespawn / 关卡逻辑 / 击杀计分 / 尸体清理在这订阅。</summary>
    public event Action<int> OnDied;

    public override void Attach(Actor owner)
    {
        base.Attach(owner);
        if (owner == null) return;
        owner.MaxHealth = InitialMaxHealth;
        if (ResetOnAttach) owner.CurHealth = InitialMaxHealth;
        owner.IsDead = false;
    }

    public override void Detach()
    {
        // 清订阅，防止 GC 拖延导致旧订阅者被回调
        OnDamaged = null;
        OnDied = null;
        // 不清 HP/IsDead：那是 Character 持久状态，不是组件"写过的意图字段"
        base.Detach();
    }

    /// <summary>受到伤害。已死 / 非正数伤害 / 友军伤害（双方非中立 + 同阵营）/ 公式算出 final&lt;=0 直接忽略。
    /// 流程：阵营过滤 → DamageCalculator 算 final（暴击 + 抗性 + 减伤）→ 扣 HP → Invoke OnDamaged（info 复制 + FinalAmount 回填）
    /// → 应用 AppliedBuffs 到目标 BuffComponent → 触发死亡链路。</summary>
    public void ApplyDamage(in DamageInfo info)
    {
        if (Owner == null || Owner.IsDead) return;
        if (Owner.IsInvulnerable) return;   // 无敌帧（闪避 / 出生保护等）：完全免伤
        if (info.Amount <= 0f) return;
        // 友军伤害过滤：双方都非中立 + 同阵营 → 跳过。中立（TeamId=0）任何一方都正常扣血。
        if (info.AttackerTeamId != 0 && Owner.TeamId != 0 && info.AttackerTeamId == Owner.TeamId) return;

        // 算最终伤害（含暴击 / 元素抗性 / 减伤 buff，详见 DamageCalculator）
        float final = DamageCalculator.ComputeFinalDamage(in info, Owner);
        if (final <= 0f) return;  // 公式判完全免疫

        Owner.CurHealth = Mathf.Max(0f, Owner.CurHealth - final);

        // 事件传"含 FinalAmount 的 info"——订阅方（view 飘字）读 FinalAmount 才正确显示
        // info 是 in 参数（readonly），先 copy 再写回，原 caller 的 info 不变
        var finalInfo = info;
        finalInfo.FinalAmount = final;
#if UNITY_EDITOR
        Debug.Log($"[Health] actor={Owner.ID} -{final:F0}({(info.IsCritical ? "CRIT" : info.Element.ToString())}) from {info.AttackerId}, hp={Owner.CurHealth:F0}/{Owner.MaxHealth:F0}");
#endif
        OnDamaged?.Invoke(finalInfo);

        // 命中后应用携带 buff（攻击者方武器 / 技能 spec 出的 buff，由目标 BuffComponent 持有）
        if (info.AppliedBuffs != null && info.AppliedBuffs.Count > 0)
        {
            var buffComp = Owner.Get<BuffComponent>();
            if (buffComp != null)
            {
                for (int i = 0; i < info.AppliedBuffs.Count; i++)
                    buffComp.AddBuff(info.AppliedBuffs[i]);
            }
        }

        if (Owner.CurHealth <= 0f && !Owner.IsDead)
        {
            Owner.IsDead = true;
            // 死亡动画 trigger + 随机变体（0=DeathL，1=DeathR）。和 IsDead 同帧写出，view 下一次 LateUpdate 消费。
            Owner.DeathVariant = UnityEngine.Random.Range(0, 2);
            Owner.Die = true;
#if UNITY_EDITOR
            Debug.Log($"[Health] actor={Owner.ID} died by {info.AttackerId} (variant={Owner.DeathVariant})");
#endif
            OnDied?.Invoke(info.AttackerId);
        }
    }

    /// <summary>治疗。已死不复活；超过 MaxHealth 夹回。</summary>
    public void Heal(float amount)
    {
        if (Owner == null || Owner.IsDead) return;
        if (amount <= 0f) return;
        Owner.CurHealth = Mathf.Min(Owner.MaxHealth, Owner.CurHealth + amount);
    }
}
