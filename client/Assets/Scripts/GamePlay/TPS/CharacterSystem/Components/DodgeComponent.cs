using UnityEngine;

/// <summary>
/// **闪避组件**（玩家近战手感核心，纯逻辑）：按移动意图做一次**带无敌帧的方向翻滚/闪身**。
/// 与 <see cref="SkillCastComponent"/> 并列、共用同一套"全身覆盖"动画机制，但更轻量——不走技能/连招的资产编排，
/// 方向 clip 直接来自角色级 <see cref="CharacterAnimSet"/>（DodgeFwd/Bwd/Left/Right），运行时按方向挑一张：
///   - **触发**：订阅 <see cref="InputComponentBase.OnDodge"/>（玩家空格；AI 可在行为树 RaiseDodge 躲技能）。
///   - **方向**：取输入组件的世界移动意图 <see cref="InputComponentBase.MoveWorld"/>；无输入 → 后撤步（角色背向）。
///     方向相对当前朝向分解，选 4 向 clip；角色朝向**不变**（用 clip + 世界向位移表达闪避方向，不转身）。
///   - **位移**：沿世界闪避向写 <see cref="Character.WishVelocity"/> 的 x/z（y 留给 <see cref="GravityComponent"/>），
///     按 <see cref="DistanceProfile"/>（默认 ease-out：起步爆发、收尾刹车）在 <see cref="Duration"/> 内推完 <see cref="Distance"/> 米。
///   - **无敌帧**：[<see cref="InvulnStartNorm"/>, <see cref="InvulnEndNorm"/>] 段内置 <see cref="Actor.IsInvulnerable"/>，
///     <see cref="HealthComponent"/> 期间完全免伤。
///   - **门控**：闪避自身/切枪/死亡中拒绝；冷却中拒绝（二连冷却：第一下短 cd 接第二下、第二下长 cd，见 <see cref="ShortCooldown"/>/<see cref="LongCooldown"/>）。
///     **技能中不拒绝——闪避会硬打断技能再起闪避（全局唯一的主动打断）**。闪避途中 Move/Aim/Weapon 经
///     <see cref="Character.IsBusy"/> 被锁（不移动/不转身/不开火），位移由本组件权威写。
///
/// Add 顺序：必须在**输入组件之后**（Attach 里 Get），且在 <see cref="MoveComponent"/> **之后**、<see cref="GravityComponent"/>
/// **之前**（同 SkillCast：前冲位移覆写 WishVelocity.xz，Move 在前会被抹掉，Gravity 在后定 y）。
/// </summary>
public class DodgeComponent : ICharacterComponent
{
    [Header("位移")]
    /// <summary>一次闪避的总位移（米）。</summary>
    public float Distance = 4f;
    /// <summary>闪避时长（秒）。也是全身 clip 被截断回 locomotion 的时刻（clip 一般更长，只播前段翻滚）。</summary>
    public float Duration = 1f;
    /// <summary>位移随进度的分布曲线（x:进度 0→1，y:已位移占比 0→1）。留空/少于 2 帧=默认 ease-out（起步爆发、收尾刹车）。</summary>
    public AnimationCurve DistanceProfile;

    [Header("时机 / 二连冷却（第一下→短 cd 接第二下；第二下→长 cd）")]
    /// <summary>二连第一下后的短冷却（秒）：这么久后即可接二连第二下闪避。</summary>
    public float ShortCooldown = 0.5f;
    /// <summary>二连第二下后的长冷却（秒）：整组用完，要等这么久才能再起新一组（回到第一下）。</summary>
    public float LongCooldown = 2f;
    /// <summary>二连复位窗（秒，从第一下闪避起算）：第一下后超过这么久没接第二下，则下次闪避退回"第一下"（仍走短 cd）。
    /// 一般 = LongCooldown，使"闪一下后久不闪"与"闪满二连"的再次可闪时刻一致。</summary>
    public float ComboResetWindow = 2f;
    /// <summary>进入闪避的淡入时长（秒）。0=用 CharacterAnimSet.DefaultFade。翻滚要紧凑，给小值。</summary>
    public float EnterFade = 0.3f;
    /// <summary>闪避结束回 locomotion 的淡入时长（秒）。0=默认。</summary>
    public float RecoverFade = 0.3f;

