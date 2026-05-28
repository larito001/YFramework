using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 近战自包含组件：订阅 V 键、管 swing 总时长、前冲位移、命中时间窗、OverlapSphere 检测、
/// 单击去重、调 HealthComponent.ApplyDamage 扣血、清回 IsMeleeing=false。
///
/// 字段写入（Owner 上）：本组件是这些字段的**唯一 setter**：
///   - <c>MeleeAttack</c>（一次性 trigger，CharacterView 消费播动画）
///   - <c>MeleeType</c>（0=Hard 枪托砸，1=Kick 前踢，按 MeleeType 字段配置）
///   - <c>IsMeleeing</c>（swing 起设 true，swing 结束清 false）
///   - <c>WishVelocity</c>（仅 ForwardDuration 时间窗内覆写水平分量做前冲）
/// WeaponComponent 只读 IsMeleeing 做开火门控，不写不清。
///
/// Add 顺序：必须在 MoveComponent 之后 Add——本组件在前冲窗内会覆写 WishVelocity，
///   Move 在后会把 WishVelocity 再算一遍抹掉前冲位移。CharacterFactory 固定为 Aim → Move → Weapon → Melee。
///
/// 命中识别：OverlapSphere 拿到的 Collider，通过 BaseView.ID 反查 ActorWorld 上的 Character。
///   要求受击 Actor 上有 view + collider 且 view.ID == Actor.ID（CharacterView 自带 ID）。
/// </summary>
public class MeleeComponent : ICharacterComponent
{
    // ── 输入触发 ──
    /// <summary>V 键触发的近战类型：0=Hard（枪托砸，对应 WeaponAnimSet.MeleeHard clip）；1=Kick（前踢，对应 MeleeKick clip）。
    /// 触发时写到 Character.MeleeType，CharacterAnimancerController 按值选播哪个 clip。</summary>
    public int MeleeType = 0;

    // ── 时序参数（秒，相对 swing 起点）──
    /// <summary>**默认**一次挥击总时长（秒）。到此时间清 IsMeleeing → MoveComponent 解锁玩家可走。
    /// **可被 Owner.MeleeLockDuration 覆盖**：WeaponComponent.ApplySwap 切枪时从当前武器的 WeaponAnimSet.MeleeLockDuration
    /// 镜像写到 Character。effective = Owner.MeleeLockDuration &gt; 0 ? Owner.MeleeLockDuration : SwingDuration。
    /// 想要"短锁定快接连击"美工配 WeaponAnimSet.MeleeLockDuration=0.4 类似；想要"重击长锁"配 1.5+。</summary>
    public float SwingDuration = 1.2f;
    /// <summary>命中窗口**开始**时间（秒，相对 swing 起点）。0~此值不做 Overlap（角色刚起手枪/脚还没接触敌人）。
    /// 跟 MeleeHard/MeleeKick clip 的"实际命中帧"对齐。常见值 0.2~0.4。</summary>
    public float HitStartTime = 0.25f;
    /// <summary>命中窗口**结束**时间（秒，相对 swing 起点）。[HitStartTime, HitEndTime] 区间每帧 Overlap 检测命中，
    /// 同一目标 ID 加入 hitThisSwing 后不重复打。常见值 0.5~0.7。结束 → 余下时间播收招动画但不再打人。</summary>
    public float HitEndTime = 0.55f;

    // ── Hitbox 几何（球形 OverlapSphereNonAlloc 区域）──
    /// <summary>球形 hitbox 半径（米）。覆盖单个敌人体型 1.0 即可；范围打击（重锤）调大到 2.0~3.0。</summary>
    public float HitRadius = 1.0f;
    /// <summary>hitbox 中心沿角色 forward 方向的偏移（米）。0=角色脚下圆心，0.8m=略往前。
    /// 调大让"前冲砸"覆盖更远；调小让"近距离打"避免穿墙打人。</summary>
    public float HitForwardOffset = 0.8f;
    /// <summary>hitbox 中心相对脚下 Position.y 的高度（米）。一般略低于 MuzzleHeight，覆盖躯干。
    /// 1.0m 适合站立敌人胸口；调高到 1.5 覆盖头部 / 调低到 0.5 打腿。</summary>
    public float HitHeight = 1.0f;
    /// <summary>OverlapSphere 物理 layer 过滤。建议生产期设成仅含敌人层，避免打到环境物 / 队友。
    /// 默认 ~0=全开（含 Default / Player / Enemy / Environment），调试用。</summary>
    public LayerMask HitLayers = ~0;

    // ── 伤害 ──
    /// <summary>命中伤害配方（基础 / 卡肉 / 暴击 / 元素 / buff 全部集中）。窗口内对同一目标只生效一次。
    /// Factory 配 object initializer，命中时 DamageInfo.Build 一行组装。</summary>
    public DamageSpec Damage;

