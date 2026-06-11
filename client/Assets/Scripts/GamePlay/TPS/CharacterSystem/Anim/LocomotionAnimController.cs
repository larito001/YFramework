using System.Collections.Generic;
using Animancer;
using UnityEngine;
using YOTO;

/// <summary>
/// 动画驱动器**基类**：封装所有持骨架 Actor（玩家 + 怪物）共通的动画层：
///   - <see cref="CharacterAnimSet"/> 加载（locomotion / death / UpperBodyMask）
///   - Locomotion mixer 构造（LinearMixerState 1D，可退化：完整 4 档 / 单段移动 / idle-only / 无）
///   - 上下身 Layer + AvatarMask 分离
///   - Die 优先级分支（用 <see cref="Actor"/> 基类字段 Die/DeathVariant/IsDead）
///   - **全身动作分支**（通用，kind-agnostic）：技能/闪避/受击等逻辑组件经 <see cref="Character.FullBody"/> 通道交 clip，这里全身覆盖播放，FullBody.Kind != None 期间锁全身
///   - one-shot 生命周期：全身锁定型（技能）+ 播完即回型（Shoot/Reload）
///   - mixer Parameter 平滑由派生类决定
///
/// **战斗段（武器 one-shot）**通过 virtual 钩子 <see cref="DriveCombat"/> 下放给派生类：
///   - <see cref="CharacterAnimancerController"/>：玩家武器/瞄准（weaponAnimSet + Aim 2D mixer + Shoot/Reload/Equip/Holster）
///   - <see cref="ZombieAnimancerController"/>：僵尸/AI，无武器无瞄准（locomotion + 技能全在基类，它是空具体子类）
///
/// **不持 GameObject / Transform 引用**——只通过 <see cref="AnimancerComponent"/> 间接驱动 Animator。
/// 跟 view 通过 <see cref="OnDeathTriggered"/> 事件交互（view 订阅做 CC disable）。
///
/// 生命周期：<see cref="Init"/>（characterAnimSetPath 一次性加载）→ 每帧 <see cref="Tick"/> → <see cref="Dispose"/>。
/// </summary>
public abstract class LocomotionAnimController : IFullBodyHost
{
    public AnimancerComponent Animancer { get; private set; }

    /// <summary>Die trigger 在内部消费后触发。view 订阅做 CC disable / 关物理碰撞等 GameObject 级响应。</summary>
    public event System.Action OnDeathTriggered;

    protected ResMgr resMgr;

    // ── 角色级 AnimSet（locomotion / death / mask，跟角色走，玩家+怪物共用同一资源类型）──
    protected CharacterAnimSet characterAnimSet;

    // ── 共通 Animancer 状态 ──
    protected LinearMixerState locomotionMixer;       // 非瞄准 locomotion（1D，按 AnimSpeedRatio blend）
    /// <summary>当前 Layer 0 在播的 mixer 引用。</summary>
    protected AnimancerState currentLayer0Mixer;
    /// <summary>当前激活的一次性 state（攻击 / Shoot / Reload / 受击 / Die）。未播完时不被 Locomotion 打断。</summary>
    protected AnimancerState activeOneShotState;
    /// <summary>true = activeOneShotState 在 Layer 0 全身覆盖（Melee/怪物攻击/Die）—— Locomotion 暂停切换避免打断。</summary>
    protected bool layer0FullBodyActive;
    /// <summary>true = characterAnimSet 配了 UpperBodyMask，启用 Layer 1 上下身分离。</summary>
    protected bool useUpperBodyLayer;

    /// <summary>本帧有效时间缩放 = 全局缩放 × 本 actor 局部缩放（view 在 <see cref="Tick"/> 传入）。
    /// 同时写到 <see cref="AnimancerGraph.Speed"/>；派生类做 Unity 时间相关平滑（SmoothDamp）时用
    /// <c>Time.unscaledDeltaTime × CurrentTimeScale</c> 代替 Time.deltaTime——因为全局缩放不再走 Time.timeScale。</summary>
    protected float CurrentTimeScale { get; private set; } = 1f;

    /// <summary>退出 layer0 fullbody 回 locomotion 时的一次性 override fade。退出 fullbody 时由
    /// <see cref="GetFullBodyRecoverFade"/> 写入，UpdateLocomotion 下次 Play 消费后清零。
    /// 解决"全身攻击 → 跑步"切换 DefaultFade 太短突兀。</summary>
    protected float overrideNextLocomotionFade;

