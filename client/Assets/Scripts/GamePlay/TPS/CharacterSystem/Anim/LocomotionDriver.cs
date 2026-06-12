using System.Collections.Generic;
using Animancer;
using UnityEngine;

/// <summary>
/// **下身 locomotion 驱动器**（Layer 0 / BaseLayer 的非全身覆盖时段）——把"走/跑/瞄准 strafe 的 mixer 构造 + 每帧驱动"
/// 收口到一处，与 <see cref="UpperBodyLayerDriver"/>（Layer 1）/ <see cref="FullBodyDriver"/>（全身动作）并列。
///
/// 同时持 1D（非瞄准，<see cref="LinearMixerState"/> 按 AnimSpeedRatio）+ 2D（瞄准，<see cref="CartesianMixerState"/> 按 AnimMoveX/Y）两个 mixer，
/// 按 <see cref="Character.IsAiming"/> 切——**玩家瞄准的 2D mixer 与基类 1D 合并在此，消掉了原 controller 的 UpdateLocomotion virtual override**。
/// 僵尸 / 无瞄准 clip 的角色：AimIdle 为 null → 2D mixer 不建 → 永远走 1D（退化，行为同改造前）。无任何 locomotion clip → 不播（只跑 one-shot）。
///
/// 全身覆盖（死亡/技能/闪避）期间 BaseLayer 被全身 clip 占用：conductor 进入全身时调 <see cref="OnLayer0Overridden"/> 让本驱动忘掉当前 mixer，
/// 退出回 locomotion 时调 <see cref="SetNextFade"/> 给一次性恢复淡入。
/// </summary>
public class LocomotionDriver
{
    private readonly ILocomotionHost host;
    private readonly float dampTime;                 // aim mixer ParameterX/Y 的 SmoothDamp 时间（CharacterView 配，经 conductor 传入）

    private LinearMixerState locomotionMixer;         // 1D 非瞄准（按 AnimSpeedRatio 真实 m/s blend）
    private CartesianMixerState aimLocomotionMixer;   // 2D 瞄准 8 方向（按 AnimMoveX/Y），无瞄准 clip 时 null
    private AnimancerState currentLayer0Mixer;        // 当前在 BaseLayer 上播的 mixer 引用（全身覆盖时被 OnLayer0Overridden 清空）
    private float overrideNextFade;                   // 一次性：下次切回 locomotion 用的恢复淡入（conductor 经 SetNextFade 写）

    // 2D aim SmoothDamp 状态
    private float smoothedAnimMoveX, smoothedAnimMoveY, smoothMoveXVel, smoothMoveYVel;

    public LocomotionDriver(ILocomotionHost host, float dampTime)
    {
        this.host = host;
        this.dampTime = dampTime;
    }

    /// <summary>CharacterAnimSet 加载后构造两个 mixer（1D 总建、可退化；2D 仅 AimIdle 非 null 时建）。</summary>
    public void Build(CharacterAnimSet set)
    {
        BuildLocomotionMixer(set);
        BuildAimMixer(set);
    }

    /// <summary>全身覆盖接管 BaseLayer：忘掉当前 mixer（下次回 locomotion 会重新 Play）。conductor 在 BeginLayer0FullBody 调。</summary>
    public void OnLayer0Overridden() => currentLayer0Mixer = null;

    /// <summary>设下次切回 locomotion 的一次性恢复淡入（conductor 在 RecoverToLocomotion 调）。</summary>
    public void SetNextFade(float fade) => overrideNextFade = fade;

    /// <summary>每帧驱动：瞄准且有 2D mixer → 2D strafe（SmoothDamp 平滑）；否则 1D。两 mixer 都无（无 locomotion clip 的怪物）则不播。</summary>
    public void Drive(Character ch)
    {
        AnimancerState target;
        if (ch.IsAiming && aimLocomotionMixer != null) target = aimLocomotionMixer;
        else target = locomotionMixer;
        if (target == null) return; // 无任何 locomotion mixer

        if (currentLayer0Mixer != target)
        {
            float fade = overrideNextFade > 0f ? overrideNextFade : host.DefaultFade;
            host.BaseLayer.Play(target, fade);
            currentLayer0Mixer = target;
            overrideNextFade = 0f;
        }

        if (target == aimLocomotionMixer)
        {
            // dt<=0 时**跳过** SmoothDamp：Unity 的 Mathf.SmoothDamp 在"已 settle（current==target）"那帧会走 overshoot 分支
            // 执行 (output-target)/deltaTime，deltaTime==0 → 0/0 = NaN，把 ref 速度 smoothMove*Vel 永久污染成 NaN，
            // 之后每帧把 NaN 喂给 mixer.ParameterX/Y → ArgumentOutOfRangeException。用 unscaledDeltaTime × 有效缩放
            // （全局缩放不走 Time.timeScale）：暂停 / 卡肉（CurrentTimeScale=0）/ 首帧 dt=0 时沿用上一帧平滑值即可。
            float dt = Time.unscaledDeltaTime * host.CurrentTimeScale;
            if (dt > 0f)
            {
                smoothedAnimMoveX = Mathf.SmoothDamp(smoothedAnimMoveX, ch.AnimMoveX, ref smoothMoveXVel, dampTime, Mathf.Infinity, dt);
                smoothedAnimMoveY = Mathf.SmoothDamp(smoothedAnimMoveY, ch.AnimMoveY, ref smoothMoveYVel, dampTime, Mathf.Infinity, dt);
            }
            aimLocomotionMixer.ParameterX = smoothedAnimMoveX;
            aimLocomotionMixer.ParameterY = smoothedAnimMoveY;
        }
        else
        {
            locomotionMixer.Parameter = ch.AnimSpeedRatio;
        }
    }

