using Animancer;
using UnityEngine;

/// <summary>
/// **玩家**动画驱动器。继承 <see cref="LocomotionAnimController"/>（locomotion / death / 分层 / 技能全身覆盖），
/// 在此实现**武器 + 瞄准**的三段式分层：
///   - <see cref="WeaponAnimSet"/> 加载（**仅上身** combat/持枪 pose 跟武器走，切枪时换；Tick 里按 character.WeaponAnimDirty 自治加载）
///   - **下身（Layer 0）**：瞄准时 2D Cartesian mixer（8 方向 strafe，**clip 来自角色级 CharacterAnimSet**，OnCharacterAnimSetLoaded 时 build 一次、切枪不重建；带 SmoothDamp 平滑），非瞄准回退基类 1D locomotion
///   - **上身（Layer 1 mask）**：持武器时**常驻** IdleGunPose（站立持枪）↔ AimPose（瞄准持枪），按 IsAiming 切换；
///     Holster/Equip/Reload/Shoot 作为 one-shot 替换式叠在常驻 pose 上，播完回到 base pose。装备/卸下武器时整层 weight 0↔1 过渡。
///   - 武器未配任一持枪 pose 时退化为旧式 Layer 1 one-shot（播完淡出整层，基类 3c）。
///
/// 全身覆盖（Die / 技能）在基类；进出全身覆盖经 OnFullBodyOverrideEnter / RestoreUpperBodyAfterFullBody 钩子干净接管上身常驻。
/// </summary>
public class CharacterAnimancerController : LocomotionAnimController
{
    /// <summary>Aim mixer ParameterX/Y 的 SmoothDamp 时间。MoveComponent 转向瞬切，没 damp 会导致 mixer 9 child 权重瞬变硬切。</summary>
    public float AnimMoveDampTime = 0.1f;

    // ── WeaponAnimSet（跟武器走）──
    private WeaponAnimSet weaponAnimSet;
    private CartesianMixerState aimLocomotionMixer; // 瞄准 8 方向（用 weaponAnimSet 的 clip）

    // Aim mixer SmoothDamp
    private float smoothedAnimMoveX;
    private float smoothedAnimMoveY;
    private float smoothMoveXVel;
    private float smoothMoveYVel;

    // ── Layer 1 上身常驻状态（站立持枪/瞄准持枪 base pose + 叠加 one-shot 回 base）──
    private AnimancerState upperBaseState;     // 当前常驻 base pose state（IdleGunPose 或 AimPose）
    private bool upperBaseIsAiming;            // upperBaseState 当前是 AimPose(true) 还是 IdleGunPose(false)
    private bool upperBodyActive;              // Layer 1 当前应常驻（持武器 + 配了 base pose）
    private AnimancerState upperOneShotState;  // 叠在 base 上的 one-shot（Reload/Equip/Holster/Shoot）；null=无
    private bool upperBaseFromZero;            // true=Layer 1 刚从 weight0 进入，base 首 Play 用 EnterFade

    public override void Dispose()
    {
        weaponAnimSet = null;
        aimLocomotionMixer = null;
        smoothedAnimMoveX = 0f;
        smoothedAnimMoveY = 0f;
        smoothMoveXVel = 0f;
        smoothMoveYVel = 0f;
        upperBaseState = null;
        upperBaseIsAiming = false;
        upperBodyActive = false;
        upperOneShotState = null;
        upperBaseFromZero = false;
        base.Dispose();
    }

    /// <summary>切 WeaponAnimSet（如果 WeaponComponent 通知）+ 同步 Layer 1 上身常驻启用。non-aim locomotion mixer 不动（跟 character 走）。</summary>
    protected override void PreDrive(Character character)
    {
        if (character.WeaponAnimDirty)
        {
            character.WeaponAnimDirty = false;
            LoadWeaponAnimSet(character.CurrentWeaponAnimSetPath);
            SyncUpperBodyActivation();
        }
    }

    // ────────────────────────── 上身常驻 pose 钩子（override 基类） ──────────────────────────

    /// <summary>持武器且配了 base pose（IdleGunPose/AimPose）时启用上身常驻状态机。</summary>
    protected override bool HasUpperBodyBasePose(Character character) => HasWeaponUpperBody();

    /// <summary>玩家初始无武器：Layer 1 静默（weight 0），由首次装备的 <see cref="SyncUpperBodyActivation"/> 升起。</summary>
    protected override float GetInitialUpperBodyWeight() => 0f;