    // ──────────────────────────── Layer 0 状态机（YStateMachine<Character>） ────────────────────────────
    // 把"死亡 > 全身动作(技能/闪避/受击…) > Locomotion"优先级级联拆成 3 个互斥状态。
    //   每帧 SelectBaseState 按 Character 意图决出**目标态**，再无条件 Switch（幂等，仅变更时真正切换）；状态 UpdateState 逐帧工作。Character 经 ctx 透传。
    //   技能/闪避/受击在动画层是同一种东西（播全身 clip + 锁 + 恢复）→ 收口进 kind-agnostic 的 FullBodyDriver（与 UpperBodyLayerDriver 对称），只认 Character.FullBody 通道。
    //   进出"全身覆盖"的 Layer 1 让位 / 恢复走 EnterFullBodyOverride / RestoreUpperBodyAfterFullBody 钩子；
    //   从全身覆盖回 Locomotion 的恢复用 baseFsm.Previous == fullBodyDriver 判定，恢复淡入取 FullBody.RecoverFade。
    private readonly YStateMachine<Character> baseFsm = new YStateMachine<Character>();
    private LocomotionState locomotionState;
    private FullBodyDriver fullBodyDriver;   // Layer 0 全身动作驱动（baseFsm 的 FullBody 状态），controller 经 IFullBodyHost 给它借 BaseLayer/钩子
    private DeathState deathState;

    // ────────────────────────── 分层契约（命名层访问器，替代 Layers[0]/[1] 魔法数字） ──────────────────────────
    //
    //   ┌─ Layer 0  (BaseLayer)  —— 无 mask = 全身 ──────────────────────────────┐
    //   │  下身 locomotion（非瞄准 1D / 瞄准 2D strafe） + 全身覆盖（死亡 / 技能）  │
    //   │  weight 恒 1                                                            │
    //   ├─ Layer 1  (UpperLayer) —— UpperBodyMask = 仅上身骨骼 ───────────────────┤
    //   │  持武器上身：持枪/瞄准 pose（常驻） + 换弹/后坐力/拿出/收回（one-shot）   │
    //   │  weight：持武器=1 / 无武器=0 / 死亡·技能=0                                │
    //   │  玩家：全部交给 UpperBodyLayerDriver；僵尸/无 mask：本层不存在            │
    //   └────────────────────────────────────────────────────────────────────────┘

    /// <summary>Layer 0：下身 locomotion + 全身覆盖。所有持骨架 actor 都有，weight 恒 1。</summary>
    protected AnimancerLayer BaseLayer => Animancer.Layers[0];

    /// <summary>Layer 1：上身（mask）。仅 <see cref="useUpperBodyLayer"/> 时有效——访问前用 <see cref="HasUpperLayer"/> 守护。</summary>
    protected AnimancerLayer UpperLayer => Animancer.Layers[1];

    /// <summary>上身层是否存在且可用（配了 UpperBodyMask 且 Animancer 已建出该层）。</summary>
    protected bool HasUpperLayer => useUpperBodyLayer && Animancer != null && Animancer.Layers.Count > 1;

    /// <summary>view 在 Bind 后立即调一次。characterAnimSetPath 一次性加载（spawn 时设定，运行时不换）。</summary>
    public void Init(AnimancerComponent animancer, ResMgr resMgr, string characterAnimSetPath)
    {
        Animancer = animancer;
        this.resMgr = resMgr;
        LoadCharacterAnimSet(characterAnimSetPath);

        // Layer 0 状态机：状态只持 controller 引用（嵌套类可访问其私有成员 + protected 钩子），运行时 Character 经 ctx 透传
        locomotionState = new LocomotionState(this);
        fullBodyDriver = new FullBodyDriver(this);
        deathState = new DeathState(this);
        baseFsm.Reset();
    }

    /// <summary>每帧驱动。<paramref name="effectiveTimeScale"/> = 全局缩放 × 本 actor 局部缩放
    /// （由 view 折算后传入）。Time.timeScale 恒为 1 不再帮忙减速 playable graph，所以这里把它写进 Graph.Speed。</summary>
    public void Tick(Character character, float effectiveTimeScale)
    {
        if (Animancer == null || character == null) return;

        // 派生类的"切 AnimSet"等前置（玩家在这里轮询 CurrentWeaponAnimSetPath 变化加载 weaponAnimSet）
        PreDrive(character);

        // 动画速度（全局慢动作 / 卡肉 / 局部慢动作 合并后的有效缩放）
        CurrentTimeScale = effectiveTimeScale;
        Animancer.Graph.Speed = effectiveTimeScale;

        // characterAnimSet 没加载就跳——locomotion / death 都依赖它
        if (characterAnimSet == null) return;
        DriveAnimation(character);
    }

