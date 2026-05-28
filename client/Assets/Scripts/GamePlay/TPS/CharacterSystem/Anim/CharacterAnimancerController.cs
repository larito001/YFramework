using Animancer;
using UnityEngine;
using YOTO;

/// <summary>
/// 角色动画驱动器。封装从 <see cref="Character"/> 字段到 Animancer Playables 输出 的全部逻辑：
///   - 双 AnimSet 协同：<see cref="CharacterAnimSet"/>（locomotion/death/mask 跟角色走）+ <see cref="WeaponAnimSet"/>（combat/aim 跟武器走）
///   - Locomotion mixer 构造（1D 非瞄准用 character set + 2D Cartesian 瞄准 8 方向用 weapon set）
///   - 上下身 Layer + AvatarMask 分离（mask 来自 character set）
///   - Trigger 优先级状态机：Die（character set） &gt; Melee &gt; Holster &gt; Equip &gt; Reload &gt; Shoot（weapon set） &gt; Locomotion
///   - mixer Parameter SmoothDamp 平滑（避免转向硬切）
///
/// **不持 GameObject / Transform 引用**——只通过 <see cref="AnimancerComponent"/> 间接驱动 Animator。
/// 跟 view 通过事件交互（<see cref="OnDeathTriggered"/>）：view 订阅事件做 CC disable。
///
/// 生命周期：<see cref="Init"/>（含 characterAnimSetPath 一次性加载）→ 每帧 <see cref="Tick"/>（自治加载 weaponAnimSet）→ <see cref="Dispose"/>。
/// </summary>
public class CharacterAnimancerController
{
    public AnimancerComponent Animancer { get; private set; }

    /// <summary>Aim mixer ParameterX/Y 的 SmoothDamp 时间。MoveComponent 转向瞬切，没 damp 会导致 mixer 9 child 权重瞬变硬切。</summary>
    public float AnimMoveDampTime = 0.1f;

    /// <summary>Die trigger 在 controller 内部消费后触发。view 订阅做 CC disable / 关物理碰撞等 GameObject 级响应。</summary>
    public event System.Action OnDeathTriggered;

    private ResMgr resMgr;

    // ── 双 AnimSet ──
    private CharacterAnimSet characterAnimSet;
    private WeaponAnimSet weaponAnimSet;

    // ── Animancer 状态 ──
    private LinearMixerState locomotionMixer;       // 非瞄准 locomotion（用 characterAnimSet 的 clip）
    private CartesianMixerState aimLocomotionMixer; // 瞄准 8 方向（用 weaponAnimSet 的 clip）
    /// <summary>当前 Layer 0 在播的 mixer 引用。</summary>
    private AnimancerState currentLayer0Mixer;
    /// <summary>当前激活的一次性 state（Shoot/Reload/Equip/Holster/Melee/Die）。未播完时 Combat layer / Locomotion 不打断。</summary>
    private AnimancerState activeOneShotState;
    /// <summary>true = activeOneShotState 在 Layer 0 全身覆盖（Melee/Die）—— Locomotion mixer 暂停切换避免打断。</summary>
    private bool layer0FullBodyActive;
    /// <summary>true = characterAnimSet 配了 UpperBodyMask，启用 Layer 1 上下身分离。</summary>
    private bool useUpperBodyLayer;

    // Aim mixer SmoothDamp
    private float smoothedAnimMoveX;
    private float smoothedAnimMoveY;
    private float smoothMoveXVel;
    private float smoothMoveYVel;

    /// <summary>view 在 Bind 后立即调一次。characterAnimSetPath 一次性加载（角色 spawn 时设定，运行时不换）。
    /// weaponAnimSet 在 Tick 里通过 character.WeaponAnimDirty 自治加载。</summary>
    public void Init(AnimancerComponent animancer, ResMgr resMgr, string characterAnimSetPath)
    {
        Animancer = animancer;
        this.resMgr = resMgr;
        LoadCharacterAnimSet(characterAnimSetPath);
    }

    /// <summary>每帧驱动。</summary>
    public void Tick(Character character, float globalTimeScale)
    {
        if (Animancer == null || character == null) return;

        // 切 WeaponAnimSet（如果 WeaponComponent 通知）
        if (character.WeaponAnimDirty)
        {
            character.WeaponAnimDirty = false;
            LoadWeaponAnimSet(character.CurrentWeaponAnimSetPath);
        }

        // 全局动画速度（卡肉 / 局部慢动作）
        Animancer.Graph.Speed = globalTimeScale;

        // characterAnimSet 没加载就跳——locomotion / death 都依赖它
        if (characterAnimSet == null) return;
        DriveAnimation(character);
    }