    /// <summary>进入全身覆盖（死亡/技能）：清上身常驻缓存，避免恢复时残留 one-shot / 旧 pose。
    /// Layer 1 weight 由基类死亡/技能分支淡到 0；恢复由 <see cref="RestoreUpperBodyAfterFullBody"/> 接管。</summary>
    protected override void OnFullBodyOverrideEnter()
    {
        upperBaseState = null;
        upperOneShotState = null;
        upperBodyActive = false;
        upperBaseFromZero = false;
    }

    /// <summary>技能结束从全身覆盖恢复：Layer 1 weight 拉回 1，当帧由 <see cref="UpdateUpperBody"/> 重建 base pose（按当前 IsAiming）。</summary>
    protected override void RestoreUpperBodyAfterFullBody(Character character, float recoverFade)
    {
        if (!(useUpperBodyLayer && Animancer.Layers.Count > 1)) return;
        if (!HasWeaponUpperBody()) return; // 武器没配 base pose：保持淡出，走退化路径
        Animancer.Layers[1].StartFade(1f, recoverFade);
        upperBodyActive = true;
        upperBaseFromZero = false; // weight 已在过渡，用 AimPoseFade crossfade 回 base
        upperBaseState = null;     // 强制 UpdateUpperBody 当帧重建 base
        upperOneShotState = null;
    }

    /// <summary>Layer 1 上身常驻状态机：one-shot 叠加（消费 combat trigger）+ 无 one-shot 时维持 base pose。</summary>
    protected override void UpdateUpperBody(Character character)
    {
        if (!upperBodyActive) return; // 理论上 HasUpperBodyBasePose 为真即 active；防御
        DriveUpperBodyOneShot(character);
        if (upperOneShotState == null) DriveUpperBodyBase(character); // one-shot 期间不切 base
    }

    /// <summary>武器配了任一上身常驻 pose（且启用了 Layer 1）。</summary>
    private bool HasWeaponUpperBody()
        => weaponAnimSet != null && useUpperBodyLayer
           && (weaponAnimSet.IdleGunPose != null || weaponAnimSet.AimPose != null);

    /// <summary>武器切换后同步 Layer 1 上身常驻：持武器配 pose -> 升 weight 常驻；否则淡出回纯 locomotion。
    /// 全身覆盖中（skill/death）跳过——Layer 1 由 <see cref="RestoreUpperBodyAfterFullBody"/> 接管。</summary>
    private void SyncUpperBodyActivation()
    {
        if (!(useUpperBodyLayer && Animancer.Layers.Count > 1)) return;
        if (layer0FullBodyActive) return;

        bool hasUpper = HasWeaponUpperBody();
        if (hasUpper && !upperBodyActive)
        {
            Animancer.Layers[1].StartFade(1f, weaponAnimSet.UpperBodyEnterFade);
            upperBodyActive = true;
            upperBaseFromZero = true;
            upperBaseState = null;
            upperOneShotState = null;
        }
        else if (!hasUpper && upperBodyActive)
        {
            float exitFade = weaponAnimSet != null ? weaponAnimSet.UpperBodyExitFade : GetDefaultFade();
            Animancer.Layers[1].StartFade(0f, exitFade);
            upperBodyActive = false;
            upperBaseState = null;
            upperOneShotState = null;
            upperBaseIsAiming = false;
        }
    }

    /// <summary>消费 combat trigger -> 在 Layer 1 替换式叠加 one-shot；one-shot 播完置空（下句 DriveUpperBodyBase 重建 base）。</summary>
    private void DriveUpperBodyOneShot(Character character)
    {
        if (TryConsumeCombatTrigger(character, out var clip, out var fade, out var speed))
        {
            if (clip != null)
            {
                var s = Animancer.Layers[1].Play(clip, fade);
                if (s != null)
                {
                    s.Time = 0f;                    // 每次从头播（连发后坐力 / 重新换弹）
                    if (speed > 0f) s.Speed = speed; // 后坐力倍率
                }
                upperOneShotState = s;
            }
            // clip==null：trigger 已消费，不播，保持当前 base
        }

        // one-shot 播完 -> 回 base（按当时最新 IsAiming，天然处理 one-shot 期间瞄准切换）
        if (upperOneShotState != null &&
            (!upperOneShotState.IsPlaying || upperOneShotState.NormalizedTime >= 1f))
        {
            upperOneShotState = null;
            upperBaseState = null; // upperBaseFromZero 保持 false -> 回 base 用 AimPoseFade crossfade
        }
    }