    public virtual void Dispose()
    {
        OnDeathTriggered = null;
        characterAnimSet = null;
        locomotionMixer = null;
        currentLayer0Mixer = null;
        activeOneShotState = null;
        layer0FullBodyActive = false;
        useUpperBodyLayer = false;
        overrideNextLocomotionFade = 0f;
        baseFsm.Reset();
        locomotionState = null;
        fullBodyDriver = null;
        deathState = null;
        resMgr = null;
        Animancer = null;
    }

    // ────────────────────────── virtual 钩子（派生类实现战斗段） ──────────────────────────

    /// <summary>Tick 最前置：派生类在此做 AnimSet 切换等。基类无操作。</summary>
    protected virtual void PreDrive(Character character) { }

    /// <summary>战斗段：派生类播自己的 one-shot（攻击/开火/换弹/切枪/受击），设置
    /// <see cref="activeOneShotState"/> / <see cref="layer0FullBodyActive"/>。
    /// 返回 true = 本帧触发了需要立即 return 的全身 one-shot（跳过生命周期 + locomotion），
    /// 返回 false = 继续走 one-shot 生命周期 + locomotion。基类无战斗，返回 false。</summary>
    protected virtual bool DriveCombat(Character character, float fade) => false;

    /// <summary>退出全身覆盖回 locomotion 的 fade。基类用 DefaultFade；技能/闪避结束优先走各自 RecoverFade（在 LocomotionState.Enter 处理）。</summary>
    protected virtual float GetFullBodyRecoverFade(float defaultFade) => defaultFade;

    // ────────────────────────── 上身常驻 pose 钩子（玩家持武器时启用） ──────────────────────────
    // 默认实现 = 旧行为（Layer 1 仅承载 one-shot、播完淡出整层）。僵尸/单层走默认，行为不变。

    /// <summary>Layer 1 是否承载**常驻 base pose**（站立持枪/瞄准持枪）。
    /// true 时基类跳过旧 3b/3c one-shot 淡出生命周期，改调 <see cref="UpdateUpperBody"/>。
    /// 默认 false（僵尸/单层/无武器走旧逻辑）。玩家持武器且配了 pose 时返 true。</summary>
    protected virtual bool HasUpperBodyBasePose(Character character) => false;

    /// <summary>驱动 Layer 1 上身常驻状态机（base pose 按 IsAiming 切换 + 叠加 one-shot 回 base）。
    /// 仅当 <see cref="HasUpperBodyBasePose"/> 为 true 时基类每帧调用。默认空。</summary>
    protected virtual void UpdateUpperBody(Character character) { }

    /// <summary>从 Layer 0 全身覆盖（技能）恢复时，让 Layer 1 回到常驻 base pose 而非空层。
    /// 默认 = 旧行为 StartFade(1)。玩家 override 转发给 UpperBodyLayerDriver 重建 base pose。</summary>
    protected virtual void RestoreUpperBodyAfterFullBody(Character character, float recoverFade)
    {
        if (HasUpperLayer) UpperLayer.StartFade(1f, recoverFade);
    }

    /// <summary>进入 Layer 0 全身覆盖（死亡/技能）：让上身层让位——降 weight 静默 + 派生类清常驻缓存。
    /// immediate=死亡（立即 SetWeight 0）；否则技能（StartFade 0）。默认只降 weight（无常驻缓存可清）。
    /// 玩家 override 转发给 UpperBodyLayerDriver（同时清 base/one-shot 缓存，保证技能后干净重建）。</summary>
    protected virtual void EnterFullBodyOverride(float fade, bool immediate)
    {
        if (!HasUpperLayer) return;
        if (immediate) UpperLayer.SetWeight(0f);
        else UpperLayer.StartFade(0f, fade);
    }

    /// <summary>Layer 1 初始权重（<see cref="SetupUpperBodyLayer"/> 加载时设）。
    /// 基类默认 1f（旧行为：mask 配了即 weight 1）。玩家 override 返 0f（初始无武器静默，由装备跃迁升起）。</summary>
    protected virtual float GetInitialUpperBodyWeight() => 1f;

