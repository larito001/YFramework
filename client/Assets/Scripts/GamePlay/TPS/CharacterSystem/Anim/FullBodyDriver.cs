using Animancer;
using UnityEngine;

/// <summary>
/// **全身动作驱动器**（Layer 0 / BaseLayer）——把"技能 / 闪避 / 受击等全身覆盖动作怎么播"收口到一处，
/// 与 <see cref="UpperBodyLayerDriver"/>（Layer 1）对称。本身是 baseFsm 里的 **FullBody 状态**（<see cref="IYState{TCtx}"/>）。
///
/// kind-agnostic：只认 <see cref="Character.FullBody"/> 通道——消费 ClipDirty → 解析出 clip → 在 BaseLayer 全身覆盖播放 → 让 Layer 1 让位。
/// 哪种动作、怎么玩全在逻辑组件（SkillCast / Dodge / …）；**clip 解析**（Phase 2 起：方向→clip / 技能段→clip）也在本驱动（view 层），逻辑层只发意图。
///
/// 与 UpperBodyLayerDriver 的差异：Layer 0 是 BaseLayer，跟 Death / Locomotion **共享**，所以本驱动不独占一层，
/// 而是通过 <see cref="IFullBodyHost"/> 借用 controller 的 BaseLayer / 静默钩子 / one-shot 槽（host 表面即这份"层共享"的代价）。
/// </summary>
public class FullBodyDriver : IYState<Character>
{
    private readonly IFullBodyHost host;
    public FullBodyDriver(IFullBodyHost host) { this.host = host; }

    public string GetStateName() => "FullBody";

    public void EnterState(YStateMachine<Character> m, Character ch) => host.OnFullBodyEnter();

    public void UpdateState(YStateMachine<Character> m, Character ch, float dt)
    {
        if (!ch.FullBody.ClipDirty) return; // 无新 clip：Animancer 继续播当前全身 clip，不驱动 combat / locomotion
        ch.FullBody.ClipDirty = false;
        var clip = ResolveClip(ch);
        float f = ch.FullBody.ClipFade > 0f ? ch.FullBody.ClipFade : host.DefaultFade;
        if (clip != null) // null = 退化段：仅保持锁定，不播
        {
            var s = host.BaseLayer.Play(clip, f);
            if (s != null) s.Time = 0f; // 每段从头播
            host.ActiveOneShot = s;
        }
        // 翻滚是"下半身动作"：dodge clip 本就不 key 上身（手臂/手 weight 0），保持持枪上身层 Layer 1（含 RightHandProp）
        // → 手不参与翻滚、武器始终握在手里。其余全身动作（技能/受击）仍让 Layer 1 让位（全身覆盖）。
        if (ch.FullBody.Kind == FullBodyKind.Dodge)
            host.RaiseUpper(ch, f);
        else
            host.SilenceUpper(f, immediate: false); // Layer 1 让位（淡出）
    }

    // 对称收尾：Enter 置全身标志、Exit 清——切回 Locomotion / 被死亡抢占切走时都复位。
    // 恢复动作（Layer 1 回 base + 恢复淡入）另由 controller 在回 Locomotion 时处理，与本标志解耦。
    public void ExitState(YStateMachine<Character> m, Character ch) => host.OnFullBodyExit();

    /// <summary>把 <see cref="Character.FullBody"/> **意图**解析成要播的 clip——逻辑层只发 kind + 方向 / 技能段，clip 提取全在此（view 层）。
    /// 加新全身动作：在此 switch 加一个 case（像上半身的 TryConsumeCombatTrigger）。clip 缺失返 null = 退化段（仅锁定不播）。</summary>
    private AnimationClip ResolveClip(Character ch)
    {
        switch (ch.FullBody.Kind)
        {
            case FullBodyKind.Dodge:
                return host.AnimSet != null ? host.AnimSet.GetDodgeClip((DodgeDir)ch.FullBody.Variant) : null;
            case FullBodyKind.Skill:
            {
                var segs = ch.FullBody.Skill != null ? ch.FullBody.Skill.Segments : null;
                int i = ch.FullBody.SkillSegment;
                return (segs != null && i >= 0 && i < segs.Length && segs[i] != null) ? segs[i].Clip : null;
            }
            // 将来 HitReact：return host.AnimSet != null ? host.AnimSet.HitReact : null;
            default:
                return null;
        }
    }
}

/// <summary>controller 暴露给各动画 driver 的**共享**表面（BaseLayer / AnimSet / DefaultFade）。
/// FullBodyDriver / LocomotionDriver 各自的专属表面在 <see cref="IFullBodyHost"/> / <see cref="ILocomotionHost"/> 上扩展。
/// controller **显式实现**，不 widen 公有面。</summary>
public interface IAnimHost
{
    /// <summary>Layer 0（全身覆盖 / locomotion 都播在这）。</summary>
    AnimancerLayer BaseLayer { get; }
    /// <summary>角色级动画集（解析闪避方向 clip / 构造 locomotion mixer 用）。</summary>
    CharacterAnimSet AnimSet { get; }
    /// <summary>CharacterAnimSet.DefaultFade。</summary>
    float DefaultFade { get; }
}

/// <summary>controller 暴露给 <see cref="FullBodyDriver"/> 的表面（在 <see cref="IAnimHost"/> 上加全身覆盖专属）——Layer 0 与 Death/Locomotion 共享 BaseLayer 的代价。</summary>
public interface IFullBodyHost : IAnimHost
{
    /// <summary>当前 Layer 0 one-shot 槽（= controller.activeOneShotState，与 Death / Locomotion 共享）。</summary>
    AnimancerState ActiveOneShot { get; set; }
    /// <summary>让 Layer 1 让位（= controller.EnterFullBodyOverride，virtual，玩家转发给 UpperBodyLayerDriver）。</summary>
    void SilenceUpper(float fade, bool immediate);
    /// <summary>把 Layer 1（持枪上身层）升起/保持到 weight 1 + 重建 base pose——给"只用下半身的全身动作"（翻滚）用：
    /// 手臂/手保持持枪姿态，不参与该动作。即便之前被技能静默过也能拉回（覆盖 skill→dodge 打断）。</summary>
    void RaiseUpper(Character ch, float fade);
    /// <summary>进入全身覆盖：置全身标志 + 清当前 mixer 引用。</summary>
    void OnFullBodyEnter();
    /// <summary>退出全身覆盖：清全身标志。</summary>
    void OnFullBodyExit();
}
