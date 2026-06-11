using UnityEngine;

/// <summary>
/// 全身互斥动作种类——占用 Layer 0 全身覆盖、释放途中锁住 Move/Aim/Weapon 的动作（技能 / 闪避 / 未来受击…）。
/// 取代旧的"每个动作一套 IsX/XClip/XClipDirty/XClipFade/XRecoverFade 字段 + 一个 FSM 状态"的并行结构。
/// 死亡是终态、语义特殊（变体 / 事件 / 不恢复），单独走 <see cref="Character.Die"/>/<see cref="Actor.IsDead"/>，不进本枚举。
///
/// **声明序 = 优先级（高位可覆盖低位）**：<see cref="Character.RequestFullBody"/> 据此仲裁抢占
/// （Dodge &gt; Skill：闪避能打断技能，技能不能打断闪避）。
///
/// 加新全身动作只需：① 本枚举加一个值（定优先级）② 写它专属的玩法组件（走 RequestFullBody / SetFullBodyClip / EndFullBody）
/// ③ clip 资产。**Character 不加字段、动画 FSM 不加状态**（动画层只认 <see cref="FullBodyRequest"/>，kind-agnostic）。
/// </summary>
public enum FullBodyKind
{
    None,
    Skill,
    Dodge,
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