    /// <summary>Locomotion 驱动：基类用 LinearMixer 1D（按 AnimSpeedRatio 真实 m/s blend）。
    /// 玩家 override 叠加瞄准 2D mixer。无 locomotionMixer（无移动逻辑的怪物）则不播。</summary>
    protected virtual void UpdateLocomotion(Character character)
    {
        if (locomotionMixer == null) return;
        float fade = overrideNextLocomotionFade > 0f ? overrideNextLocomotionFade : GetDefaultFade();
        if (currentLayer0Mixer != locomotionMixer)
        {
            BaseLayer.Play(locomotionMixer, fade);
            currentLayer0Mixer = locomotionMixer;
            overrideNextLocomotionFade = 0f;
        }
        locomotionMixer.Parameter = character.AnimSpeedRatio;
    }

    // ────────────────────────── 共通实现 ──────────────────────────

    protected float GetDefaultFade() => characterAnimSet != null ? characterAnimSet.DefaultFade : 0.1f;

    /// <summary>加载 CharacterAnimSet（spawn 时一次）+ 构造 locomotion mixer + 配 Layer 1 mask。</summary>
    private void LoadCharacterAnimSet(string path)
    {
        characterAnimSet = null;
        locomotionMixer = null;
        useUpperBodyLayer = false;

        if (string.IsNullOrEmpty(path)) return;
        if (resMgr == null)
        {
            Debug.LogWarning("[LocomotionAnimController] ResMgr 未拿到 —— 无法 Load CharacterAnimSet");
            return;
        }
        var set = resMgr.Load<CharacterAnimSet>(path);
        if (set == null)
        {
            Debug.LogWarning($"[LocomotionAnimController] CharacterAnimSet 加载失败: {path}");
            return;
        }

        characterAnimSet = set;
        BuildLocomotionMixer();
        SetupUpperBodyLayer();
        OnCharacterAnimSetLoaded();
    }

    /// <summary>CharacterAnimSet 加载完（locomotion mixer / Layer mask 已就绪）后调。
    /// 派生类在此构造角色级附加 mixer（玩家：瞄准 8 方向 strafe，从 CharacterAnimSet 取 clip，build 一次不随切枪重建）。默认空。</summary>
    protected virtual void OnCharacterAnimSetLoaded() { }

    /// <summary>构造非瞄准 locomotion mixer：LinearMixerState 按 AnimSpeedRatio 真实 m/s blend。
    /// **可退化**：只取 Idle/Walk/Run/Sprint 中非 null 的 clip——
    ///   4 个 = 完整 blend；2 个 = 单段移动（动/不动两态）；1 个 = idle-only（炮台/植物，恒 idle）；0 个 = 不建 mixer（只跑 one-shot）。
    /// idle child（第一个、阈值最低）不参与 SynchronizeChildren——idle 没移动节奏。</summary>
    private void BuildLocomotionMixer()
    {
        locomotionMixer = null;
        if (characterAnimSet == null) return;

        // 顺序 Idle → Walk → Run → Sprint，阈值升序；只收非 null
        var clips = new List<AnimationClip>(4);
        var thresholds = new List<float>(4);
        if (characterAnimSet.Idle != null)   { clips.Add(characterAnimSet.Idle);   thresholds.Add(characterAnimSet.IdleThreshold); }
        if (characterAnimSet.Walk != null)   { clips.Add(characterAnimSet.Walk);   thresholds.Add(characterAnimSet.WalkThreshold); }
        if (characterAnimSet.Run != null)    { clips.Add(characterAnimSet.Run);    thresholds.Add(characterAnimSet.RunThreshold); }
        if (characterAnimSet.Sprint != null) { clips.Add(characterAnimSet.Sprint); thresholds.Add(characterAnimSet.SprintThreshold); }

        if (clips.Count == 0) return; // 无任何 locomotion clip：UpdateLocomotion 守 null，只跑 one-shot

        locomotionMixer = new LinearMixerState();
        locomotionMixer.AddRange(clips.ToArray());
        locomotionMixer.SetThresholds(thresholds.ToArray());
        var idleChild = locomotionMixer.GetChild(0);
        if (idleChild != null) locomotionMixer.DontSynchronize(idleChild);
    }

    /// <summary>UpperBodyMask 配了启用 Layer 1 上半身分离。mask 来自 CharacterAnimSet（角色级，不跟武器走）。</summary>
    private void SetupUpperBodyLayer()
    {
        useUpperBodyLayer = characterAnimSet != null && characterAnimSet.UpperBodyMask != null;
        if (useUpperBodyLayer)
        {
            UpperLayer.Mask = characterAnimSet.UpperBodyMask;
            UpperLayer.SetWeight(GetInitialUpperBodyWeight());
        }
        else if (Animancer.Layers.Count > 1)
        {
            Animancer.Layers[1].StartFade(0f, GetDefaultFade());
        }
    }

