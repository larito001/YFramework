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
    /// <summary>近战锁位移时长（秒），近似匹配 Rifle_Melee_Hard / Kick 动画长度。</summary>
    public float MeleeLockDuration = 1.2f;
    /// <summary>近战前冲速度峰值 (m/s)。</summary>
    public float MeleeForwardSpeed = 3f;
    /// <summary>近战前冲持续时间（秒）。从 melee 触发起算，线性衰减到 0。
    /// 默认 0.3s + 峰值 3 → 位移约 0.45m，配合动画前段冲击。</summary>
    public float MeleeForwardDuration = 2f;
    /// <summary>切枪锁开火时长（秒），近似匹配 EquipRifle 动画长度。</summary>
    public float WeaponSwapDuration = 0.8f;

    private InputService input;
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
        // 开火条件：瞄准 + 没在切枪 + 没在近战
        Owner.IsShooting = input.FireHeld && Owner.IsAiming && !Owner.IsSwapping && !Owner.IsMeleeing;

        // 近战锁位移计时 + 前冲位移
        if (meleeLockTimer > 0f)
        {
            // 前冲：melee 触发起算，前 MeleeForwardDuration 秒内沿 meleeForwardDir 推进
            // speed 从 MeleeForwardSpeed 线性衰减到 0；超过这段时长就停在原地（由 MoveComponent 写的 0 水平速度）
            float elapsed = MeleeLockDuration - meleeLockTimer;
            if (elapsed < MeleeForwardDuration && MeleeForwardDuration > 0.001f)
            {
                float pushT = elapsed / MeleeForwardDuration;
                float speed = MeleeForwardSpeed * (1f - pushT);
                Owner.WishVelocity = new Vector3(
                    meleeForwardDir.x * speed,
                    Owner.WishVelocity.y, // 保留重力
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
        if (Owner.IsMeleeing) return; // 已在近战中，忽略重复触发
        Owner.MeleeAttack = true;
        Owner.MeleeType = MeleeTypeForKey;
        Owner.IsMeleeing = true;
        meleeLockTimer = MeleeLockDuration;
        // 锁定前冲方向：用触发瞬间角色面朝的水平方向
        var fwd = Owner.Rotation * Vector3.forward;
        fwd.y = 0f;
        meleeForwardDir = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
    }

    private void HandleWeaponSelect(int slot) => Equip(slot);
}
