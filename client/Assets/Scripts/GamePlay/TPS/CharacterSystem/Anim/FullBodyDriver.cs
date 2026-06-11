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
        host.SilenceUpper(f, immediate: false); // Layer 1 让位（淡出）
    }

    // 对称收尾：Enter 置全身标志、Exit 清——切回 Locomotion / 被死亡抢占切走时都复位。
    // 恢复动作（Layer 1 回 base + 恢复淡入）另由 controller 在回 Locomotion 时处理，与本标志解耦。
    public void ExitState(YStateMachine<Character> m, Character ch) => host.OnFullBodyExit();

    /// <summary>把 <see cref="Character.FullBody"/> 意图解析成要播的 clip。
    /// **Phase 1**：直接取通道里的 Clip（行为不变）。**Phase 2** 改成按 Kind 解析
    /// （Dodge 方向→host.AnimSet / Skill 段→SkillDef），逻辑层从此不传 clip。</summary>
    private AnimationClip ResolveClip(Character ch) => ch.FullBody.Clip;
}

/// <summary>controller 暴露给 <see cref="FullBodyDriver"/> 的最小表面——Layer 0 与 Death/Locomotion 共享 BaseLayer 的代价。
/// controller **显式实现**本接口，不 widen 公有面。</summary>
public interface IFullBodyHost
{
    /// <summary>Layer 0（全身覆盖播在这）。</summary>
    AnimancerLayer BaseLayer { get; }
    /// <summary>CharacterAnimSet.DefaultFade。</summary>
    float DefaultFade { get; }
    /// <summary>当前 Layer 0 one-shot 槽（= controller.activeOneShotState，与 Death / Locomotion 共享）。</summary>
    AnimancerState ActiveOneShot { get; set; }
    /// <summary>让 Layer 1 让位（= controller.EnterFullBodyOverride，virtual，玩家转发给 UpperBodyLayerDriver）。</summary>
    void SilenceUpper(float fade, bool immediate);
    /// <summary>进入全身覆盖：置全身标志 + 清当前 mixer 引用。</summary>
    void OnFullBodyEnter();
    /// <summary>退出全身覆盖：清全身标志。</summary>
    void OnFullBodyExit();
}
