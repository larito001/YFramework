using System;
using UnityEngine;

/// <summary>
/// 生命值组件：管 Character.MaxHealth / CurHealth / IsDead 三个字段，提供受击/治疗 API + 事件。
/// 不 Tick（被动接受调用）。HP 数据放 Character 上方便 UI / 死亡逻辑直接读，不挂任何 Get 链。
///
/// **职责边界（重要）**：
///   - 本组件**只做 HP 数学 + 事件广播**，不直接调任何 service / UI / Manager。
///   - 视觉反馈（飘字 / 闪烁 / 溶解）走 view 订阅 OnDamaged / OnDied。
///   - 卡肉触发走 <see cref="HitstopOnDamageComponent"/> 订阅 OnDamaged。
///   - 死亡自动清理走 <see cref="AutoDespawnComponent"/> 订阅 OnDied。
///   不要再往本组件里加 service 依赖——破坏分层。
///
/// 谁可以打它：MeleeComponent / 子弹 BulletMoveComponent / Hitscan / 任意外部伤害源都通过
/// <c>actor.Get&lt;HealthComponent&gt;()?.ApplyDamage(in info)</c> 统一入口。
///
/// 死亡：CurHealth&lt;=0 时一次性置 IsDead，触发 OnDied 事件。组件不负责销毁 Actor，
/// 由订阅方（AutoDespawnComponent / 关卡逻辑）决定何时 RemoveCharacter。
/// </summary>
public class HealthComponent : ICharacterComponent
{
    /// <summary>初始最大生命值。Attach 时写到 Owner.MaxHealth。</summary>
    public float InitialMaxHealth = 100f;
    /// <summary>Attach 时是否把 Owner.CurHealth 重置为满血。false 用于"切换持有者保留 HP"等场景。</summary>
    public bool ResetOnAttach = true;

    /// <summary>受伤事件 (info)。HUD / 飞字 / 受击闪烁 / 卡肉触发等所有反馈在这订阅。</summary>
    public event Action<DamageInfo> OnDamaged;
    /// <summary>死亡事件 (attackerId)。AutoDespawn / 关卡逻辑 / 击杀计分 / 尸体清理在这订阅。</summary>
    public event Action<int> OnDied;

    public override void Attach(Character owner)
    {
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

    /// <summary>受到伤害。已死 / 非正数伤害直接忽略。amount 大于剩余 HP 时夹到 0。
    /// 数学 + 事件广播；所有副作用（飘字 / 卡肉 / 清理）由订阅方处理。</summary>
    public void ApplyDamage(in DamageInfo info)
    {
        if (Owner == null || Owner.IsDead) return;
        if (info.Amount <= 0f) return;

        Owner.CurHealth = Mathf.Max(0f, Owner.CurHealth - info.Amount);
        Debug.Log($"[Health] actor={Owner.ID} -{info.Amount} from {info.AttackerId}, hp={Owner.CurHealth:F0}/{Owner.MaxHealth:F0}");
        OnDamaged?.Invoke(info);

        if (Owner.CurHealth <= 0f && !Owner.IsDead)
        {
            Owner.IsDead = true;
            // 死亡动画 trigger + 随机变体（0=DeathL，1=DeathR）。和 IsDead 同帧写出，view 下一次 LateUpdate 消费。
            Owner.DeathVariant = UnityEngine.Random.Range(0, 2);
            Owner.Die = true;
            Debug.Log($"[Health] actor={Owner.ID} died by {info.AttackerId} (variant={Owner.DeathVariant})");
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
