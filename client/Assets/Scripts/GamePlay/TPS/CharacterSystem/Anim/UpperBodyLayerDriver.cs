using Animancer;
using UnityEngine;

/// <summary>
/// **上身层（Animancer Layer 1）驱动器**——把"持武器时上半身怎么动"的全部逻辑收口到这一处。
/// 由 <see cref="CharacterAnimancerController"/> 持有；controller 只调意图级方法，**不直接碰 Layer 1**。
///
/// 它独占管三件事：
///   1. **常驻 base pose**：IdleGunPose（站立持枪）↔ AimPose（瞄准持枪），按 <see cref="Character.IsAiming"/> 切换并持续保持。
///   2. **战斗 one-shot**：Holster / Equip / Reload / Shoot 替换式叠在 base 上，播完淡回 base（按当时最新 IsAiming）。
///   3. **整层 weight 进出**：装备(0→1, <see cref="SyncActivation"/>) / 卸下(1→0) / 全身覆盖静默(→0, <see cref="Silence"/>) / 技能后恢复(→1, <see cref="Restore"/>)。
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
    private bool active;                     // 当前常驻中（weight=1，承载 base pose）

    /// <summary>正在常驻上身。controller 据此跳过退化 one-shot 生命周期、改走本驱动。</summary>
    public bool Active => active;

    /// <summary>当前武器配了任一持枪 pose——决定本驱动接管常驻(true)，还是 controller 走退化(false)。</summary>
    public bool HasPose => weapon != null && (weapon.IdleGunPose != null || weapon.AimPose != null);

    public UpperBodyLayerDriver(AnimancerLayer layer, AvatarMask mask, float defaultFade)
    {
        this.layer = layer;
        this.defaultFade = defaultFade;
        layer.Mask = mask;
        layer.SetWeight(0f); // 初始静默：装备武器后由 SyncActivation 升起
    }

    /// <summary>切武器：只换上身集引用，不动 weight（weight 由 <see cref="SyncActivation"/> 处理）。</summary>
    public void SetWeapon(WeaponAnimSet w) => weapon = w;

    /// <summary>装备/卸下武器后同步 weight：配 pose→升 1 常驻；否则→0 退化。全身覆盖中由 controller 决定是否调（不该在死亡/技能时升起）。</summary>
    public void SyncActivation()
    {
        if (HasPose && !active)
        {
            layer.StartFade(1f, weapon.UpperBodyEnterFade);
            active = true; fromZero = true; baseState = null; oneShotState = null;
        }
        else if (!HasPose && active)
        {
            layer.StartFade(0f, weapon != null ? weapon.UpperBodyExitFade : defaultFade);
            active = false; baseState = null; oneShotState = null; baseIsAiming = false;
        }
    }

    /// <summary>进入全身覆盖（死亡/技能）：降 weight 静默 + 清缓存。immediate=死亡(立即 SetWeight 0)，否则技能(StartFade 0)。
    /// 即使当前未常驻（退化 one-shot 在播）也降 weight，保证全身动作期间上身彻底让位。</summary>
    public void Silence(float fade, bool immediate)
    {
        if (immediate) layer.SetWeight(0f);
        else layer.StartFade(0f, fade);
        active = false; baseState = null; oneShotState = null; fromZero = false;
    }

    /// <summary>技能结束从全身覆盖恢复：weight→1 + 标记当帧重建 base（下次 <see cref="Tick"/> 按当前 IsAiming 选 pose）。</summary>
    public void Restore(float recoverFade)
    {
        if (!HasPose) return;
        layer.StartFade(1f, recoverFade);
        active = true; fromZero = false; baseState = null; oneShotState = null;
    }

    /// <summary>每帧驱动：消费 combat trigger 叠 one-shot；无 one-shot 时维持/切换 base pose。未常驻则不动。</summary>
    public void Tick(Character character)
    {
        if (!active) return;
        DriveOneShot(character);
        if (oneShotState == null) DriveBase(character); // one-shot 播放期间不切 base
    }

    /// <summary>消费 combat trigger -> 替换式在本层叠 one-shot；one-shot 播完置空（下句 DriveBase 重建 base）。</summary>
    private void DriveOneShot(Character character)
    {
        if (TryConsumeCombatTrigger(character, weapon, defaultFade, out var clip, out var fade, out var speed))
        {
            if (clip != null)
            {
                var s = layer.Play(clip, fade);
                if (s != null)
                {
                    s.Time = 0f;                    // 每次从头播（连发后坐力 / 重新换弹）
                    if (speed > 0f) s.Speed = speed; // 后坐力倍率
                }
                oneShotState = s;
            }
            // clip==null：trigger 已消费，不播，保持当前 base
        }

        // 播完 -> 回 base（按当时最新 IsAiming，天然处理 one-shot 期间瞄准切换）
        if (oneShotState != null && (!oneShotState.IsPlaying || oneShotState.NormalizedTime >= 1f))
        {
            oneShotState = null;
            baseState = null; // fromZero 保持 false -> 回 base 用 AimPoseFade crossfade
        }
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

    public void Dispose()
    {
        weapon = null;
        baseState = null;
        oneShotState = null;
        baseIsAiming = false;
        fromZero = false;
        active = false;
    }

    /// <summary>按优先级 Holster &gt; Equip &gt; Reload &gt; Shoot 消费一个 combat trigger（常驻模式与 controller 退化路径共用，保证单一消费方、优先级一致）。
    /// 返回 true=本帧有 trigger（clip 可能为 null，表示该动作未配 clip——trigger 已消费、不播）。</summary>
    public static bool TryConsumeCombatTrigger(Character character, WeaponAnimSet weapon, float defaultFade,
        out AnimationClip clip, out float fade, out float speed)
    {
        clip = null; fade = defaultFade; speed = 0f;
        if (character.WeaponHolster) { character.WeaponHolster = false; clip = weapon.Holster; return true; }
        if (character.WeaponSwap)    { character.WeaponSwap = false;    clip = weapon.Equip;   return true; }
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