    /// <summary>核心状态机入口。Layer 0 由 <see cref="baseFsm"/>（YStateMachine）驱动：
    /// 每帧按优先级 Die &gt; 技能 &gt; 闪避 &gt; Locomotion 决出目标态（<see cref="SelectBaseState"/>），变了才切换，再 Update 当前态。
    /// 各状态内部再调战斗段（派生类）/ one-shot 生命周期 / Locomotion 钩子——与改造前同序同效。</summary>
    private void DriveAnimation(Character character)
    {
        baseFsm.Switch(SelectBaseState(character), character); // 幂等：仅目标态变更时真正切换
        baseFsm.Update(character, Time.unscaledDeltaTime * CurrentTimeScale);
    }

    /// <summary>纯选择（不切换）：按优先级决出 Layer 0 目标态。死亡 &gt; 全身动作 &gt; Locomotion。
    /// 全身动作（技能/闪避/受击…）共用 <see cref="Character.FullBody"/> 通道：<c>Kind != None</c> 维持锁定、<c>ClipDirty</c> 捕获"起手/进段当帧"
    /// ——两者任一为真即归属 <see cref="FullBodyDriver"/>（具体哪种动作的优先级仲裁在 <see cref="Character.RequestFullBody"/>，动画层不关心 kind）。</summary>
    private IYState<Character> SelectBaseState(Character character)
    {
        if (character.Die || character.IsDead) return deathState;
        if (character.FullBody.Kind != FullBodyKind.None || character.FullBody.ClipDirty) return fullBodyDriver;
        return locomotionState;
    }

    /// <summary>供嵌套状态触发死亡事件（event 只能在声明类内 Invoke，嵌套类经此薄封装转发）。</summary>
    private void RaiseDeath() => OnDeathTriggered?.Invoke();

    /// <summary>进入 Layer 0 全身覆盖（死亡 / 全身动作 Enter 共用）：清当前 mixer 引用 + 置全身标志。
    /// 标志的清除对称放在 <see cref="FullBodyDriver.ExitState"/>（经 <see cref="IFullBodyHost.OnFullBodyExit"/>）；死亡是终态、不退出，故标志恒真——正确。</summary>
    private void BeginLayer0FullBody()
    {
        currentLayer0Mixer = null;
        layer0FullBodyActive = true;
    }

    /// <summary>从全身覆盖（技能/闪避/受击…）切回 Locomotion 的一次性恢复：取恢复淡入、让 Layer 1 回常驻 base、清 one-shot。
    /// 由 <see cref="LocomotionState.EnterState"/> 在 Previous == fullBodyDriver 时调（死亡是终态不会回本态）。
    /// 恢复淡入取 <see cref="Character.FullBody"/>.RecoverFade（动作结束时各组件经 EndFullBody 写，kind-agnostic）；标志清除由 fullBodyDriver.Exit 负责。</summary>
    private void RecoverToLocomotion(Character ch)
    {
        float recoverFade = ch.FullBody.RecoverFade;
        if (recoverFade <= 0f) recoverFade = GetFullBodyRecoverFade(GetDefaultFade());
        ch.FullBody.RecoverFade = 0f;
        overrideNextLocomotionFade = recoverFade;
        if (HasUpperBodyBasePose(ch)) RestoreUpperBodyAfterFullBody(ch, recoverFade); // 玩家：driver 把 Layer1 拉回 weight1 + 当帧重建 base
        else if (HasUpperLayer) UpperLayer.StartFade(1f, recoverFade);
        activeOneShotState = null;
    }

    // ────────────────────────── IFullBodyHost：给 FullBodyDriver 借的最小表面（Layer 0 与 Death/Locomotion 共享 BaseLayer 的代价；显式实现不 widen 公有面） ──────────────────────────
    AnimancerLayer IFullBodyHost.BaseLayer => BaseLayer;
    CharacterAnimSet IFullBodyHost.AnimSet => characterAnimSet;
    float IFullBodyHost.DefaultFade => GetDefaultFade();
    AnimancerState IFullBodyHost.ActiveOneShot { get => activeOneShotState; set => activeOneShotState = value; }
    void IFullBodyHost.SilenceUpper(float fade, bool immediate) => EnterFullBodyOverride(fade, immediate);
    void IFullBodyHost.OnFullBodyEnter() => BeginLayer0FullBody();
    void IFullBodyHost.OnFullBodyExit() => layer0FullBodyActive = false;

