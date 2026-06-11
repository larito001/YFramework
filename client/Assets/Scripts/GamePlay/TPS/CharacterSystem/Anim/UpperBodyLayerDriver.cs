using Animancer;
using UnityEngine;

/// <summary>
/// **上身层（Animancer Layer 1）驱动器**——把"持武器时上半身怎么动"的全部逻辑收口到这一处。
/// 由 <see cref="CharacterAnimancerController"/> 持有；controller 只调意图级方法，**不直接碰 Layer 1**。
///
/// 内部用 <see cref="YStateMachine"/> 建 **Layer 1 状态机**，三个互斥状态：
///   - **Silent**：weight 0、不驱动（无武器 / 全身覆盖让位 / 死亡）。
///   - **Pose**：常驻 base pose（IdleGunPose ↔ AimPose，按 <see cref="Character.IsAiming"/> 切换并持续保持）。
///   - **OneShot**：Holster / Equip / Reload / Shoot 替换式叠在 base 上，播完淡回 Pose（按当时最新 IsAiming）。
/// 进出（装备/卸下/全身覆盖静默/技能后恢复）由公开方法触发状态切换，weight 进出也在那里处理。
///
/// mask 来自 CharacterAnimSet.UpperBodyMask（构造时配一次）。武器没配任一 pose（<see cref="HasPose"/>=false）时本驱动不接管常驻，
/// controller 走"退化路径"（在 Layer 1 播完即淡出的旧式 one-shot）——退化路径仍复用本类的 <see cref="TryConsumeCombatTrigger"/>。
///
/// 生命周期：玩家 controller 在 CharacterAnimSet 加载后 new 一个（仅当启用了上身 mask）→ 每帧 <see cref="Tick"/> → <see cref="Dispose"/>。
/// </summary>
public class UpperBodyLayerDriver
{
    private readonly AnimancerLayer layer;   // = Animancer.Layers[1]，本类独占
    private readonly float defaultFade;      // 角色 DefaultFade，卸下武器无 ExitFade 可用时兜底

    private WeaponAnimSet weapon;            // 当前武器上身集（切枪换）
    private AnimancerState baseState;        // 常驻 base pose（IdleGunPose / AimPose）
    private bool baseIsAiming;               // baseState 当前是 AimPose(true) 还是 IdleGunPose(false)
    private AnimancerState oneShotState;     // 叠在 base 上的 one-shot；null=无
    private bool fromZero;                   // 刚从 weight0 进入，base 首次 Play 用 EnterFade（否则 AimPoseFade）
    private Character curCharacter;          // 本帧 Tick 写入，供状态 UpdateState 读取

    // ── Layer 1 状态机 ──
    private readonly UpperFsm machine = new UpperFsm();
    private readonly IYState silentMode;
    private readonly IYState poseMode;
    private readonly IYState oneShotMode;

    /// <summary>正在常驻上身（Pose / OneShot 态）。controller 据此跳过退化 one-shot 生命周期、改走本驱动。</summary>
    public bool Active => machine.Current == poseMode || machine.Current == oneShotMode;

    /// <summary>当前武器配了任一持枪 pose——决定本驱动接管常驻(true)，还是 controller 走退化(false)。</summary>
    public bool HasPose => weapon != null && (weapon.IdleGunPose != null || weapon.AimPose != null);

    public UpperBodyLayerDriver(AnimancerLayer layer, AvatarMask mask, float defaultFade)
    {
        this.layer = layer;
        this.defaultFade = defaultFade;
        layer.Mask = mask;
        layer.SetWeight(0f); // 初始静默：装备武器后由 SyncActivation 升起

        silentMode = new SilentState(this);
        poseMode = new PoseState(this);
        oneShotMode = new OneShotState(this);
        machine.SwitchState(silentMode, null); // 初始 Silent
    }

    /// <summary>切武器：只换上身集引用，不动 weight（weight 由 <see cref="SyncActivation"/> 处理）。</summary>
    public void SetWeapon(WeaponAnimSet w) => weapon = w;