    [Header("无敌帧")]
    /// <summary>是否开启无敌帧。false=纯位移闪身（无免伤）。</summary>
    public bool Invulnerable = true;
    /// <summary>无敌帧开始（归一化时间 0→1，相对 Duration）。</summary>
    public float InvulnStartNorm = 0f;
    /// <summary>无敌帧结束（归一化时间 0→1）。一般留点收尾破绽（如 0.6），不要全程无敌。</summary>
    public float InvulnEndNorm = 0.6f;

    private InputComponentBase input;
    private YOTO.ResMgr resMgr;
    private CharacterAnimSet animSet;   // 与 controller 共享同一 Resources 资产（按 CurrentCharacterAnimSetPath 加载，Resources 缓存）

    // ── runtime ──
    private bool active;
    private float elapsed;
    private float prevFrac;             // 上帧已位移占比
    private Vector3 dashDir;            // 世界空间闪避方向（起手锁定，途中不变）
    private float cooldownTimer;
    private bool comboPending;          // 已用二连第一下、等第二下（仅在 ComboResetWindow 内为 true，Tick 超时清回 false）
    private float comboResetTimer;      // 二连复位倒计时（从第一下起算）

    public override void Attach(Character owner)
    {
        base.Attach(owner);
        Ctx?.TryGet(out resMgr);
        input = owner.Get<InputComponentBase>();
        if (input != null) input.OnDodge += Dodge;
        else Debug.LogWarning("[DodgeComponent] 找不到 InputComponentBase —— 闪避不会触发。需在输入组件之后 Add。");

        // 方向 clip 来自角色级 CharacterAnimSet（与动画 controller 同一份资产，Resources.Load 缓存，加载两次无额外开销）
        if (resMgr != null && !string.IsNullOrEmpty(owner.CurrentCharacterAnimSetPath))
            animSet = resMgr.Load<CharacterAnimSet>(owner.CurrentCharacterAnimSetPath);
        if (animSet == null)
            Debug.LogWarning($"[DodgeComponent] CharacterAnimSet 加载失败: {owner.CurrentCharacterAnimSetPath} —— 闪避无动画。");
    }

    public override void Detach()
    {
        if (input != null) input.OnDodge -= Dodge;
        // 清自己写过的 Owner 字段，避免 writer 离场后 reader 卡在闪避态（Move 永锁 / view 死值 / 永久无敌）
        if (Owner != null)
        {
            // 仅当全身通道仍被闪避占用才释放（避免踩到他人）；恢复无敌帧
            if (Owner.IsDodging) Owner.EndFullBody(0f);
            Owner.IsInvulnerable = false;
        }
        active = false;
        input = null;
        resMgr = null;
        animSet = null;
        base.Detach();
    }

