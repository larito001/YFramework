using System;
using UnityEngine;

/// <summary>
/// 角色输入抽象基类：把"输入意图"和"输入来源"解耦。玩法组件（Move/Aim/Weapon/Skill）只读本组件暴露的
/// **世界空间意图**，不直接碰全局 <see cref="InputService"/> / 相机。两个实现：
///   - <see cref="InputComponent"/>（玩家）：唯一消费 InputService + 相机，做 WASD→世界 / 鼠标→世界点 的解释
///   - <see cref="AIInputComponent"/>（AI/僵尸）：同一意图面，由行为树/状态机产出（暂随机占位）
///
/// 玩法组件 Attach 时用 <c>Owner.Get&lt;InputComponentBase&gt;()</c> 拿到挂着的那个实现。
/// Add 顺序必须保证**输入组件在玩法组件之前**（Factory 约定）。
/// </summary>
public abstract class InputComponentBase : ICharacterComponent
{
    /// <summary>世界空间水平移动意图（模 0~1，y=0）。玩家=相机基底转换后的 WASD；AI=目标/游荡方向。</summary>
    public Vector3 MoveWorld { get; protected set; }
    /// <summary>冲刺/快速移动按住。</summary>
    public bool SprintHeld { get; protected set; }
    /// <summary>瞄准按住。</summary>
    public bool AimHeld { get; protected set; }
    /// <summary>开火按住。</summary>
    public bool FireHeld { get; protected set; }
    /// <summary>瞄准世界点（AimHeld 时有效）。玩家=鼠标射线∩枪口高度平面；AI 不瞄准则忽略。</summary>
    public Vector3 AimWorldPoint { get; protected set; }

    /// <summary>释放技能请求（参数=技能下标）。AI→随机下标，<see cref="SkillCastComponent"/> 直接订阅。
    /// 玩家走 <see cref="OnAttack"/> + <see cref="ComboComponent"/>（连招前端），不直接发本事件。</summary>
    public event Action<int> OnCastSkill;
    /// <summary>攻击键事件（连招用，参数=语义按键）。玩家左键→Light、V→Heavy。<see cref="ComboComponent"/> 订阅、按武器连招图路由成具体技能。</summary>
    public event Action<ComboButton> OnAttack;
    /// <summary>换弹请求。<see cref="WeaponComponent"/> 订阅。</summary>
    public event Action OnReload;
    /// <summary>选武器槽请求（0..8）。<see cref="WeaponComponent"/> 订阅。</summary>
    public event Action<int> OnWeaponSelect;

    protected void RaiseCastSkill(int index) => OnCastSkill?.Invoke(index);
    protected void RaiseAttack(ComboButton button) => OnAttack?.Invoke(button);
    protected void RaiseReload() => OnReload?.Invoke();
    protected void RaiseWeaponSelect(int slot) => OnWeaponSelect?.Invoke(slot);

    public override void Detach()
    {
        // 不在此置 null 事件：退订由各订阅者自己 Detach 时 `-=`（拆解按 Add 逆序，订阅者先于输入组件 Detach）。
        // 发布方清订阅者会掩盖"订阅者忘记退订"的泄漏，归属也不清晰，故只清自身意图状态。
        MoveWorld = Vector3.zero;
        SprintHeld = false;
        AimHeld = false;
        FireHeld = false;
        base.Detach();
    }
}
