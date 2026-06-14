using UnityEngine;

/// <summary>
/// **闪避组件**（玩家近战手感核心，纯逻辑）：按移动意图做一次**带无敌帧的方向翻滚/闪身**。
/// 与 <see cref="SkillCastComponent"/> 并列、共用同一套"全身覆盖"动画机制，但更轻量——不走技能/连招的资产编排。
/// **纯逻辑、不碰 AnimationClip**：本组件只算**方向意图**（<see cref="DodgeDir"/>）发给全身通道，clip 由 <see cref="FullBodyDriver"/>
/// 按方向从角色级 <see cref="CharacterAnimSet"/>（DodgeFwd/Bwd/Left/Right）解析（含缺向兜底）。
///   - **触发**：订阅 <see cref="InputComponentBase.OnDodge"/>（玩家空格；AI 可在行为树 RaiseDodge 躲技能）。
///   - **方向**：取输入组件的世界移动意图 <see cref="InputComponentBase.MoveWorld"/>；无输入 → 后撤步（角色背向）。
///     **起手**按当前朝向分解选 4 向 clip + 锁定世界向 dashDir（位移方向，途中不变）。
///   - **朝向**：前段锁朝向（AimComponent 因 IsBusy 让位），把方向 clip 滚干净；到**尾段过渡期**（n ≥ <see cref="TurnToAimStartNorm"/>）
///     本组件自己把朝向 slerp 向瞄准点 / 移动方向，使翻滚结束正好面向鼠标——避免半截转身穿帮。翻滚结束后 AimComponent 自然接管。
///     位移（dashDir）与朝向（Rotation）相互独立：只转身、不改落点。
///   - **位移**：沿世界闪避向写 <see cref="Character.WishVelocity"/> 的 x/z（y 留给 <see cref="GravityComponent"/>），
///     按 <see cref="DistanceProfile"/>（默认 ease-out：起步爆发、收尾刹车）在 <see cref="Duration"/> 内推完 <see cref="Distance"/> 米。
///   - **无敌帧**：[<see cref="InvulnStartNorm"/>, <see cref="InvulnEndNorm"/>] 段内置 <see cref="Actor.IsInvulnerable"/>，
///     <see cref="HealthComponent"/> 期间完全免伤。
///   - **门控**：闪避自身/切枪/死亡中拒绝；冷却中拒绝（二连冷却：第一下短 cd 接第二下、第二下长 cd，见 <see cref="ShortCooldown"/>/<see cref="LongCooldown"/>）。
///     **技能中不拒绝——闪避在 FullBodyKind 里优先级最高，RequestFullBody(Dodge) 直接占走全身通道，被抢的技能在自己 Tick 里自检通道易主 → 自行中止**。
///     闪避途中 Move/Aim/Weapon 经 <see cref="Character.IsBusy"/> 被锁（不移动/不转身/不开火），位移由本组件权威写。
///
/// Add 顺序：必须在**输入组件之后**（Attach 里 Get），且在 <see cref="MoveComponent"/> **之后**、<see cref="GravityComponent"/>
/// **之前**（同 SkillCast：前冲位移覆写 WishVelocity.xz，Move 在前会被抹掉，Gravity 在后定 y）。
/// </summary>
public class DodgeComponent : ICharacterComponent
{
    /// <summary>翻滚手感配置资产（Resources 相对路径）。Attach 时加载 <see cref="DodgeConfig"/> 覆盖下方默认值——
    /// 让 Distance/Duration/曲线/无敌帧等可在 Inspector 拖曲线调。空 / 加载失败 → 用下方组件内置默认（不崩）。</summary>
    public string DodgeConfigPath;

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
    public float RecoverFade = 0.12f;

    [Header("尾段转向瞄准")]
    /// <summary>是否在翻滚尾段把朝向转向瞄准/移动方向（让翻滚结束面向鼠标）。false=整段保持起手朝向。</summary>
    public bool TurnToAim = true;
    /// <summary>从归一化进度（相对 Duration）的哪一点开始转向。前段（&lt;此值）保持起手朝向把方向 clip 滚干净，
    /// 此点起进入"过渡转向"。0.6 ≈ 位移基本走完（ease-out）+ 无敌帧结束后才转，避免半截转身穿帮。</summary>
    public float TurnToAimStartNorm = 0.6f;
    /// <summary>尾段转向的 slerp 速率（指数收敛，帧率无关）。25 ≈ 在剩余尾段 + 恢复淡入内转到位。</summary>
    public float TurnToAimRate = 25f;

    [Header("无敌帧")]
    /// <summary>是否开启无敌帧。false=纯位移闪身（无免伤）。</summary>
    public bool Invulnerable = true;
    /// <summary>无敌帧开始（归一化时间 0→1，相对 Duration）。</summary>
    public float InvulnStartNorm = 0f;
    /// <summary>无敌帧结束（归一化时间 0→1）。一般留点收尾破绽（如 0.6），不要全程无敌。</summary>
    public float InvulnEndNorm = 0.6f;

    private InputComponentBase input;
    private YOTO.ResMgr resMgr;

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
        input = owner.Get<InputComponentBase>();
        if (input != null) input.OnDodge += Dodge;
        else Debug.LogWarning("[DodgeComponent] 找不到 InputComponentBase —— 闪避不会触发。需在输入组件之后 Add。");

