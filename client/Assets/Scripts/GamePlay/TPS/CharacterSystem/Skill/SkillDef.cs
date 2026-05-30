using System;
using UnityEngine;

/// <summary>
/// **技能定义**（技能编辑器数据）。一个技能 = 一组按顺序播放的 <see cref="SkillSegment"/>，每段 = 一个全身 clip +
/// 可配置前向位移 + 可配置命中窗（伤害）。玩家近战、僵尸攻击/飞扑等所有"全身不可打断战斗动作"都用它表达。
///
/// 由 <see cref="SkillCastComponent"/>（逻辑侧）驱动：组件 owns 时间线 + 命中窗 OverlapSphere 扣血 + 位移（写 WishVelocity）+
/// 锁定（IsCastingSkill），并把当前段 clip 交给动画 controller（<see cref="LocomotionAnimController"/> 的技能全身分支）播放。
///
/// **使用流程**（美工/策划，纯 Inspector 编辑）：
///   1. Create → TPS → SkillDef，放 Resources 下（如 Resources/Skill/PlayerMelee.asset）
///   2. 配 Segments：每段拖 Clip，填 ForwardDistance（位移）、HitWindows（何时开伤害 + hitbox + DamageSpec）
///      - 原地攻击：单段 [Atk]，ForwardDistance=0，HitWindow 命中帧开伤害
///      - 飞扑：[Jump_Start, Jump_Air(ForwardDistance 大), Jump_End]，落地段 HitWindow 开范围伤害
///   3. 把 Resources 相对路径配到持有者的 SkillCastComponent.SkillPaths
/// </summary>
[CreateAssetMenu(fileName = "SkillDef", menuName = "TPS/SkillDef", order = 102)]
public class SkillDef : ScriptableObject
{
    [Tooltip("仅编辑器可读标识，如 \"PlayerMelee\" / \"ZombieLeap\"")]
    public string Name;
    [Tooltip("整技能结束、回 locomotion 的淡入时长（秒）。0=用 CharacterAnimSet.DefaultFade")]
    public float RecoverFade;

    [Header("相机震屏（命中窗开启那帧触发，kickback 风格，与是否打中解耦）")]
    [Tooltip("震屏强度（米级位移）。0=不震。常见 0.1~0.3。")]
    public float ShakeIntensity;
    [Tooltip("震屏从满到 0 的衰减时长（秒）。0.1~0.2 典型。")]
    public float ShakeDuration = 0.15f;
    [Tooltip("按顺序播放的段。每段播完（clip 自然结束或 HoldDuration 到点）进下一段，最后一段完回 locomotion。")]
    public SkillSegment[] Segments;

    /// <summary>技能的一段：一个全身 clip + 持续策略 + 前向位移 + 命中窗。</summary>
    [Serializable]
    public class SkillSegment
    {
        public AnimationClip Clip;
        [Tooltip("进入本段的淡入时长（秒）。0=用 DefaultFade；段间衔接一般填 0~0.1 紧凑")]
        public float Fade;
        [Tooltip("本段持续策略：0=播到 clip 自然结束（按 clip.length）；>0=循环该 clip 这么多秒再进下一段（循环段的 clip 需在 import 设为 Loop）")]
        public float HoldDuration;
        [Tooltip("本段沿角色 forward 的总位移（米）。0=原地（攻击）；正=前冲（飞扑 Air 段填大值如 4）；负=后退")]
        public float ForwardDistance;
        [Tooltip("位移随段内进度的分布曲线（x:段进度 0→1，y:已位移占比 0→1）。留空/少于2帧=线性匀速。可做'前段爆发后段刹车'")]
        public AnimationCurve DistanceProfile;
        [Tooltip("本段的命中窗（可多个，做多段连击伤害）。窗内每帧做球形 OverlapSphere，同一目标在一个窗内只扣一次。")]
        public HitWindow[] HitWindows;
        [Tooltip("本段的动效（可多个，与 HitWindows 平级）。按段内归一化时间 [0,1] 触发：到 StartNorm 生成，跟随型到 EndNorm 销毁。")]
        public SkillVfx[] Vfx;
    }

    /// <summary>技能动效：段内某归一化时间生成一个特效 prefab（位置/朝向相对角色，可选跟随角色移动）。
    /// 与 <see cref="HitWindow"/> 平级，由 <see cref="SkillCastComponent"/> 驱动、底层走 <see cref="VfxManager"/> 池化播放。</summary>
    [Serializable]
    public class SkillVfx
    {
        [Tooltip("特效 prefab 的 Resources 相对路径，如 \"VFX/SwordSlash\"。空=跳过。")]
        public string Path;
        [Tooltip("段内归一化时间 [0,1]：何时生成")]
        public float StartNorm;
        [Tooltip("段内归一化时间 [0,1]：何时结束。AttachToOwner=true（跟随型）到此销毁；false（世界一次性）忽略此值，prefab 自己播完回收")]
        public float EndNorm = 1f;
        [Tooltip("相对角色释放朝向的本地偏移：x=右, y=上, z=前（米）")]
        public Vector3 LocalOffset;
        [Tooltip("相对角色释放朝向的旋转（欧拉角）")]
        public Vector3 RotationEuler;
        [Tooltip("缩放倍率。0=用 prefab 原始缩放")]
        public float Scale = 1f;
        [Tooltip("是否跟随角色移动：true=挂到角色身上跟着走、到 EndNorm 销毁（光环/充能）；false=生成在世界点不跟随（命中爆点/挥砍残影）")]
        public bool AttachToOwner;
    }

    /// <summary>命中窗：段内某个归一化时间区间开启伤害检测 + 球形 hitbox + 伤害配方。</summary>
    [Serializable]
    public class HitWindow
    {
        [Tooltip("命中窗开始（段内归一化时间 0→1）")]
        public float StartNorm;
        [Tooltip("命中窗结束（段内归一化时间 0→1）")]
        public float EndNorm = 1f;
        [Tooltip("球形 hitbox 半径（米）")]
        public float Radius = 1f;
        [Tooltip("hitbox 中心沿角色 forward 的偏移（米）")]
        public float ForwardOffset = 0.8f;
        [Tooltip("hitbox 中心相对脚下 Position.y 的高度（米）")]
        public float Height = 1f;
        [Tooltip("命中伤害配方（基础/卡肉/暴击/元素/buff）")]
        public DamageSpec Damage;
    }
}