    // ── 相机震屏（动作伴随反馈，inline 调 CameraManager.Shake） ──
    /// <summary>挥击命中窗开启那一帧触发的相机抖动强度（米级位移）。0=不抖。
    /// kickback 方向 = -forwardDir（相机被推到挥击反向，看起来像"撞击反作用"）。常见 0.1~0.3。</summary>
    public float ShakeIntensity = 0.18f;
    /// <summary>相机抖动从满到 0 的衰减时长（秒）。0.1~0.2 是典型动作游戏值。
    /// 跟 SwingDuration 无关——抖动是瞬时反馈，可以早于 swing 结束。</summary>
    public float ShakeDuration = 0.15f;

    // ── 前冲位移（swing 期间 MeleeComponent 覆写 Owner.WishVelocity.x/z）──
    /// <summary>前冲峰值速度 (m/s)。swing 起点 t=0 时的瞬时速度，按 ForwardDuration 线性衰减。
    /// 3=轻量动作（步伐前移）；6~8=重击带强位移；0=完全原地不前冲。</summary>
    public float ForwardSpeed = 3f;
    /// <summary>前冲衰减时间常数 (s)。speed = ForwardSpeed × (1 - elapsed/ForwardDuration)。
    /// **设大于 SwingDuration** 表示"swing 全程都有残余推力"；**设小于 SwingDuration** 表示"前段有冲后段静止"。
    /// 默认 ForwardSpeed=3 / ForwardDuration=2 / SwingDuration=1.2：推力从 3→1.2 m/s 衰减，swing 内总位移 ~2.5m。</summary>
    public float ForwardDuration = 2f;

    // ── 冷却 ──
    /// <summary>**默认**两次近战间的冷却时长（秒），swing 结束后到下次允许触发的间隔。0=无冷却（落地立刻可再挥）。
    /// **可被 Owner.MeleeCooldown 覆盖**（WeaponComponent.ApplySwap 从 WeaponAnimSet.MeleeCooldown 镜像）。
    /// 用途：避免"连按 V → swing 重启 → 前冲反复重启 → 角色被持续推"——加冷却让连击有明确节奏。
    /// 推荐：轻武器 0.2~0.4；重武器 0.5+。</summary>
    public float Cooldown = 0f;

    private bool swinging;
    private float elapsed;
    private Vector3 forwardDir;       // swing 起点锁定的水平前向
    private bool shakeFired;          // 一次 swing 内只触发一次相机抖
    private float cooldownTimer;      // >0 时拒绝新的 V 触发；swing 结束帧 = effectiveCooldown，每帧 -dt
    private InputService input;
    private ActorWorld world;
    private CameraManager cameraMgr;
    private readonly HashSet<int> hitThisSwing = new HashSet<int>();
    // OverlapSphereNonAlloc 复用 buffer，避免每帧 alloc
    private static readonly Collider[] overlapBuf = new Collider[16];

    public override void Attach(Character owner)
    {
        Ctx?.TryGet(out world);
        Ctx?.TryGet(out input);
        Ctx?.TryGet(out cameraMgr);
        if (input != null) input.OnMeleeDown += HandleMelee;
    }

    public override void Detach()
    {
        if (input != null) input.OnMeleeDown -= HandleMelee;
        // 组件离场把全部近战相关字段清干净，避免残留卡住 Move/Weapon
        if (Owner != null)
        {
            Owner.IsMeleeing = false;
            Owner.MeleeAttack = false;
            Owner.MeleeType = 0;
        }
        swinging = false;
        elapsed = 0f;
        shakeFired = false;
        cooldownTimer = 0f;
        hitThisSwing.Clear();
        input = null;
        world = null;
        cameraMgr = null;
        base.Detach();
    }

    /// <summary>V 键事件：起手设字段，MeleeComponent 自己的 Tick 检测 IsMeleeing 上升沿启动 swing。
    /// **连击重置语义**：cooldown 通过时无视 IsMeleeing，强制清当前 swing 状态 → Tick 下一帧把 "IsMeleeing && !swinging"
    /// 视为新上升沿，elapsed/forwardDir/shakeFired/hitThisSwing/clip time 全部重置，相当于"打断重启"。
    /// 限制连击节奏只靠 cooldown：cooldown=0 → 按一次重启一次（无限连按）；cooldown&gt;0 → 强制节奏。</summary>
    private void HandleMelee()
    {
        if (Owner == null || Owner.IsDead) return;
        if (Owner.IsReloading) return;  // 换弹中手是占用的，不响应近战
        if (Owner.IsSwapping) return;   // 切枪中手是占用的，不响应近战（Holster+Equip 两阶段全程）
        if (cooldownTimer > 0f) return; // 冷却中静默忽略
        // 不再用 IsMeleeing 早退——允许打断当前 swing 重启。
        // 显式重置 swinging：让 Tick 进入 "IsMeleeing && !swinging" 分支重置 elapsed/forwardDir/shakeFired/hitThisSwing。
        swinging = false;
        Owner.MeleeAttack = true;
        Owner.MeleeType = MeleeType;
        Owner.IsMeleeing = true;
    }

