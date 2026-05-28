using UnityEngine;

/// <summary>
/// 通用开火组件：装在 Weapon 上。统一处理所有武器共用的门控/节流/弹药/抖屏/触发事件，
/// 具体"造哪种弹道"由 <see cref="Effect"/> 策略决定（<see cref="LinearProjectileEffect"/> /
/// <see cref="BezierMissileEffect"/> / <see cref="HitscanEffect"/> / 等）。
///
/// 每帧 Tick 流程：
///   1. 早退：未装备 / 没开火意图 / cooldown 没好 / Effect 未配 / 弹药空
///   2. 起 cooldown，扣弹（MagCapacity=0 视作无限弹药）
///   3. 调用 Effect.Fire 走具体弹道
///   4. 写 Owner.ShootEvent 让 WeaponComponent 转写 Owner(Character).Shoot 给动画 trigger
///   5. 触发相机抖（动作伴随反馈，inline 调 service，详见架构规范）
///
/// 加新武器形态（追踪导弹 / 散弹枪 / 喷火器）直接写个 FireEffect 子类即可，本组件无需改动。
/// </summary>
public class FireComponent : IWeaponComponent
{
    /// <summary>两次射击最小间隔（秒）。0.1 = 600 RPM 全自动；0.6 = 重武器节奏。</summary>
    public float FireInterval = 0.1f;
    /// <summary>单发伤害。透传给 Effect.Fire，由具体实现写到 Bullet.Damage 或直接通过 DamageRouter 应用。</summary>
    public float Damage = 25f;
    /// <summary>每发开火的相机抖动强度。0 = 不抖（默认，给 NPC 武器用）；玩家武器一般 0.08~0.4。</summary>
    public float RecoilShakeIntensity = 0f;
    /// <summary>每发开火的相机抖动时长（秒）。和 FireInterval 量级接近，抖动有连续感。</summary>
    public float RecoilShakeDuration = 0.08f;
    /// <summary>弹道策略：null 时不开火。Factory 配置时按武器形态注入：
    /// 直线 → LinearProjectileEffect；曲线/导弹 → BezierMissileEffect；瞬时射线 → HitscanEffect。</summary>
    public FireEffect Effect;

    private float cooldown;
    private BulletManager bulletMgr;
    private ActorWorld world;
    private CameraManager cameraMgr;

    public override void Attach(Weapon owner)
    {
        Ctx?.TryGet(out bulletMgr);
        Ctx?.TryGet(out world);
        Ctx?.TryGet(out cameraMgr);
    }

    public override void Detach()
    {
        bulletMgr = null;
        world = null;
        cameraMgr = null;
        cooldown = 0f;
        base.Detach();
    }

    public override void Tick(float dt)
    {
        if (Owner == null || !Owner.IsEquipped) return;
        if (cooldown > 0f) cooldown -= dt;
        if (!Owner.FireIntent) return;
        if (cooldown > 0f) return;
        if (Effect == null) return;
        // MagCapacity=0 表示无限弹药（不扣不查）。否则弹匣空就不开火。
        if (Owner.MagCapacity > 0 && Owner.CurrentAmmo <= 0) return;

        cooldown = FireInterval;
        if (Owner.MagCapacity > 0) Owner.CurrentAmmo--;

        Effect.Fire(Owner, bulletMgr, world, Damage);

        Owner.ShootEvent = true; // 喂 WeaponComponent，下一帧转写 Owner(Character).Shoot 给 view SetTrigger

        if (RecoilShakeIntensity > 0f && cameraMgr?.Shake != null)
        {
            // kickback：相机被推到"射击反方向"。-FireDirection 就是后坐力的世界方向。
            cameraMgr.Shake.Shake(-Owner.FireDirection, RecoilShakeDuration, RecoilShakeIntensity);
        }
    }
}
