using UnityEngine;

/// <summary>
/// 全身互斥动作种类——占用 Layer 0 全身覆盖、释放途中锁住 Move/Aim/Weapon 的动作（技能 / 闪避 / 未来受击…）。
/// 取代旧的"每个动作一套 IsX/XClip/XClipDirty/XClipFade/XRecoverFade 字段 + 一个 FSM 状态"的并行结构。
/// 死亡是终态、语义特殊（变体 / 事件 / 不恢复），单独走 <see cref="Character.Die"/>/<see cref="Actor.IsDead"/>，不进本枚举。
///
/// **声明越靠前 = 优先级越高**（与 <see cref="CombatOneShot"/> 同方向，避免两套枚举方向相反的坑）：
/// <see cref="Character.RequestFullBody"/> 据此仲裁抢占——靠前(优先级高)的 kind 能覆盖当前正在播的；靠后的请求被拒。
/// 当前 Dodge &gt; Skill（闪避能打断技能，技能不能打断闪避）。加受击插在 Dodge 与 Skill 之间即 Dodge &gt; HitReact &gt; Skill。
/// <c>None</c> 是空通道 sentinel（不是真优先级——请求时永不传 None，仲裁也 guard 掉）。
///
/// 加新全身动作只需：① 本枚举按优先级插一个值 ② 写它专属的玩法组件（走 RequestFullBody / SetFullBodyClip / EndFullBody，
/// Tick 里自检 `FullBody.Kind != 自己` 则中止）③ clip 资产。**Character 不加字段、动画 FSM 不加状态**
/// （动画层只认 <see cref="FullBodyRequest"/>，kind-agnostic；谁打断谁全由本枚举顺序说了算，无需任何显式 interrupt 调用）。
/// </summary>
public enum FullBodyKind
{
    None,   // 空通道 sentinel（不计优先级）
    Dodge,  // 最高：能打断技能 / 受击
    Skill,  // 最低：谁都能打断它
}

/// <summary>全身动作的统一动画请求（<see cref="Character.FullBody"/> 持有一份）。逻辑组件写、动画层读。</summary>
public struct FullBodyRequest
{
    /// <summary>当前动作种类。None = 无（未锁定）；!= None 即"全身锁定中"（取代旧 IsCastingSkill / IsDodging）。</summary>
    public FullBodyKind Kind;
    /// <summary>当前要播的全身 clip（技能逐段换 / 闪避单条）。</summary>
    public AnimationClip Clip;
    /// <summary>Clip 进入淡入（秒，0 = 用 CharacterAnimSet.DefaultFade）。</summary>
    public float ClipFade;
    /// <summary>结束回 locomotion 的淡入（秒，0 = 默认）。EndFullBody 写、LocomotionState 恢复时消费。</summary>
    public float RecoverFade;
    /// <summary>一次性：有新 clip 待播（起手 / 技能进段当帧）。动画层 Play 后清回。</summary>
    public bool ClipDirty;
}
