using System;
using UnityEngine;

/// <summary>
/// 生命值组件：管 Character.MaxHealth / CurHealth / IsDead 三个字段，提供受击/治疗 API + 事件。
/// 不 Tick（被动接受调用）。HP 数据放 Character 上方便 UI / 死亡逻辑直接读，不挂任何 Get 链。
///
/// 谁可以打它：MeleeComponent / 子弹 BulletMoveComponent / Hitscan / 任意外部伤害源都通过
/// <c>actor.Get&lt;HealthComponent&gt;()?.ApplyDamage(amount, attackerId)</c> 统一入口。
///
/// 死亡：CurHealth&lt;=0 时一次性置 IsDead，触发 OnDied 事件。组件不负责销毁 Actor，
/// 由订阅方（关卡逻辑 / CharacterManager 死亡清理）决定何时 RemoveCharacter。
/// </summary>
public class HealthComponent : ICharacterComponent
{
    /// <summary>初始最大生命值。Attach 时写到 Owner.MaxHealth。</summary>
    public float InitialMaxHealth = 100f;
    /// <summary>Attach 时是否把 Owner.CurHealth 重置为满血。false 用于"切换持有者保留 HP"等场景。</summary>
    public bool ResetOnAttach = true;

    /// <summary>受伤事件 (amount, attackerId)。HUD / 飞字 / 受击反馈在这订阅。</summary>
    public event Action<float, int> OnDamaged;
    /// <summary>死亡事件 (attackerId)。关卡逻辑 / 击杀计分 / 尸体清理在这订阅。</summary>
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

    /// <summary>受到伤害。已死 / 非正数伤害直接忽略。amount 大于剩余 HP 时夹到 0。</summary>
    public void ApplyDamage(float amount, int attackerId)
    {
        if (Owner == null || Owner.IsDead) return;
        if (amount <= 0f) return;

        Owner.CurHealth = Mathf.Max(0f, Owner.CurHealth - amount);
        Debug.Log($"[Health] actor={Owner.ID} -{amount} from {attackerId}, hp={Owner.CurHealth:F0}/{Owner.MaxHealth:F0}");
        OnDamaged?.Invoke(amount, attackerId);

        if (Owner.CurHealth <= 0f && !Owner.IsDead)
        {
            Owner.IsDead = true;
            Debug.Log($"[Health] actor={Owner.ID} died by {attackerId}");
            OnDied?.Invoke(attackerId);
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
