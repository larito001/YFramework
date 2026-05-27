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
    /// <summary>V 键触发的近战类型：0=Hard 枪托砸，1=Kick 前踢。供 Animator 切动画。</summary>
    public int MeleeType = 0;

    // ── 时序参数（秒，相对 swing 起点）──
    /// <summary>一次挥击总时长。到此时间清 IsMeleeing。应近似匹配动画长度。</summary>
    public float SwingDuration = 1.2f;
    /// <summary>命中窗口开始时间。在此之前不做 Overlap（角色刚起手没接触敌人）。</summary>
    public float HitStartTime = 0.25f;
    /// <summary>命中窗口结束时间。窗口内每帧 Overlap 一次，命中过的 ID 加入 hitThisSwing 不重复。</summary>
    public float HitEndTime = 0.55f;

    // ── Hitbox 几何 ──
    /// <summary>球形 hitbox 半径（米）。</summary>
    public float HitRadius = 1.0f;
    /// <summary>hitbox 中心沿角色前方偏移（米）。</summary>
    public float HitForwardOffset = 0.8f;
    /// <summary>hitbox 中心相对脚下的高度（米）。一般略低于枪口高度，覆盖躯干。</summary>
    public float HitHeight = 1.0f;
    /// <summary>命中过滤层。建议设成仅含敌人层，避免打到环境物。</summary>
    public LayerMask HitLayers = ~0;

    // ── 伤害 ──
    /// <summary>单次命中伤害（在窗口内对同一目标只生效一次）。</summary>
    public float Damage = 30f;

    // ── 前冲位移 ──
    /// <summary>前冲速度峰值 (m/s)。t=0 时的瞬时速度。</summary>
    public float ForwardSpeed = 3f;
    /// <summary>前冲衰减时间常数 (s)。speed = ForwardSpeed * (1 - elapsed/ForwardDuration)，
    /// 在 swing 内被 SwingDuration 截断。设大于 SwingDuration 表示"swing 全程都有残余推力"。
    /// 取 2 + SwingDuration=1.2 → 推力从 3 m/s 线性降到 1.2 m/s，总位移 ~2.5m。</summary>
    public float ForwardDuration = 2f;

    private bool swinging;
    private float elapsed;
    private Vector3 forwardDir;       // swing 起点锁定的水平前向
    private InputService input;
    private ActorWorld world;
    private readonly HashSet<int> hitThisSwing = new HashSet<int>();
    // OverlapSphereNonAlloc 复用 buffer，避免每帧 alloc
    private static readonly Collider[] overlapBuf = new Collider[16];

    public override void Attach(Character owner)
    {
        Ctx?.TryGet(out world);
        Ctx?.TryGet(out input);
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
        hitThisSwing.Clear();
        input = null;
        world = null;
        base.Detach();
    }

    /// <summary>V 键事件：起手设字段，MeleeComponent 自己的 Tick 检测上升沿启动 swing。</summary>
    private void HandleMelee()
    {
        if (Owner == null || Owner.IsDead) return;
        if (Owner.IsMeleeing) return;
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
            return;
        }

        // IsMeleeing 上升沿 = 新一击开始（WeaponComponent 在事件回调里把它置 true）
        if (Owner.IsMeleeing && !swinging)
        {
            swinging = true;
            elapsed = 0f;
            hitThisSwing.Clear();
            var fwd = Owner.Rotation * Vector3.forward;
            fwd.y = 0f;
            forwardDir = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
        }

        if (!swinging) return;

        elapsed += dt;

        // 1. 前冲位移：从 swing 起算前 ForwardDuration 内，速度线性衰减到 0。
        //    覆写 Owner.WishVelocity 的水平分量，纵向（重力）保持 Move 写入的值不动。
        if (elapsed < ForwardDuration && ForwardDuration > 0.001f)
        {
            float pushT = elapsed / ForwardDuration;
            float speed = ForwardSpeed * (1f - pushT);
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

        // 3. swing 结束：清回 IsMeleeing，让 Move/Weapon 解锁
        if (elapsed >= SwingDuration)
        {
            swinging = false;
            elapsed = 0f;
            hitThisSwing.Clear();
            Owner.IsMeleeing = false;
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
            DamageRouter.ApplyToActor(world, id, Owner.ID, Damage);
        }
    }
}