    /// <summary>装备/卸下武器后同步 weight：配 pose→升 1 进 Pose；否则→0 退 Silent。全身覆盖中由 controller 决定是否调（不该在死亡/技能时升起）。</summary>
    public void SyncActivation()
    {
        if (HasPose && !Active)
        {
            layer.StartFade(1f, weapon.UpperBodyEnterFade);
            fromZero = true; baseState = null; oneShotState = null;
            machine.SwitchState(poseMode, null);
        }
        else if (!HasPose && Active)
        {
            layer.StartFade(0f, weapon != null ? weapon.UpperBodyExitFade : defaultFade);
            baseState = null; oneShotState = null; baseIsAiming = false;
            machine.SwitchState(silentMode, null);
        }
    }

    /// <summary>进入全身覆盖（死亡/技能）：降 weight 静默 + 清缓存 + 进 Silent。immediate=死亡(立即 SetWeight 0)，否则技能(StartFade 0)。
    /// 即使当前未常驻（退化 one-shot 在播）也降 weight，保证全身动作期间上身彻底让位。</summary>
    public void Silence(float fade, bool immediate)
    {
        if (immediate) layer.SetWeight(0f);
        else layer.StartFade(0f, fade);
        baseState = null; oneShotState = null; fromZero = false;
        if (machine.Current != silentMode) machine.SwitchState(silentMode, null);
    }

    /// <summary>技能结束从全身覆盖恢复：weight→1 + 进 Pose（下次 <see cref="Tick"/> 按当前 IsAiming 重建 base）。</summary>
    public void Restore(float recoverFade)
    {
        if (!HasPose) return;
        layer.StartFade(1f, recoverFade);
        fromZero = false; baseState = null; oneShotState = null;
        if (machine.Current != poseMode) machine.SwitchState(poseMode, null);
    }

    /// <summary>每帧驱动：交给状态机。Silent 态不动；Pose/OneShot 态按状态逻辑维持 base / 叠 one-shot。</summary>
    public void Tick(Character character)
    {
        curCharacter = character;
        machine.Update(0f);
    }

    public void Dispose()
    {
        weapon = null;
        baseState = null;
        oneShotState = null;
        baseIsAiming = false;
        fromZero = false;
        curCharacter = null;
        machine.ReSet();
    }

    // ──────────────────────────── 状态机内部驱动 ────────────────────────────

    /// <summary>消费 combat trigger -> 替换式在本层叠 one-shot。返回 true=本帧确实起了一个 one-shot（clip 非空已播）。
    /// clip==null（动作未配 clip）：trigger 已消费但不播、不视为起 one-shot，保持当前 base。</summary>
    private bool TryStartOneShot(Character character)
    {
        if (!TryConsumeCombatTrigger(character, weapon, defaultFade, out var clip, out var fade, out var speed))
            return false;
        if (clip == null) return false; // trigger 已消费，无 clip 不播
        var s = layer.Play(clip, fade);
        if (s != null)
        {
            s.Time = 0f;                     // 每次从头播（连发后坐力 / 重新换弹）
            if (speed > 0f) s.Speed = speed; // 后坐力 / 取出收回倍率
        }
        oneShotState = s;
        return true;
    }

    /// <summary>维持常驻 base pose；按 IsAiming 在 IdleGunPose &lt;-&gt; AimPose 切换。两者互为兜底。</summary>
    private void DriveBase(Character character)
    {
        bool wantAiming = character.IsAiming;
        AnimationClip target = wantAiming ? weapon.AimPose : weapon.IdleGunPose;
        if (target == null) target = wantAiming ? weapon.IdleGunPose : weapon.AimPose;
        if (target == null) return;

        if (baseState == null || baseIsAiming != wantAiming || baseState.Clip != target)
        {
            float fade = fromZero ? weapon.UpperBodyEnterFade : weapon.AimPoseFade;
            baseState = layer.Play(target, fade);
            baseIsAiming = wantAiming;
            fromZero = false;
        }
    }