    public void Dispose()
    {
        locomotionMixer = null;
        aimLocomotionMixer = null;
        currentLayer0Mixer = null;
        overrideNextFade = 0f;
        smoothedAnimMoveX = smoothedAnimMoveY = smoothMoveXVel = smoothMoveYVel = 0f;
    }

    /// <summary>构造非瞄准 1D locomotion mixer：<see cref="LinearMixerState"/> 按 AnimSpeedRatio 真实 m/s blend。
    /// **可退化**：只取 Idle/Walk/Run/Sprint 中非 null 的 clip——4=完整 blend；2=动/不动两态；1=idle-only；0=不建（只跑 one-shot）。
    /// idle child（第一个、阈值最低）不参与 SynchronizeChildren——idle 没移动节奏。</summary>
    private void BuildLocomotionMixer(CharacterAnimSet set)
    {
        locomotionMixer = null;
        if (set == null) return;

        var clips = new List<AnimationClip>(4);
        var thresholds = new List<float>(4);
        if (set.Idle != null)   { clips.Add(set.Idle);   thresholds.Add(set.IdleThreshold); }
        if (set.Walk != null)   { clips.Add(set.Walk);   thresholds.Add(set.WalkThreshold); }
        if (set.Run != null)    { clips.Add(set.Run);    thresholds.Add(set.RunThreshold); }
        if (set.Sprint != null) { clips.Add(set.Sprint); thresholds.Add(set.SprintThreshold); }

        if (clips.Count == 0) return;

        locomotionMixer = new LinearMixerState();
        locomotionMixer.AddRange(clips.ToArray());
        locomotionMixer.SetThresholds(thresholds.ToArray());
        var idleChild = locomotionMixer.GetChild(0);
        if (idleChild != null) locomotionMixer.DontSynchronize(idleChild);
    }

    /// <summary>构造瞄准 2D aim mixer：<see cref="CartesianMixerState"/> 9 child（idle 中心 + 8 方向 strafe），按 (AnimMoveX, AnimMoveY) blend。
    /// clip 来自角色级 CharacterAnimSet。null 方向 clip 用 AimWalk/AimWalkFwd/Bwd 兜底。AimIdle 为 null（如僵尸）→ 不建，本驱动永远走 1D。</summary>
    private void BuildAimMixer(CharacterAnimSet set)
    {
        aimLocomotionMixer = null;
        if (set == null || set.AimIdle == null) return;

        var fallback = set.AimWalk != null ? set.AimWalk : set.AimIdle;
        var fwd = set.AimWalkFwd != null ? set.AimWalkFwd : fallback;
        var bwd = set.AimWalkBwd != null ? set.AimWalkBwd : fallback;
        var right = set.AimStrafeRight != null ? set.AimStrafeRight : fallback;
        var left = set.AimStrafeLeft != null ? set.AimStrafeLeft : fallback;
        var fr = set.AimStrafeFR != null ? set.AimStrafeFR : fwd;
        var fl = set.AimStrafeFL != null ? set.AimStrafeFL : fwd;
        var br = set.AimStrafeBR != null ? set.AimStrafeBR : bwd;
        var bl = set.AimStrafeBL != null ? set.AimStrafeBL : bwd;

        aimLocomotionMixer = new CartesianMixerState();
        aimLocomotionMixer.AddRange(set.AimIdle, fwd, fr, right, br, bwd, bl, left, fl);
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

/// <summary>controller 暴露给 <see cref="LocomotionDriver"/> 的表面（在 <see cref="IAnimHost"/> 上加 SmoothDamp 用的时间缩放）。</summary>
public interface ILocomotionHost : IAnimHost
{
    /// <summary>本帧有效时间缩放（aim SmoothDamp 的 dt 用：unscaledDeltaTime × 本值）。</summary>
    float CurrentTimeScale { get; }
}