    /// <summary>触发一次闪避。门控：死亡 / 闪避中 / 切枪中 / 冷却中 / 无 AnimSet → 静默忽略。
    /// **闪避可打断技能**（唯一的主动打断关系）：正在释放技能时不拒绝，而是先硬打断技能再起闪避；
    /// 闪避自身、切枪不可被打断（见 <see cref="Owner.IsDodging"/> / <see cref="Owner.IsSwapping"/> 门控）。</summary>
    public void Dodge()
    {
        if (Owner == null || Owner.IsDead) return;
        if (Owner.IsDodging || Owner.IsSwapping) return; // 闪避中 / 切枪中不闪避（这两者不可被闪避打断）
        if (cooldownTimer > 0f) return;                  // 冷却中
        if (animSet == null) return;                     // 没动画不闪避

        // 闪避打断技能：正在放技能 → 先硬打断（清命中窗/动效/位移、释放通道），再起闪避。
        // 技能 EndCast 写的 RecoverFade 残留会被下面 RequestFullBody(Dodge) 自动清零（新动作占用即清），闪避结束走自己的 RecoverFade。
        if (Owner.IsCastingSkill)
            Owner.Get<SkillCastComponent>()?.Interrupt();

        // 朝向（水平）
        var fwd = Owner.Rotation * Vector3.forward; fwd.y = 0f;
        fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
        // 世界闪避方向：有移动意图 → 朝它；无输入 → 后撤步（背向）
        var move = input != null ? input.MoveWorld : Vector3.zero; move.y = 0f;
        Vector3 worldDir = move.sqrMagnitude > 0.01f ? move.normalized : -fwd;

        // 相对朝向分解（local.z=前后, local.x=左右）→ 选 4 向 clip
        var local = Quaternion.Inverse(Owner.Rotation) * worldDir;
        AnimationClip clip;
        if (Mathf.Abs(local.z) >= Mathf.Abs(local.x))
            clip = local.z >= 0f ? animSet.DodgeFwd : animSet.DodgeBwd;
        else
            clip = local.x >= 0f ? animSet.DodgeRight : animSet.DodgeLeft;
        // 兜底（缺某向 clip）：用 == null（Unity 重载）而非 ?? ——避免 fake-null 取到已销毁引用
        if (clip == null) clip = animSet.DodgeBwd;
        if (clip == null) clip = animSet.DodgeFwd;

        // 起闪避
        active = true;
        elapsed = 0f;
        prevFrac = 0f;
        dashDir = worldDir;
        // 二连冷却：第一下 → 短 cd（很快可接第二下）；第二下 → 长 cd（整组用完，重来）。
        // comboPending 只在复位窗内为 true（Tick 超时会清），所以"两次间隔 > 窗口"时这次按第一下处理 → 仍是短 cd（满足"间隔超 2s 重置 0.5cd"）。
        if (comboPending)
        {
            cooldownTimer = LongCooldown;   // 二连第二下：长 cd
            comboPending = false;           // 二连结束，下次从第一下重来
        }
        else
        {
            cooldownTimer = ShortCooldown;  // 二连第一下：短 cd
            comboPending = true;
            comboResetTimer = ComboResetWindow;
        }
        Owner.RequestFullBody(FullBodyKind.Dodge); // 占用全身通道（已硬打断技能，必成功）
        Owner.SetFullBodyClip(clip, EnterFade);
    }

    public override void Tick(float dt)
    {
        if (cooldownTimer > 0f) cooldownTimer -= dt;
        // 二连复位：第一下后开 ComboResetWindow 计时，期内没接第二下就退回"第一段"（下次闪避走短 cd）。
        if (comboPending)
        {
            comboResetTimer -= dt;
            if (comboResetTimer <= 0f) comboPending = false;
        }
        if (Owner == null || !active) return;

        if (Owner.IsDead) { EndDodge(); return; }

        elapsed += dt;
        float n = Duration > 0f ? Mathf.Clamp01(elapsed / Duration) : 1f;

        // 位移：沿 dashDir，按分布曲线逐帧写 WishVelocity.xz（y 留给 Gravity）。
        // 用增量占比 / dt 换算成本帧速度——撞墙 / 贴地由 CharacterController collide-and-slide 正确处理。
        float fracNow = SampleProfile(n);
        float vmag = dt > 0f ? Distance * (fracNow - prevFrac) / dt : 0f;
        prevFrac = fracNow;
        Vector3 planar = dashDir * vmag;
        Owner.WishVelocity = new Vector3(planar.x, Owner.WishVelocity.y, planar.z);

        // 无敌帧：窗内置免伤，窗外（含收尾破绽）解除
        Owner.IsInvulnerable = Invulnerable && n >= InvulnStartNorm && n <= InvulnEndNorm;

        if (elapsed >= Duration) EndDodge();
    }

    private void EndDodge()
    {
        active = false;
        prevFrac = 0f;
        if (Owner != null)
        {
            // 释放全身通道 + 记恢复淡入（替代 IsDodging=false + DodgeRecoverFade=）；解除无敌帧
            Owner.EndFullBody(RecoverFade);
            Owner.IsInvulnerable = false;
            // 停下冲刺水平意图，y 留给 Gravity
            Owner.WishVelocity = new Vector3(0f, Owner.WishVelocity.y, 0f);
        }
    }

    /// <summary>位移分布采样：有效曲线（≥2 帧）按曲线，否则默认 ease-out 1-(1-n)^2（起步爆发、收尾刹车，翻滚冲出感）。</summary>
    private float SampleProfile(float n)
    {
        if (DistanceProfile != null && DistanceProfile.length >= 2)
            return Mathf.Clamp01(DistanceProfile.Evaluate(n));
        float inv = 1f - n;
        return 1f - inv * inv;
    }
}