    // ──────────────────────────── Layer 0 状态机：状态实现（嵌套私有类，可访问 controller 私有成员 + protected 钩子） ────────────────────────────

    /// <summary>死亡态（终态）。仅 Die trigger 那一帧播 DeathL/R 全身覆盖 + 通知 view；之后纯 IsDead 保持不重播。</summary>
    private sealed class DeathState : IYState<Character>
    {
        private readonly LocomotionAnimController c;
        public DeathState(LocomotionAnimController c) { this.c = c; }
        public string GetStateName() => "Death";
        public void EnterState(YStateMachine<Character> m, Character ch)
        {
            if (ch.Die) // 真死那一帧才播放 + 通知；纯 IsDead 进入（已死态）静默
            {
                ch.Die = false;
                var clip = ch.DeathVariant == 0 ? c.characterAnimSet.DeathL : c.characterAnimSet.DeathR;
                if (clip != null)
                {
                    c.BaseLayer.SetWeight(1f);
                    c.activeOneShotState = c.BaseLayer.Play(clip, 0f);
                }
                c.EnterFullBodyOverride(0f, immediate: true); // Layer 1 立即让位
                c.RaiseDeath();
            }
            c.BeginLayer0FullBody();
        }
        public void UpdateState(YStateMachine<Character> m, Character ch, float dt) { } // 终态：保持死亡 pose（不退出，layer0FullBodyActive 恒真）
        public void ExitState(YStateMachine<Character> m, Character ch) { }
    }

    /// <summary>Locomotion 态（默认态）。Enter：若由全身覆盖态切回，算恢复 fade + 让 Layer 1 回常驻 base pose。
    /// UpdateState：战斗段（派生类）→ one-shot 生命周期（退化路径）→ 上身常驻（upperBase）→ Locomotion always-on。</summary>
    private sealed class LocomotionState : IYState<Character>
    {
        private readonly LocomotionAnimController c;
        public LocomotionState(LocomotionAnimController c) { this.c = c; }
        public string GetStateName() => "Locomotion";

        public void EnterState(YStateMachine<Character> m, Character ch)
        {
            // 从全身覆盖（技能/闪避/受击…）切回：Previous == 唯一的 fullBodyDriver 即"刚退全身"（死亡是终态、不会切回本态）。
            // 标志清除对称放在 fullBodyDriver.Exit，本态只负责"恢复"动作，恢复淡入取 FullBody.RecoverFade。
            if (m.Previous == c.fullBodyDriver) c.RecoverToLocomotion(ch);
        }

        public void UpdateState(YStateMachine<Character> m, Character ch, float dt)
        {
            float fade = c.GetDefaultFade();

            // 战斗段（派生类武器 one-shot）。返回 true = 触发了全身 one-shot，本帧短路。
            // upperBase 模式下 DriveCombat 短路（trigger 交给 UpdateUpperBody 消费），仅退化/僵尸走旧逻辑。
            if (c.DriveCombat(ch, fade)) return;

            // upperBase = Layer 1 承载常驻 base pose（玩家持武器）：跳过退化 one-shot 淡出，改走 UpdateUpperBody。
            bool upperBase = c.HasUpperBodyBasePose(ch);
            if (!upperBase)
            {
                // 退化模型（无 base pose：僵尸/单层/无武器）的 one-shot 生命周期。
                // 单层模式 + one-shot 未播完：等待（受击 flinch / 单层开火走这条），不驱动 locomotion。
                if (!c.useUpperBodyLayer && c.activeOneShotState != null && c.activeOneShotState.IsPlaying && c.activeOneShotState.NormalizedTime < 1f)
                {
                    return;
                }
                // 双层模式 + one-shot 播完：淡出 Layer 1
                else if (c.activeOneShotState != null && (c.activeOneShotState.NormalizedTime >= 1f || !c.activeOneShotState.IsPlaying))
                {
                    if (c.HasUpperLayer) c.UpperLayer.StartFade(0f, fade);
                    c.activeOneShotState = null;
                }
            }

            // 新模型：Layer 1 上身常驻状态机（base pose + 叠加 one-shot 回 base）。
            if (upperBase) c.UpdateUpperBody(ch);

            // Locomotion always-on
            c.UpdateLocomotion(ch);
        }

        public void ExitState(YStateMachine<Character> m, Character ch) { }
    }
}