    /// <summary>维持 Layer 1 常驻 base pose；按 IsAiming 在 IdleGunPose &lt;-&gt; AimPose 切换。两者互为兜底。</summary>
    private void DriveUpperBodyBase(Character character)
    {
        bool wantAiming = character.IsAiming;
        AnimationClip target = wantAiming ? weaponAnimSet.AimPose : weaponAnimSet.IdleGunPose;
        if (target == null) target = wantAiming ? weaponAnimSet.IdleGunPose : weaponAnimSet.AimPose;
        if (target == null) return;

        if (upperBaseState == null || upperBaseIsAiming != wantAiming || upperBaseState.Clip != target)
        {
            float fade = upperBaseFromZero ? weaponAnimSet.UpperBodyEnterFade : weaponAnimSet.AimPoseFade;
            upperBaseState = Animancer.Layers[1].Play(target, fade);
            upperBaseIsAiming = wantAiming;
            upperBaseFromZero = false;
        }
    }

    /// <summary>按优先级 Holster &gt; Equip &gt; Reload &gt; Shoot 消费一个 combat trigger。
    /// 返回 true=本帧有 trigger（clip 可能为 null，表示该动作未配 clip——trigger 已消费，调用方不播）。
    /// 退化路径（无 base pose）与上身常驻路径共用此消费逻辑，保证单一消费方、优先级一致。</summary>
    private bool TryConsumeCombatTrigger(Character character, out AnimationClip clip, out float fade, out float speed)
    {
        clip = null; fade = GetDefaultFade(); speed = 0f;
        if (character.WeaponHolster)
        {
            character.WeaponHolster = false;
            clip = weaponAnimSet.Holster;
            return true;
        }
        if (character.WeaponSwap)
        {
            character.WeaponSwap = false;
            clip = weaponAnimSet.Equip;
            return true;
        }
        if (character.Reload)
        {
            character.Reload = false;
            clip = weaponAnimSet.Reload;
            return true;
        }
        if (character.Shoot)
        {
            character.Shoot = false;
            clip = character.HeavyRecoil ? weaponAnimSet.ShootHeavy : weaponAnimSet.ShootLight;
            fade = weaponAnimSet.ShootFade;
            speed = character.RecoilAnimSpeed;
            return true;
        }
        return false;
    }

    /// <summary>武器战斗段。持武器且配了 base pose 时短路（trigger 交给 <see cref="UpdateUpperBody"/> 消费）；
    /// 退化路径（无 base pose）沿用旧式 Layer 1 / 单层 one-shot（基类 3c 淡出整层）。无武器仅消费 trigger 防重复。</summary>
    protected override bool DriveCombat(Character character, float fade)
    {
        if (weaponAnimSet == null)
        {
            // 没 weaponAnimSet 时消费 combat trigger 避免下一帧重复触发
            if (character.WeaponHolster) character.WeaponHolster = false;
            if (character.WeaponSwap) character.WeaponSwap = false;
            if (character.Reload) character.Reload = false;
            if (character.Shoot) character.Shoot = false;
            return false;
        }

        // 持武器且配了 base pose：trigger 由 UpdateUpperBody -> DriveUpperBodyOneShot 消费，这里不碰
        if (HasUpperBodyBasePose(character)) return false;

        // 退化路径（武器未配 base pose）：在 combatLayer 播 one-shot，基类 3c 负责淡出
        var combatLayer = useUpperBodyLayer ? Animancer.Layers[1] : Animancer.Layers[0];
        if (TryConsumeCombatTrigger(character, out var clip, out var f, out var speed) && clip != null)
        {
            // 退化模式 Layer 1 初始 weight=0（玩家），播 one-shot 前需拉起，基类 3c 播完淡回 0
            if (useUpperBodyLayer && Animancer.Layers.Count > 1) Animancer.Layers[1].StartFade(1f, f);
            var s = combatLayer.Play(clip, f);
            if (s != null && speed > 0f) s.Speed = speed;
            activeOneShotState = s;
        }
        return false;
    }

