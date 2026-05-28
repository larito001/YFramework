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
    /// <summary>死亡后多久自动删除 Character（秒）。&lt;=0 关闭自动清理（适合留尸体的场景）。
    /// 默认 3s = 倒地动画播完 + 留个停顿让玩家看清。</summary>
    public float AutoRemoveDelay = 3f;

    private CharacterManager characterMgr;
    private FlyTextMgr flyTextMgr;
    private float removeTimer;

    /// <summary>飘字相对脚下 Position.y 的偏移（米）。1.8 ≈ 头顶上方一点点，俯视角下不会被身体挡。</summary>
    public float FlyTextHeight = 1.8f;

    private TimeScaleService timeScaleService;

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
        Ctx?.TryGet(out characterMgr);
        Ctx?.TryGet(out flyTextMgr);
        Ctx?.TryGet(out timeScaleService);
    }

    public override void Detach()
    {
        // 清订阅，防止 GC 拖延导致旧订阅者被回调
        OnDamaged = null;
        OnDied = null;
        characterMgr = null;
        flyTextMgr = null;
        timeScaleService = null;
        removeTimer = 0f;
        // 不清 HP/IsDead：那是 Character 持久状态，不是组件"写过的意图字段"
        base.Detach();
    }

    /// <summary>死亡后倒计时清理。挂在 Character.Tick 里跑，到点叫 CharacterManager 走 deferred remove。</summary>
    public override void Tick(float dt)
    {
        if (Owner == null || !Owner.IsDead || AutoRemoveDelay <= 0f) return;
        if (removeTimer <= 0f) return; // 没在跑（死亡瞬间会被 ApplyDamage 启动）
        removeTimer -= dt;
        if (removeTimer <= 0f)
        {
            removeTimer = 0f;
            characterMgr?.RemoveCharacter(Owner);
        }
    }

    /// <summary>受到伤害。已死 / 非正数伤害直接忽略。amount 大于剩余 HP 时夹到 0。
    /// hitstopTier：本次受击的卡肉分级，由攻击端在命中瞬间根据子弹/目标类型决定。默认 Long。</summary>
    public void ApplyDamage(float amount, int attackerId, HitstopTier hitstopTier = HitstopTier.Long)
    {
        if (Owner == null || Owner.IsDead) return;
        if (amount <= 0f) return;

        Owner.CurHealth = Mathf.Max(0f, Owner.CurHealth - amount);
        Debug.Log($"[Health] actor={Owner.ID} -{amount} from {attackerId}, hp={Owner.CurHealth:F0}/{Owner.MaxHealth:F0}");
        // 飘字：受击位置（头顶上方）弹个伤害数字。Quick 类型在 FlyTextCtrl 里是红色 + 弹性曲线，正好当"-X HP"动效。
        flyTextMgr?.AddText($"-{Mathf.RoundToInt(amount)}", Owner.Position + Vector3.up * FlyTextHeight, FlyTextType.Quick);
        // 卡肉：tier=None 时跳过；Short/Long 在 service 查表后转 Hitstop。具体 (duration, scale) 在 TimeScaleService 上配。
        timeScaleService?.HitstopByTier(Owner.ID, hitstopTier);
        OnDamaged?.Invoke(amount, attackerId);

        if (Owner.CurHealth <= 0f && !Owner.IsDead)
        {
            Owner.IsDead = true;
            // 死亡动画 trigger + 随机变体（0=DeathL，1=DeathR）。和 IsDead 同帧写出，view 下一次 LateUpdate 消费。
            Owner.DeathVariant = UnityEngine.Random.Range(0, 2);
            Owner.Die = true;
            if (AutoRemoveDelay > 0f) removeTimer = AutoRemoveDelay; // 启动倒计时清理
            Debug.Log($"[Health] actor={Owner.ID} died by {attackerId} (variant={Owner.DeathVariant})");
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