    public override void Tick(float dt)
    {
        if (Owner == null) return;
        if (Owner.IsDead)
        {
            // 死亡时强制结束 swing，IsMeleeing 也清掉
            if (swinging) { swinging = false; Owner.IsMeleeing = false; }
            cooldownTimer = 0f;
            return;
        }

        // 冷却倒计时（无论是否 swinging 都走，swing 内 timer 也 = 0 不影响）
        if (cooldownTimer > 0f) cooldownTimer -= dt;

        // IsMeleeing 上升沿 = 新一击开始（WeaponComponent 在事件回调里把它置 true）
        if (Owner.IsMeleeing && !swinging)
        {
            swinging = true;
            elapsed = 0f;
            shakeFired = false;
            hitThisSwing.Clear();
            var fwd = Owner.Rotation * Vector3.forward;
            fwd.y = 0f;
            forwardDir = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
        }

        if (!swinging) return;

        elapsed += dt;

        // 1. 前冲位移：从 swing 起算前 effForwardDur 内，速度线性衰减到 0。覆写 Owner.WishVelocity 的水平分量，纵向（重力）保持 Move 写入的值不动。
        //    **effective**：Owner.MeleeForwardSpeed/Duration 由 WeaponComponent.ApplySwap 从 WeaponAnimSet 镜像写入，>0 时覆盖默认。
        //    Speed 允许 <0 表示"显式覆盖为 0"（0 被当作"不覆盖"信号）。
        float effForwardSpeed = Owner.MeleeForwardSpeed > 0f ? Owner.MeleeForwardSpeed
                              : Owner.MeleeForwardSpeed < 0f ? 0f
                              : ForwardSpeed;
        float effForwardDur = Owner.MeleeForwardDuration > 0f ? Owner.MeleeForwardDuration : ForwardDuration;
        if (elapsed < effForwardDur && effForwardDur > 0.001f && effForwardSpeed > 0f)
        {
            float pushT = elapsed / effForwardDur;
            float speed = effForwardSpeed * (1f - pushT);
            Owner.WishVelocity = new Vector3(
                forwardDir.x * speed,
                Owner.WishVelocity.y,
                forwardDir.z * speed);
        }

        // 2. 命中窗：每帧 Overlap，命中过的 actor 不重复打
        if (elapsed >= HitStartTime && elapsed <= HitEndTime)
        {
            DoHitDetection();
        }

        // 3. 相机震屏：命中窗开启那一帧打一发，方向 = -forwardDir（kickback 风格）。
        // 与命中结果解耦：哪怕没打到东西，挥击本身也应该让相机往挥击反向"顿"一下。
        if (!shakeFired && elapsed >= HitStartTime)
        {
            shakeFired = true;
            if (ShakeIntensity > 0f && cameraMgr?.Shake != null)
                cameraMgr.Shake.Shake(-forwardDir, ShakeDuration, ShakeIntensity);
        }

        // 4. swing 结束：清回 IsMeleeing，让 Move/Weapon 解锁，启动 cooldown
        // **effective duration**：Owner.MeleeLockDuration > 0 时覆盖 SwingDuration（美工在 WeaponAnimSet 配，由 WeaponComponent.ApplySwap 镜像写入）
        // 允许 .asset 缩短锁定（连击体验好）或延长锁定（重武器手感重）
        float effectiveDuration = Owner.MeleeLockDuration > 0f ? Owner.MeleeLockDuration : SwingDuration;
        if (elapsed >= effectiveDuration)
        {
            swinging = false;
            elapsed = 0f;
            shakeFired = false;
            hitThisSwing.Clear();
            Owner.IsMeleeing = false;
            // cooldown：swing 结束后到下次允许触发的间隔。0=无冷却（不阻挡 V）
            float effectiveCooldown = Owner.MeleeCooldown > 0f ? Owner.MeleeCooldown : Cooldown;
            cooldownTimer = effectiveCooldown;
        }
    }

    private void DoHitDetection()
    {
        if (world == null) return;
        var center = Owner.Position
                     + forwardDir * HitForwardOffset
                     + Vector3.up * HitHeight;

        int count = Physics.OverlapSphereNonAlloc(center, HitRadius, overlapBuf, HitLayers);
        for (int i = 0; i < count; i++)
        {
            // 两步式：ResolveActorId 拿 id → 自己去重 → ApplyToActor 扣血
            int id = DamageRouter.ResolveActorId(overlapBuf[i], Owner.ID);
            if (id < 0 || hitThisSwing.Contains(id)) continue;
            hitThisSwing.Add(id);
            var info = DamageInfo.Build(in Damage, Owner.ID, Owner.TeamId);
            DamageRouter.ApplyToActor(world, id, in info);
        }
    }
}