        // 翻滚手感从 DodgeConfig 资产读（可 Inspector 拖曲线）；缺失则用上方组件内置默认。
        if (!string.IsNullOrEmpty(DodgeConfigPath))
        {
            Ctx?.TryGet(out resMgr);
            var cfg = resMgr != null ? resMgr.Load<DodgeConfig>(DodgeConfigPath) : null;
            if (cfg != null) ApplyConfig(cfg);
            else Debug.LogWarning($"[DodgeComponent] DodgeConfig 加载失败（用内置默认）: {DodgeConfigPath}");
        }
        // 方向 clip 不再在此加载——本组件只发方向意图，FullBodyDriver 按方向从 CharacterAnimSet 解析 clip（逻辑层不碰动画资产）
    }

    /// <summary>把 DodgeConfig 的数值覆盖到运行时字段（DistanceProfile 为空/少于 2 帧时 SampleProfile 仍回退 ease-out）。</summary>
    private void ApplyConfig(DodgeConfig c)
    {
        Distance = c.Distance;
        Duration = c.Duration;
        DistanceProfile = c.DistanceProfile;
        ShortCooldown = c.ShortCooldown;
        LongCooldown = c.LongCooldown;
        ComboResetWindow = c.ComboResetWindow;
        EnterFade = c.EnterFade;
        RecoverFade = c.RecoverFade;
        TurnToAim = c.TurnToAim;
        TurnToAimStartNorm = c.TurnToAimStartNorm;
        TurnToAimRate = c.TurnToAimRate;
        Invulnerable = c.Invulnerable;
        InvulnStartNorm = c.InvulnStartNorm;
        InvulnEndNorm = c.InvulnEndNorm;
    }

    public override void Detach()
    {
        if (input != null) input.OnDodge -= Dodge;
        // 释放 config 引用计数（玩家重生会重建本组件，避免每次重生 Load 不还导致泄漏）。
        if (resMgr != null && !string.IsNullOrEmpty(DodgeConfigPath)) resMgr.Release<DodgeConfig>(DodgeConfigPath);
        resMgr = null;
        // 清自己写过的 Owner 字段，避免 writer 离场后 reader 卡在闪避态（Move 永锁 / view 死值 / 永久无敌）
        if (Owner != null)
        {
            // 仅当全身通道仍被闪避占用才释放（避免踩到他人）；恢复无敌帧
            if (Owner.IsDodging) Owner.EndFullBody(0f);
            Owner.IsInvulnerable = false;
        }
        active = false;
        input = null;
        base.Detach();
    }

    /// <summary>触发一次闪避。门控：死亡 / 闪避中 / 切枪中 / 冷却中 → 静默忽略（无 dodge clip 也照闪，只是无动画——gameplay 不依赖动画）。
    /// **闪避可打断技能**：靠 FullBodyKind 优先级（Dodge 最高）——`RequestFullBody(Dodge)` 占走通道即完成打断，技能自检后中止，无需显式调它；
    /// 闪避自身、切枪不可被打断（见 <see cref="Owner.IsDodging"/> / <see cref="Owner.IsSwapping"/> 门控）。</summary>
    public void Dodge()
    {
        if (Owner == null || Owner.IsDead) return;
        if (Owner.IsDodging || Owner.IsSwapping) return; // 闪避中 / 切枪中不闪避（这两者不可被闪避打断）
        if (cooldownTimer > 0f) return;                  // 冷却中

        // 朝向（水平）
        var fwd = Owner.Rotation * Vector3.forward; fwd.y = 0f;
        fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
        // 世界闪避方向：有移动意图 → 朝它；无输入 → 后撤步（背向）
        var move = input != null ? input.MoveWorld : Vector3.zero; move.y = 0f;
        Vector3 worldDir = move.sqrMagnitude > 0.01f ? move.normalized : -fwd;

        // 相对朝向分解（local.z=前后, local.x=左右）→ 选方向意图（clip 由 FullBodyDriver 按方向解析，本组件不碰 AnimationClip / CharacterAnimSet）
        var local = Quaternion.Inverse(Owner.Rotation) * worldDir;
        DodgeDir dir;
        if (Mathf.Abs(local.z) >= Mathf.Abs(local.x))
            dir = local.z >= 0f ? DodgeDir.Fwd : DodgeDir.Bwd;
        else
            dir = local.x >= 0f ? DodgeDir.Right : DodgeDir.Left;

        // 占用全身通道：闪避在 FullBodyKind 里优先级最高，正常必成功；被更高优先级占用则放弃（护栏，谁能起手由优先级说了算）。
        // 占走通道即"打断"了技能/受击——被抢方在各自 Tick 里自检 FullBody.Kind != 自己 → 自行中止，无需在此显式调它们的打断。
        if (!Owner.RequestFullBody(FullBodyKind.Dodge)) return;

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
        Owner.SetFullBodyDodge(dir, EnterFade);
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

        // 尾段过渡转向：前段保持起手朝向把方向 clip 滚干净，n≥TurnToAimStartNorm 才转向瞄准/移动方向，
        // 翻滚结束正好面向鼠标，避免半截转身穿帮。结束后 AimComponent 自然接管。
        if (TurnToAim && dt > 0f && n >= TurnToAimStartNorm) FaceAimTarget(dt);

        if (elapsed >= Duration) EndDodge();
    }

    /// <summary>把朝向 slerp 向瞄准点（瞄准中）/ 移动方向（非瞄准）。复用 AimComponent 同款目标选择，仅在翻滚尾段调。</summary>
    private void FaceAimTarget(float dt)
    {
        if (input == null) return;
        Vector3 targetDir = input.AimHeld ? (input.AimWorldPoint - Owner.Position) : input.MoveWorld;
        targetDir.y = 0f;
        if (targetDir.sqrMagnitude < 1e-4f) return; // 没有目标方向 → 保持当前朝向
        var targetRot = Quaternion.LookRotation(targetDir, Vector3.up);
        float t = 1f - Mathf.Exp(-TurnToAimRate * dt);
        Owner.Rotation = Quaternion.Slerp(Owner.Rotation, targetRot, t);
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
