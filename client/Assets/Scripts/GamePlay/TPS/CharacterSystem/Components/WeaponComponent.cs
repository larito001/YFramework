using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 持枪人组件（Character 侧，纯逻辑）：
///   - 持有武器槽位 Weapon Actor 列表，Attach 时交给 WeaponManager 接管（注册 + 创建 view）
///   - 切枪：监听 InputService.OnWeaponSelect（1~9 数字键）→ Equip(slot) → weaponMgr.Mount/Unmount
///   - 射击：每帧把 input.FireHeld 写到 Owner.IsShooting（开火行为由 Weapon 的 FireComponent 自己消费，本类不管）
///   - 近战：监听 InputService.OnMeleeDown（V）→ 置 Owner.MeleeAttack + MeleeType（后续可拆 MeleeComponent）
///
/// Add 顺序约束：**必须在 MoveComponent 之后 Add**。
///   近战前冲段 Tick 里直接覆写 Owner.WishVelocity（见下方 melee 锁位移块），如果 Move 在本组件之后 Tick，
///   Move 会把 WishVelocity 再算一遍抹掉前冲位移。CharacterFactory 里固定为 Aim → Move → Weapon。
/// </summary>
public class WeaponComponent : ICharacterComponent
{
    public List<Weapon> Weapons = new List<Weapon>();
    public int InitialSlot = 0;
    /// <summary>武器挂载到角色的骨骼名。RifleAnimsetPro 是 RightHandProp。</summary>
    public string SocketName = "RightHandProp";
    /// <summary>V 键触发的近战类型：0=Hard 枪托砸，1=Kick 前踢。</summary>
    public int MeleeTypeForKey = 0;
    /// <summary>近战锁位移时长（秒），近似匹配 Rifle_Melee_Hard / Kick 动画长度。</summary>
    public float MeleeLockDuration = 1.2f;
    /// <summary>近战前冲速度峰值 (m/s)。</summary>
    public float MeleeForwardSpeed = 3f;
    /// <summary>近战前冲持续时间（秒）。从 melee 触发起算，线性衰减到 0。
    /// 默认 0.3s + 峰值 3 → 位移约 0.45m，配合动画前段冲击。</summary>
    public float MeleeForwardDuration = 2f;
    /// <summary>切枪锁开火时长（秒），近似匹配 EquipRifle 动画长度。</summary>
    public float WeaponSwapDuration = 1.3f;

    private InputService input;
    private WeaponManager weaponMgr;
    private Weapon currentWeapon;
    private float meleeLockTimer;
    private float swapLockTimer;
    private Vector3 meleeForwardDir; // melee 触发瞬间锁定的水平前向

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
        ctx.TryGet(out weaponMgr);
        if (input != null)
        {
            input.OnMeleeDown += HandleMelee;
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
            input.OnMeleeDown -= HandleMelee;
            input.OnWeaponSelect -= HandleWeaponSelect;
        }
        // 武器随持枪人一起销毁
        if (weaponMgr != null)
        {
            for (int i = 0; i < Weapons.Count; i++)
                if (Weapons[i] != null) weaponMgr.Despawn(Weapons[i]);
        }
        currentWeapon = null;
        base.Detach();
    }

    public override void Tick(float dt)
    {
        if (input == null || Owner == null) return;
        // 开火条件：瞄准 + 没在切枪 + 没在近战
        Owner.IsShooting = input.FireHeld && Owner.IsAiming && !Owner.IsSwapping && !Owner.IsMeleeing;

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
        }

        // 近战锁位移计时 + 前冲位移
        if (meleeLockTimer > 0f)
        {
            float elapsed = MeleeLockDuration - meleeLockTimer;
            if (elapsed < MeleeForwardDuration && MeleeForwardDuration > 0.001f)
            {
                float pushT = elapsed / MeleeForwardDuration;
                float speed = MeleeForwardSpeed * (1f - pushT);
                Owner.WishVelocity = new Vector3(
                    meleeForwardDir.x * speed,
                    Owner.WishVelocity.y,
                    meleeForwardDir.z * speed);
            }

            meleeLockTimer -= dt;
            if (meleeLockTimer <= 0f) Owner.IsMeleeing = false;
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

    private void HandleMelee()
    {
        if (Owner == null) return;
        if (Owner.IsMeleeing) return;
        Owner.MeleeAttack = true;
        Owner.MeleeType = MeleeTypeForKey;
        Owner.IsMeleeing = true;
        meleeLockTimer = MeleeLockDuration;
        var fwd = Owner.Rotation * Vector3.forward;
        fwd.y = 0f;
        meleeForwardDir = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
    }

    private void HandleWeaponSelect(int slot) => Equip(slot);
}