    public void Dispose()
    {
        OnDeathTriggered = null;
        characterAnimSet = null;
        weaponAnimSet = null;
        locomotionMixer = null;
        aimLocomotionMixer = null;
        currentLayer0Mixer = null;
        activeOneShotState = null;
        layer0FullBodyActive = false;
        useUpperBodyLayer = false;
        smoothedAnimMoveX = 0f;
        smoothedAnimMoveY = 0f;
        smoothMoveXVel = 0f;
        smoothMoveYVel = 0f;
        resMgr = null;
        Animancer = null;
    }

    /// <summary>加载 CharacterAnimSet（角色 spawn 时一次）+ 构造 locomotion mixer + 配 Layer 1 mask。</summary>
    private void LoadCharacterAnimSet(string path)
    {
        characterAnimSet = null;
        locomotionMixer = null;
        useUpperBodyLayer = false;

        if (string.IsNullOrEmpty(path)) return;
        if (resMgr == null)
        {
            Debug.LogWarning("[CharacterAnimancerController] ResMgr 未拿到 —— 无法 Load CharacterAnimSet");
            return;
        }
        var set = resMgr.Load<CharacterAnimSet>(path);
        if (set == null)
        {
            Debug.LogWarning($"[CharacterAnimancerController] CharacterAnimSet 加载失败: {path}");
            return;
        }

        characterAnimSet = set;
        BuildLocomotionMixer();
        SetupUpperBodyLayer();
    }

    /// <summary>加载 WeaponAnimSet（切枪时换）+ 重建 aim mixer。non-aim locomotion mixer 不动（跟 character 走）。</summary>
    private void LoadWeaponAnimSet(string path)
    {
        weaponAnimSet = null;
        aimLocomotionMixer = null;
        // 不清 currentLayer0Mixer 也不清 activeOneShotState——切枪不该打断下半身 locomotion，combat trigger 自然下一帧覆盖

        if (string.IsNullOrEmpty(path)) return;
        if (resMgr == null) return;
        var set = resMgr.Load<WeaponAnimSet>(path);
        if (set == null)
        {
            Debug.LogWarning($"[CharacterAnimancerController] WeaponAnimSet 加载失败: {path}");
            return;
        }

        weaponAnimSet = set;
        BuildAimMixer();
    }

    /// <summary>构造非瞄准 locomotion mixer：LinearMixerState 4 child Idle/Walk/Run/Sprint，按 AnimSpeedRatio 真实 m/s blend。
    /// idle child 不参与 SynchronizeChildren——idle 没移动节奏。</summary>
    private void BuildLocomotionMixer()
    {
        if (characterAnimSet == null || characterAnimSet.Idle == null || characterAnimSet.Walk == null) return;

        locomotionMixer = new LinearMixerState();
        var idle = characterAnimSet.Idle;
        var walk = characterAnimSet.Walk;
        var run = characterAnimSet.Run != null ? characterAnimSet.Run : walk;
        var sprint = characterAnimSet.Sprint != null ? characterAnimSet.Sprint : run;
        locomotionMixer.AddRange(idle, walk, run, sprint);
        locomotionMixer.SetThresholds(
            characterAnimSet.IdleThreshold,
            characterAnimSet.WalkThreshold,
            characterAnimSet.RunThreshold,
            characterAnimSet.SprintThreshold);
        var idleChild = locomotionMixer.GetChild(0);
        if (idleChild != null) locomotionMixer.DontSynchronize(idleChild);
    }

