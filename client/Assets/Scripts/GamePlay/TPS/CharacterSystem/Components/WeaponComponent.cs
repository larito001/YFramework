using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 持枪人组件（Character 侧，纯逻辑）：
///   - 持有武器槽位 Weapon Actor 列表，Attach 时交给 WeaponManager 接管（注册 + 创建 view）
///   - 切枪：监听 InputService.OnWeaponSelect（1~9 数字键）→ Equip(slot) → weaponMgr.Mount/Unmount + 计时锁开火
///   - 射击：每帧把 input.FireHeld 写到 Owner.IsShooting（开火行为由 Weapon 的 FireComponent 自己消费）
///   - 换弹：监听 InputService.OnReloadDown → 校验后启动 reloadTimer，到点把 CurrentAmmo 回填到 MagCapacity
///     并清 IsReloading；换枪打断换弹（reloadTimer 是绑定 currentWeapon 跑的）
///
/// 近战完全由 <see cref="MeleeComponent"/> 接管：V 键订阅、MeleeAttack/MeleeType/IsMeleeing 写入、
/// swing 时长、前冲、命中、伤害结算、清回 IsMeleeing。本组件只在 Tick 里**读** IsMeleeing 做开火门控，
/// 不写入也不清除近战字段。
///
/// 字段写入：FireOrigin / FireDirection / FireTarget / FireIntent / IsReloading / CurrentAmmo → currentWeapon。
/// IsShooting / IsSwapping / WeaponSwap / CurrentWeaponSlot / IsReloading / Reload / Shoot / HeavyRecoil / RecoilAnimSpeed → Owner。
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
    private float reloadTimer; // >0 表示 currentWeapon 正在换弹倒计时

    public override void Attach(Character owner)
    {
        if (Ctx == null) { Debug.LogError("[WeaponComponent] GameLoop.Ctx 未就绪"); return; }
        Ctx.TryGet(out input);
        Ctx.TryGet(out weaponMgr);
        if (input != null)
        {
            input.OnWeaponSelect += HandleWeaponSelect;
            input.OnReloadDown += HandleReload;
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
            input.OnReloadDown -= HandleReload;
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
            Owner.IsReloading = false;
            Owner.Reload = false;
            Owner.Shoot = false;
        }
        currentWeapon = null;
        input = null;
        weaponMgr = null;
        swapLockTimer = 0f;
        reloadTimer = 0f;
        base.Detach();
    }

    public override void Tick(float dt)
    {
        if (input == null || Owner == null) return;

        // 换弹倒计时：只对 currentWeapon 跑。到点回填弹匣 + 清状态。
        if (reloadTimer > 0f)
        {
            reloadTimer -= dt;
            if (reloadTimer <= 0f)
            {
                reloadTimer = 0f;
                if (currentWeapon != null)
                {
                    currentWeapon.CurrentAmmo = currentWeapon.MagCapacity;
                    currentWeapon.IsReloading = false;
                }
                Owner.IsReloading = false;
            }
        }

        // 开火条件：瞄准 + 没在切枪 + 没在近战 + 没在换弹 + 没死 + 有弹（MagCapacity=0 是无限弹药武器，跳过弹药门控）
        bool hasAmmo = currentWeapon == null || currentWeapon.MagCapacity <= 0 || currentWeapon.CurrentAmmo > 0;
        Owner.IsShooting = input.FireHeld && Owner.IsAiming && !Owner.IsSwapping && !Owner.IsMeleeing && !Owner.IsReloading && !Owner.IsDead && hasAmmo;

        // 射击一次性 trigger 镜像：FireComponent 每发射成功置 ShootEvent，view 端 SetTrigger("Shoot") 重启 Recoil 动画
        if (currentWeapon != null && currentWeapon.ShootEvent)
        {
            currentWeapon.ShootEvent = false;
            Owner.Shoot = true;
        }

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

        // 换枪打断换弹：reloadTimer 是绑定 currentWeapon 跑的，换枪后必须清掉，
        // 否则到点会回填到新武器弹匣里
        if (slotChanged && reloadTimer > 0f)
        {
            reloadTimer = 0f;
            if (currentWeapon != null) currentWeapon.IsReloading = false;
            Owner.IsReloading = false;
        }

        if (weaponMgr != null)
        {
            if (currentWeapon != null && currentWeapon != w) weaponMgr.Unmount(currentWeapon);
            if (w != null) weaponMgr.Mount(w, Owner, SocketName);
        }
        currentWeapon = w;

        // 把新武器的后坐力动画参数推给 Owner，view 每帧 SetBool/SetFloat 给 Animator
        if (currentWeapon != null)
        {
            Owner.HeavyRecoil = currentWeapon.HeavyRecoil;
            Owner.RecoilAnimSpeed = currentWeapon.RecoilAnimSpeed;
        }

        if (playAnim && slotChanged)
        {
            Owner.WeaponSwap = true;
            Owner.IsSwapping = true;
            swapLockTimer = WeaponSwapDuration;
        }
    }

    private void HandleWeaponSelect(int slot) => Equip(slot);

    /// <summary>R 键事件：校验后启动换弹倒计时，并发一次性 Reload trigger 给 view。</summary>
    private void HandleReload()
    {
        if (Owner == null || Owner.IsDead) return;
        if (currentWeapon == null) return;
        // 切枪/近战/换弹中不响应：这些状态下角色手是占用的
        if (Owner.IsSwapping || Owner.IsMeleeing || Owner.IsReloading) return;
        if (currentWeapon.MagCapacity <= 0) return;                          // 无限弹药武器不换弹
        if (currentWeapon.CurrentAmmo >= currentWeapon.MagCapacity) return;  // 已满

        reloadTimer = currentWeapon.ReloadDuration;
        currentWeapon.IsReloading = true;
        Owner.IsReloading = true;
        Owner.Reload = true; // 一次性 trigger，view 下一帧消费 SetTrigger 后清回
    }
}
