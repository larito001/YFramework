using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 武器组件（纯逻辑，不碰 view）：
///   - 切枪：监听 InputService.OnWeaponSelect（1~9 数字键）→ Equip(slot)
///   - 射击：每帧把 input.FireHeld 写到 Owner.IsShooting
///   - 近战：监听 InputService.OnMeleeDown（V）→ 置 Owner.MeleeAttack + MeleeType
/// 业务想程序式换武器调 Equip(slot)；想换 melee 类型改 MeleeTypeForKey。
/// </summary>
public class WeaponComponent : ICharacterComponent
{
    public List<Weapon> Weapons = new List<Weapon>();
    public int InitialSlot = 0;
    /// <summary>V 键触发的近战类型：0=Hard 枪托砸，1=Kick 前踢。</summary>
    public int MeleeTypeForKey = 0;

    private InputService input;

    public override void Attach(Character owner)
    {
        base.Attach(owner);
        var ctx = GameLoop.Instance != null ? GameLoop.Instance.Ctx : null;
        if (ctx == null)
        {
            Debug.LogError("[WeaponComponent] GameLoop.Ctx 未就绪");
            return;
        }
        ctx.TryGet(out input);
        if (input != null)
        {
            input.OnMeleeDown += HandleMelee;
            input.OnWeaponSelect += HandleWeaponSelect;
        }
        // 初始装备：不播切枪动画
        EquipInternal(InitialSlot, playAnim: false);
    }

    public override void Detach()
    {
        if (input != null)
        {
            input.OnMeleeDown -= HandleMelee;
            input.OnWeaponSelect -= HandleWeaponSelect;
        }
        base.Detach();
    }

    public override void Tick(float dt)
    {
        if (input == null || Owner == null) return;
        // 必须先瞄准（抬枪）才能开火
        Owner.IsShooting = input.FireHeld && Owner.IsAiming;
    }

    /// <summary>切到指定槽位。槽越界静默忽略，没有武器时也安全。
    /// 实际换枪：只换模型 + 触发 Animator 的 Equipping 过场动画。不切 runtimeAnimatorController。</summary>
    public void Equip(int slot) => EquipInternal(slot, playAnim: true);

    private void EquipInternal(int slot, bool playAnim)
    {
        if (Owner == null) return;
        if (Weapons == null || slot < 0 || slot >= Weapons.Count) return;
        var w = Weapons[slot];
        bool slotChanged = Owner.CurrentWeaponSlot != slot;
        Owner.CurrentWeaponSlot = slot;
        Owner.CurrentWeaponModelPath = w != null ? w.ModelPath : null;
        Owner.CurrentWeaponLocalPosition = w != null ? w.LocalPosition : default;
        Owner.CurrentWeaponLocalEuler = w != null ? w.LocalEuler : default;
        if (playAnim && slotChanged) Owner.WeaponSwap = true;
    }

    private void HandleMelee()
    {
        if (Owner == null) return;
        Owner.MeleeAttack = true;
        Owner.MeleeType = MeleeTypeForKey;
    }

    private void HandleWeaponSelect(int slot) => Equip(slot);
}