    /// <summary>暴露 <see cref="YStateMachine.currentState"/> 给本驱动做引用比较（基类是 protected）。</summary>
    private class UpperFsm : YStateMachine
    {
        public IYState Current => currentState;
    }

    /// <summary>静默态：weight 0、不驱动任何动画（等 SyncActivation/Restore 升起）。</summary>
    private sealed class SilentState : IYState
    {
        private readonly UpperBodyLayerDriver d;
        public SilentState(UpperBodyLayerDriver d) { this.d = d; }
        public string GetStateName() => "Silent";
        public void EnterState(YStateMachine m, object param) { }
        public void UpdateState(YStateMachine m, float dt) { }
        public void ExitState(YStateMachine m) { }
    }

    /// <summary>常驻 pose 态：有 combat trigger → 起 one-shot 并转 OneShot；否则维持/切换 base pose。</summary>
    private sealed class PoseState : IYState
    {
        private readonly UpperBodyLayerDriver d;
        public PoseState(UpperBodyLayerDriver d) { this.d = d; }
        public string GetStateName() => "Pose";
        public void EnterState(YStateMachine m, object param) { }
        public void UpdateState(YStateMachine m, float dt)
        {
            var ch = d.curCharacter;
            // 先消费 trigger（与原逻辑同序：起 one-shot 的那帧不再驱动 base）
            if (d.TryStartOneShot(ch)) { d.machine.SwitchState(d.oneShotMode, null); return; }
            d.DriveBase(ch);
        }
        public void ExitState(YStateMachine m) { }
    }

    /// <summary>one-shot 态：新 trigger 替换式重播（留本态）；当前 one-shot 播完 → 清空回 Pose（base 用 crossfade 重建）。</summary>
    private sealed class OneShotState : IYState
    {
        private readonly UpperBodyLayerDriver d;
        public OneShotState(UpperBodyLayerDriver d) { this.d = d; }
        public string GetStateName() => "OneShot";
        public void EnterState(YStateMachine m, object param) { }
        public void UpdateState(YStateMachine m, float dt)
        {
            var ch = d.curCharacter;
            if (d.TryStartOneShot(ch)) return; // 新 trigger 替换当前 one-shot，留在本态
            var s = d.oneShotState;
            if (s == null || !s.IsPlaying || s.NormalizedTime >= 1f)
            {
                d.oneShotState = null;
                d.baseState = null; // fromZero 保持 false → 回 base 用 AimPoseFade crossfade
                d.machine.SwitchState(d.poseMode, null);
                d.DriveBase(ch);    // 同帧重建 base（与原 DriveOneShot 完成即 DriveBase 同序，避免迟一帧）
            }
        }
        public void ExitState(YStateMachine m) { }
    }

    /// <summary>按优先级 Holster &gt; Equip &gt; Reload &gt; Shoot 消费一个 combat trigger（常驻模式与 controller 退化路径共用，保证单一消费方、优先级一致）。
    /// 返回 true=本帧有 trigger（clip 可能为 null，表示该动作未配 clip——trigger 已消费、不播）。</summary>
    public static bool TryConsumeCombatTrigger(Character character, WeaponAnimSet weapon, float defaultFade,
        out AnimationClip clip, out float fade, out float speed)
    {
        clip = null; fade = defaultFade; speed = 0f;
        // 取出/收回按 Character.SwapAnimSpeed 倍率播（过场时长在 WeaponComponent 已同步缩放，clip 完整不被切）
        if (character.WeaponHolster) { character.WeaponHolster = false; clip = weapon.Holster; speed = character.SwapAnimSpeed; return true; }
        if (character.WeaponSwap)    { character.WeaponSwap = false;    clip = weapon.Equip;   speed = character.SwapAnimSpeed; return true; }
        if (character.Reload)        { character.Reload = false;        clip = weapon.Reload;  return true; }
        if (character.Shoot)
        {
            character.Shoot = false;
            clip = character.HeavyRecoil ? weapon.ShootHeavy : weapon.ShootLight;
            fade = weapon.ShootFade;
            speed = character.RecoilAnimSpeed;
            return true;
        }
        return false;
    }
}