    /// <summary>瞄准时用 weaponAnimSet 的 2D Cartesian mixer（SmoothDamp 平滑）；否则回退基类 1D locomotion。</summary>
    protected override void UpdateLocomotion(Character character)
    {
        if (character.IsAiming && aimLocomotionMixer != null)
        {
            float fade = overrideNextLocomotionFade > 0f ? overrideNextLocomotionFade : GetDefaultFade();
            if (currentLayer0Mixer != aimLocomotionMixer)
            {
                Animancer.Layers[0].Play(aimLocomotionMixer, fade);
                currentLayer0Mixer = aimLocomotionMixer;
                overrideNextLocomotionFade = 0f;
            }
            // dt<=0 时**跳过** SmoothDamp：Unity 的 Mathf.SmoothDamp 在"已 settle（current==target）"那一帧会走 overshoot 分支
            // 执行 (output-target)/deltaTime，deltaTime==0 → 0/0 = NaN，把 ref 速度 smoothMove*Vel 永久污染成 NaN，
            // 之后每帧把 NaN 喂给 mixer.ParameterX/Y → ArgumentOutOfRangeException(value must not be NaN/Infinity)。
            // Time.deltaTime 在暂停（Time.timeScale=0）/ 首帧 / 编辑器刚恢复时为 0，而 LateUpdate 仍会跑。dt<=0 时沿用上一帧平滑值即可。
            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                smoothedAnimMoveX = Mathf.SmoothDamp(smoothedAnimMoveX, character.AnimMoveX, ref smoothMoveXVel, AnimMoveDampTime, Mathf.Infinity, dt);
                smoothedAnimMoveY = Mathf.SmoothDamp(smoothedAnimMoveY, character.AnimMoveY, ref smoothMoveYVel, AnimMoveDampTime, Mathf.Infinity, dt);
            }
            aimLocomotionMixer.ParameterX = smoothedAnimMoveX;
            aimLocomotionMixer.ParameterY = smoothedAnimMoveY;
        }
        else
        {
            base.UpdateLocomotion(character);
        }
    }

    /// <summary>加载 WeaponAnimSet（切枪时换，仅上身）。aim strafe mixer 是角色级（CharacterAnimSet），切枪不重建。</summary>
    private void LoadWeaponAnimSet(string path)
    {
        weaponAnimSet = null;
        // 不动 aimLocomotionMixer（角色级，跟 CharacterAnimSet 走）、currentLayer0Mixer、activeOneShotState
        // ——切枪不该打断下半身 locomotion，combat trigger 自然下一帧覆盖

        if (string.IsNullOrEmpty(path)) return;
        if (resMgr == null) return;
        var set = resMgr.Load<WeaponAnimSet>(path);
        if (set == null)
        {
            Debug.LogWarning($"[CharacterAnimancerController] WeaponAnimSet 加载失败: {path}");
            return;
        }

        weaponAnimSet = set;
    }

    /// <summary>CharacterAnimSet 加载完构造瞄准 aim mixer（角色级，一次性）。下半身 strafe 跟武器无关，全角色共用。</summary>
    protected override void OnCharacterAnimSetLoaded() => BuildAimMixer();

    /// <summary>构造瞄准 aim mixer：CartesianMixerState 9 child（idle 中心 + 8 方向 strafe），按 (AnimMoveX, AnimMoveY) 2D blend。
    /// clip 来自 <see cref="LocomotionAnimController.characterAnimSet"/>（角色级）。null 方向 clip 用 AimWalk/AimWalkFwd/Bwd 兜底。</summary>
    private void BuildAimMixer()
    {
        aimLocomotionMixer = null;
        if (characterAnimSet == null || characterAnimSet.AimIdle == null) return;

        var fallback = characterAnimSet.AimWalk != null ? characterAnimSet.AimWalk : characterAnimSet.AimIdle;
        var fwd = characterAnimSet.AimWalkFwd != null ? characterAnimSet.AimWalkFwd : fallback;
        var bwd = characterAnimSet.AimWalkBwd != null ? characterAnimSet.AimWalkBwd : fallback;
        var right = characterAnimSet.AimStrafeRight != null ? characterAnimSet.AimStrafeRight : fallback;
        var left = characterAnimSet.AimStrafeLeft != null ? characterAnimSet.AimStrafeLeft : fallback;
        var fr = characterAnimSet.AimStrafeFR != null ? characterAnimSet.AimStrafeFR : fwd;
        var fl = characterAnimSet.AimStrafeFL != null ? characterAnimSet.AimStrafeFL : fwd;
        var br = characterAnimSet.AimStrafeBR != null ? characterAnimSet.AimStrafeBR : bwd;
        var bl = characterAnimSet.AimStrafeBL != null ? characterAnimSet.AimStrafeBL : bwd;

        aimLocomotionMixer = new CartesianMixerState();
        aimLocomotionMixer.AddRange(characterAnimSet.AimIdle, fwd, fr, right, br, bwd, bl, left, fl);
        const float d = 0.7071f;
        aimLocomotionMixer.SetThresholds(
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(d, d),
            new Vector2(1f, 0f),
            new Vector2(d, -d),
            new Vector2(0f, -1f),
            new Vector2(-d, -d),
            new Vector2(-1f, 0f),
            new Vector2(-d, d));
        var idleChild = aimLocomotionMixer.GetChild(0);
        if (idleChild != null) aimLocomotionMixer.DontSynchronize(idleChild);
    }
}
