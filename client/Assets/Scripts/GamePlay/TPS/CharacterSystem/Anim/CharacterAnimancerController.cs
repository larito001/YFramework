using Animancer;
using UnityEngine;

/// <summary>
/// **玩家**动画驱动器。继承 <see cref="LocomotionAnimController"/>（locomotion / death / 分层 / 技能全身覆盖），
/// 在此实现**武器 + 瞄准**的三段式分层：
///   - <see cref="WeaponAnimSet"/> 加载（**仅上身** combat/持枪 pose 跟武器走，切枪时换；Tick 里按 character.WeaponAnimDirty 自治加载）
///   - **下身（Layer 0）**：瞄准时 2D Cartesian mixer（8 方向 strafe，**clip 来自角色级 CharacterAnimSet**，OnCharacterAnimSetLoaded 时 build 一次、切枪不重建；带 SmoothDamp 平滑），非瞄准回退基类 1D locomotion
///   - **上身（Layer 1 mask）**：**全部委托给 <see cref="UpperBodyLayerDriver"/>**——持武器常驻持枪/瞄准 pose + 换弹/后坐力/拿出/收回 one-shot。
///     本类的上身钩子（HasUpperBodyBasePose / UpdateUpperBody / EnterFullBodyOverride / RestoreUpperBodyAfterFullBody）都是转发给 driver 的薄封装。
///   - 武器未配任一持枪 pose 时 driver 不接管，退化为旧式 Layer 1 one-shot（播完淡出整层，基类 3c）。
///
/// 全身覆盖（Die / 技能）在基类；进出全身覆盖经 EnterFullBodyOverride / RestoreUpperBodyAfterFullBody 钩子，由 driver 干净接管上身常驻。
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

    // ── 上身层驱动（Layer 1 的全部持武器逻辑收口在这里；仅 useUpperBodyLayer 时 new 出来）──
    private UpperBodyLayerDriver upperBody;

    public override void Dispose()
    {
        weaponAnimSet = null;
        aimLocomotionMixer = null;
        smoothedAnimMoveX = 0f;
        smoothedAnimMoveY = 0f;
        smoothMoveXVel = 0f;
        smoothMoveYVel = 0f;
        upperBody?.Dispose();
        upperBody = null;
        base.Dispose();
    }

    /// <summary>切 WeaponAnimSet（如果 WeaponComponent 通知）+ 同步上身层启用。下身 aim mixer 不动（角色级，跟 CharacterAnimSet 走）。</summary>
    protected override void PreDrive(Character character)
    {
        if (character.WeaponAnimDirty)
        {
            character.WeaponAnimDirty = false;
            LoadWeaponAnimSet(character.CurrentWeaponAnimSetPath);
            upperBody?.SetWeapon(weaponAnimSet);
            if (!layer0FullBodyActive) upperBody?.SyncActivation(); // 全身覆盖中不升起（由 Restore 接管）
        }
    }

    // ────────────────── 上身层钩子（全部转发给 UpperBodyLayerDriver，分层逻辑集中在那里） ──────────────────

    /// <summary>持武器且配了持枪 pose 时，由 driver 接管常驻上身（基类据此跳过退化 one-shot 生命周期）。</summary>
    protected override bool HasUpperBodyBasePose(Character character) => upperBody != null && upperBody.HasPose;

    /// <summary>玩家初始无武器：Layer 1 静默（weight 0），由首次装备的 driver.SyncActivation 升起。</summary>
    protected override float GetInitialUpperBodyWeight() => 0f;

    /// <summary>进入全身覆盖（死亡/技能）：让上身层让位——driver 独占 Layer 1，降 weight 静默 + 清常驻缓存（保证技能后干净重建）。</summary>
    protected override void EnterFullBodyOverride(float fade, bool immediate)
    {
        if (upperBody != null) upperBody.Silence(fade, immediate); // driver 独占 Layer 1 weight + 清缓存
        else base.EnterFullBodyOverride(fade, immediate);          // 无 driver（无 mask）走基类通用静默
    }

    /// <summary>技能结束从全身覆盖恢复：driver 把 Layer 1 weight 拉回 1 + 当帧重建 base pose（按当前 IsAiming）。</summary>
    protected override void RestoreUpperBodyAfterFullBody(Character character, float recoverFade)
        => upperBody?.Restore(recoverFade);

    /// <summary>每帧驱动上身常驻状态机（base pose + 叠加 one-shot）。</summary>
    protected override void UpdateUpperBody(Character character) => upperBody?.Tick(character);

    /// <summary>武器战斗段。持武器且配了 pose 时短路（trigger 交给 driver.Tick 消费）；
    /// 退化路径（无 pose）沿用旧式 Layer 1 / 单层 one-shot（基类 3c 淡出整层）。无武器仅消费 trigger 防重复。</summary>
    protected override bool DriveCombat(Character character, float fade)
    {
        if (weaponAnimSet == null)
        {
            character.ClearCombatOneShots(); // 没 weaponAnimSet：清空挂起 combat one-shot 避免堆积（取代旧版逐个 bool 手动清）
            return false;
        }

        // 持武器且配了 pose：trigger 由 driver 消费（HasUpperBodyBasePose 为真 -> 基类走 UpdateUpperBody），这里不碰
        if (HasUpperBodyBasePose(character)) return false;

        // 退化路径（武器未配 pose）：复用 driver 的静态 trigger 消费，在 combatLayer 播 one-shot，基类 3c 负责淡出
        if (UpperBodyLayerDriver.TryConsumeCombatTrigger(character, weaponAnimSet, GetDefaultFade(),
                out var clip, out var f, out var speed) && clip != null)
        {
            // 退化模式 Layer 1 初始 weight=0（玩家），播 one-shot 前拉起，基类 3c 播完淡回 0
            if (HasUpperLayer) UpperLayer.StartFade(1f, f);
            var combatLayer = useUpperBodyLayer ? UpperLayer : BaseLayer;
            var s = combatLayer.Play(clip, f);
            if (s != null && speed > 0f) s.Speed = speed;
            activeOneShotState = s;
        }
        return false;
    }

    /// <summary>瞄准时下身用角色级 2D Cartesian strafe mixer（SmoothDamp 平滑）；否则回退基类 1D locomotion。均在 Layer 0。</summary>
    protected override void UpdateLocomotion(Character character)
    {
        if (character.IsAiming && aimLocomotionMixer != null)
        {
            float fade = overrideNextLocomotionFade > 0f ? overrideNextLocomotionFade : GetDefaultFade();
            if (currentLayer0Mixer != aimLocomotionMixer)
            {
                BaseLayer.Play(aimLocomotionMixer, fade);
                currentLayer0Mixer = aimLocomotionMixer;
                overrideNextLocomotionFade = 0f;
            }
            // dt<=0 时**跳过** SmoothDamp：Unity 的 Mathf.SmoothDamp 在"已 settle（current==target）"那一帧会走 overshoot 分支
            // 执行 (output-target)/deltaTime，deltaTime==0 → 0/0 = NaN，把 ref 速度 smoothMove*Vel 永久污染成 NaN，
            // 之后每帧把 NaN 喂给 mixer.ParameterX/Y → ArgumentOutOfRangeException(value must not be NaN/Infinity)。
            // 用 unscaledDeltaTime × 有效缩放（全局缩放不走 Time.timeScale）：暂停 / 卡肉（CurrentTimeScale=0）/
            // 首帧 / 编辑器刚恢复时 dt=0，而 LateUpdate 仍会跑。dt<=0 时沿用上一帧平滑值即可。
            float dt = Time.unscaledDeltaTime * CurrentTimeScale;
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

    /// <summary>CharacterAnimSet 加载完（spawn 一次）：构造角色级瞄准 aim mixer（下身 strafe，跟武器无关）+ 创建上身层驱动（仅配了 UpperBodyMask 时）。</summary>
    protected override void OnCharacterAnimSetLoaded()
    {
        BuildAimMixer();
        upperBody = HasUpperLayer
            ? new UpperBodyLayerDriver(UpperLayer, characterAnimSet.UpperBodyMask, GetDefaultFade())
            : null;
    }

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