    /// <summary>构造瞄准 aim mixer：CartesianMixerState 9 child（idle 中心 + 8 方向 strafe），按 (AnimMoveX, AnimMoveY) 2D blend。
    /// 替代旧 BlendTree 2D。null 方向 clip 用 AimWalk/AimWalkFwd/Bwd 兜底。</summary>
    private void BuildAimMixer()
    {
        if (weaponAnimSet == null || weaponAnimSet.AimIdle == null) return;

        var fallback = weaponAnimSet.AimWalk != null ? weaponAnimSet.AimWalk : weaponAnimSet.AimIdle;
        var fwd = weaponAnimSet.AimWalkFwd != null ? weaponAnimSet.AimWalkFwd : fallback;
        var bwd = weaponAnimSet.AimWalkBwd != null ? weaponAnimSet.AimWalkBwd : fallback;
        var right = weaponAnimSet.AimStrafeRight != null ? weaponAnimSet.AimStrafeRight : fallback;
        var left = weaponAnimSet.AimStrafeLeft != null ? weaponAnimSet.AimStrafeLeft : fallback;
        var fr = weaponAnimSet.AimStrafeFR != null ? weaponAnimSet.AimStrafeFR : fwd;
        var fl = weaponAnimSet.AimStrafeFL != null ? weaponAnimSet.AimStrafeFL : fwd;
        var br = weaponAnimSet.AimStrafeBR != null ? weaponAnimSet.AimStrafeBR : bwd;
        var bl = weaponAnimSet.AimStrafeBL != null ? weaponAnimSet.AimStrafeBL : bwd;

        aimLocomotionMixer = new CartesianMixerState();
        aimLocomotionMixer.AddRange(weaponAnimSet.AimIdle, fwd, fr, right, br, bwd, bl, left, fl);
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

    /// <summary>UpperBodyMask 配了启用 Layer 1 上半身分离。mask 来自 CharacterAnimSet（角色级，不跟武器走）。</summary>
    private void SetupUpperBodyLayer()
    {
        useUpperBodyLayer = characterAnimSet != null && characterAnimSet.UpperBodyMask != null;
        if (useUpperBodyLayer)
        {
            var layer = Animancer.Layers[1];
            layer.Mask = characterAnimSet.UpperBodyMask;
            layer.SetWeight(1f);
        }
        else if (Animancer.Layers.Count > 1)
        {
            Animancer.Layers[1].StartFade(0f, GetDefaultFade());
        }
    }

    private float GetDefaultFade() => characterAnimSet != null ? characterAnimSet.DefaultFade : 0.1f;

    /// <summary>核心状态机。优先级 Die &gt; Melee &gt; Holster &gt; Equip &gt; Reload &gt; Shoot &gt; Locomotion。
    /// Die / DeathL/R 走 CharacterAnimSet；Combat (Shoot/Reload/Equip/Holster/Melee) 走 WeaponAnimSet。</summary>
    private void DriveAnimation(Character character)
    {
        var combatLayer = useUpperBodyLayer ? Animancer.Layers[1] : Animancer.Layers[0];
        var fade = GetDefaultFade();

        // 1. 死亡：全身 Layer 0 覆盖 + Layer 1 weight=0 + fade=0 立即切。clip 来自 CharacterAnimSet（不依赖 weapon）
        if (character.Die)
        {
            character.Die = false;
            var clip = character.DeathVariant == 0 ? characterAnimSet.DeathL : characterAnimSet.DeathR;
            if (clip != null)
            {
                Animancer.Layers[0].SetWeight(1f);
                activeOneShotState = Animancer.Layers[0].Play(clip, 0f);
            }
            if (useUpperBodyLayer && Animancer.Layers.Count > 1) Animancer.Layers[1].SetWeight(0f);
            currentLayer0Mixer = null;
            layer0FullBodyActive = true;
            OnDeathTriggered?.Invoke();
            return;
        }
        if (character.IsDead) return;

        // 后续 combat trigger 都需要 weaponAnimSet（如果没武器就跳过 combat，仍跑 locomotion）
        if (weaponAnimSet != null)
        {
            // 2. 近战（全身覆盖——Melee 包含全身动作）
            // **连击重置语义**：Animancer 的 Play(clip, fade) 若该 clip 已是 active state，会返回旧 state 但 **不重置 Time**，
            // 连按 V 时动画会"卡在挥击中段"循环看起来停了。这里 Play 后显式 state.Time=0 让 clip 每次都从头播。
            if (character.MeleeAttack)
            {
                character.MeleeAttack = false;
                var clip = character.MeleeType == 0 ? weaponAnimSet.MeleeHard : weaponAnimSet.MeleeKick;
                if (clip != null)
                {
                    var state = Animancer.Layers[0].Play(clip, fade);
                    if (state != null) state.Time = 0f; // 连击打断重启：强制从头播
                    activeOneShotState = state;
                    layer0FullBodyActive = true;
                    currentLayer0Mixer = null;
                }
                if (useUpperBodyLayer && Animancer.Layers.Count > 1) Animancer.Layers[1].StartFade(0f, fade);
                return;
            }
            else if (character.WeaponHolster)
            {
                character.WeaponHolster = false;
                if (weaponAnimSet.Holster != null) activeOneShotState = combatLayer.Play(weaponAnimSet.Holster, fade);
            }
            else if (character.WeaponSwap)
            {
                character.WeaponSwap = false;
                if (weaponAnimSet.Equip != null) activeOneShotState = combatLayer.Play(weaponAnimSet.Equip, fade);
            }
            else if (character.Reload)
            {
                character.Reload = false;
                if (weaponAnimSet.Reload != null) activeOneShotState = combatLayer.Play(weaponAnimSet.Reload, fade);
            }
            else if (character.Shoot)
            {
                character.Shoot = false;
                var clip = character.HeavyRecoil ? weaponAnimSet.ShootHeavy : weaponAnimSet.ShootLight;
                if (clip != null)
                {
                    var s = combatLayer.Play(clip, weaponAnimSet.ShootFade);
                    if (s != null && character.RecoilAnimSpeed > 0f) s.Speed = character.RecoilAnimSpeed;
                    activeOneShotState = s;
                }
            }
        }
        else
        {
            // 没 weaponAnimSet 时消费 combat trigger 避免下一帧重复触发
            if (character.MeleeAttack) character.MeleeAttack = false;
            if (character.WeaponHolster) character.WeaponHolster = false;
            if (character.WeaponSwap) character.WeaponSwap = false;
            if (character.Reload) character.Reload = false;
            if (character.Shoot) character.Shoot = false;
        }

        // 7a. Layer 0 全身覆盖中（Melee）：等待播完才回 Locomotion mixer
        if (layer0FullBodyActive)
        {
            if (activeOneShotState != null && activeOneShotState.IsPlaying && activeOneShotState.NormalizedTime < 1f)
                return;
            layer0FullBodyActive = false;
            if (useUpperBodyLayer && Animancer.Layers.Count > 1) Animancer.Layers[1].SetWeight(1f);
            activeOneShotState = null;
        }
        // 7b. 单层模式 + Combat one-shot 未播完：等待
        else if (!useUpperBodyLayer && activeOneShotState != null && activeOneShotState.IsPlaying && activeOneShotState.NormalizedTime < 1f)
        {
            return;
        }
        // 7c. 双层模式 + Combat one-shot 播完：淡出 Layer 1
        else if (activeOneShotState != null && (activeOneShotState.NormalizedTime >= 1f || !activeOneShotState.IsPlaying))
        {
            if (useUpperBodyLayer && Animancer.Layers.Count > 1) Animancer.Layers[1].StartFade(0f, fade);
            activeOneShotState = null;
        }

        // 8. Locomotion always-on
        UpdateLocomotion(character);
    }

    /// <summary>Locomotion 驱动：
    /// 非瞄准 → LinearMixer 1D (characterAnimSet) 按 AnimSpeedRatio 真实 m/s blend；
    /// 瞄准 → CartesianMixer 2D (weaponAnimSet) 按 SmoothDamp 平滑后的 (AnimMoveX, AnimMoveY) blend。
    /// 瞄准时如果没 weaponAnimSet aim mixer，回退到非瞄准 locomotion mixer。</summary>
    private void UpdateLocomotion(Character character)
    {
        if (character.IsAiming && aimLocomotionMixer != null)
        {
            if (currentLayer0Mixer != aimLocomotionMixer)
            {
                Animancer.Layers[0].Play(aimLocomotionMixer, GetDefaultFade());
                currentLayer0Mixer = aimLocomotionMixer;
            }
            smoothedAnimMoveX = Mathf.SmoothDamp(smoothedAnimMoveX, character.AnimMoveX, ref smoothMoveXVel, AnimMoveDampTime, Mathf.Infinity, Time.deltaTime);
            smoothedAnimMoveY = Mathf.SmoothDamp(smoothedAnimMoveY, character.AnimMoveY, ref smoothMoveYVel, AnimMoveDampTime, Mathf.Infinity, Time.deltaTime);
            aimLocomotionMixer.ParameterX = smoothedAnimMoveX;
            aimLocomotionMixer.ParameterY = smoothedAnimMoveY;
        }
        else if (locomotionMixer != null)
        {
            if (currentLayer0Mixer != locomotionMixer)
            {
                Animancer.Layers[0].Play(locomotionMixer, GetDefaultFade());
                currentLayer0Mixer = locomotionMixer;
            }
            locomotionMixer.Parameter = character.AnimSpeedRatio;
        }
    }
}
