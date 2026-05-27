using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 持枪人组件（Character 侧，纯逻辑）：
///   - 持有武器槽位 Weapon Actor 列表，Attach 时交给 WeaponManager 接管（注册 + 创建 view）
///   - 切枪：监听 InputService.OnWeaponSelect（1~9 数字键）→ Equip(slot) → weaponMgr.Mount/Unmount + 计时锁开火
///   - 射击：每帧把 input.FireHeld 写到 Owner.IsShooting（开火行为由 Weapon 的 FireComponent 自己消费）
///
/// 近战完全由 <see cref="MeleeComponent"/> 接管：V 键订阅、MeleeAttack/MeleeType/IsMeleeing 写入、
/// swing 时长、前冲、命中、伤害结算、清回 IsMeleeing。本组件只在 Tick 里**读** IsMeleeing 做开火门控，
/// 不写入也不清除近战字段。
///
/// 字段写入：FireOrigin / FireDirection / FireTarget / FireIntent → currentWeapon。
/// IsShooting / IsSwapping / WeaponSwap / CurrentWeaponSlot → Owner。
/// </summary>
public class WeaponComponent : ICharacterComponent
{
    public List<Weapon> Weapons = new List<Weapon>();
    public int InitialSlot = 0;
    /// <summary>武器挂载到角色的骨骼名。RifleAnimsetPro 是 RightHandProp。</summary>
    public string SocketName = "RightHandProp";
    /// <summary>切枪锁开火时长（秒），近似匹配 EquipRifle 动画长度。</summary>
    public float WeaponSwapDuration = 1.3f;

    private InputService input;
    private WeaponManager weaponMgr;
    private Weapon currentWeapon;
    private float swapLockTimer;

    public override void Attach(Character owner)
    {
        if (Ctx == null) { Debug.LogError("[WeaponComponent] GameLoop.Ctx 未就绪"); return; }
        Ctx.TryGet(out input);
        Ctx.TryGet(out weaponMgr);
        if (input != null)
        {
            input.OnWeaponSelect += HandleWeaponSelect;
        }
        // 把配置中的 Weapon Actor 实例交给 WeaponManager（注册 + 创建 view）
        if (weaponMgr != null)
        {
            for (int i = 0; i < Weapons.Count; i++)
                if (Weapons[i] != null) weaponMgr.Adopt(Weapons[i]);
        }
        // 初始装备：不播切枪动画
        EquipInternal(InitialSlot, playAnim: false);
    }

    public override void Detach()
    {
        if (input != null)
        {
            input.OnWeaponSelect -= HandleWeaponSelect;
        }
        // 武器随持枪人一起销毁
        if (weaponMgr != null)
        {
            for (int i = 0; i < Weapons.Count; i++)
                if (Weapons[i] != null) weaponMgr.Despawn(Weapons[i]);
        }
        // 清自己写过的 Owner 状态字段。近战相关字段由 MeleeComponent 负责。
        if (Owner != null)
        {
            Owner.IsShooting = false;
            Owner.IsSwapping = false;
            Owner.WeaponSwap = false;
        }
        currentWeapon = null;
        input = null;
        weaponMgr = null;
        swapLockTimer = 0f;
        base.Detach();
    }

    public override void Tick(float dt)
    {
        if (input == null || Owner == null) return;
        // 开火条件：瞄准 + 没在切枪 + 没在近战 + 没死
        Owner.IsShooting = input.FireHeld && Owner.IsAiming && !Owner.IsSwapping && !Owner.IsMeleeing && !Owner.IsDead;

        // 当前武器的开火意图 + 弹道源：FireComponent 自己消费（射速/弹夹/扩散在子组件里再叠）
        // origin 沿角色朝前推 0.6m + 枪口高度（避开自己的 CharacterController capsule）
        // direction = AimTargetWorldPos - origin：AimComponent 已经把鼠标投影到同一枪口高度平面，dir 天然水平
        if (currentWeapon != null)
        {
            currentWeapon.FireIntent = Owner.IsShooting;
            var origin = Owner.Position + Owner.Rotation * Vector3.forward * 0.6f + Vector3.up * Owner.MuzzleHeight;
            var dir = Owner.AimTargetWorldPos - origin;
            if (dir.sqrMagnitude > 1e-4f) dir.Normalize();
            else dir = Owner.Rotation * Vector3.forward;
            currentWeapon.FireOrigin = origin;
            currentWeapon.FireDirection = dir;
            currentWeapon.FireTarget = Owner.AimTargetWorldPos;
        }

        // 切枪锁开火计时
        if (swapLockTimer > 0f)
        {
            swapLockTimer -= dt;
            if (swapLockTimer <= 0f) Owner.IsSwapping = false;
        }
    }

    /// <summary>切到指定槽位。槽越界静默忽略。只走 WeaponManager Mount/Unmount，view 自己响应。</summary>
    public void Equip(int slot) => EquipInternal(slot, playAnim: true);

    private void EquipInternal(int slot, bool playAnim)
    {
        if (Owner == null) return;
        if (Weapons == null || slot < 0 || slot >= Weapons.Count) return;
        var w = Weapons[slot];
        bool slotChanged = Owner.CurrentWeaponSlot != slot;
        Owner.CurrentWeaponSlot = slot;

        if (weaponMgr != null)
        {
            if (currentWeapon != null && currentWeapon != w) weaponMgr.Unmount(currentWeapon);
            if (w != null) weaponMgr.Mount(w, Owner, SocketName);
        }
        currentWeapon = w;

        if (playAnim && slotChanged)
        {
            Owner.WeaponSwap = true;
            Owner.IsSwapping = true;
            swapLockTimer = WeaponSwapDuration;
        }
    }

    private void HandleWeaponSelect(int slot) => Equip(slot);
}
