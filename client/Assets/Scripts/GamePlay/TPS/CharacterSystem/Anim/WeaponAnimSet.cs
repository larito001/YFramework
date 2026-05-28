using UnityEngine;

/// <summary>
/// 武器动画集：一份 ScriptableObject 含某把武器全部 AnimationClip + locomotion 阈值。
/// <see cref="CharacterView"/> 用 Animancer 直接 Play 这些 clip 替代原 Animator state machine。
///
/// **使用流程**（美工）：
///   1. Project 窗口 → Create → TPS → WeaponAnimSet
///   2. 放 Assets/Resources/Weapon/Anim/ 下，命名 `&lt;武器名&gt;.asset`（如 Pistol.asset / RifleA.asset）
///   3. 拖入对应武器的各 clip，配 walk/run/sprint speed 阈值
///   4. 程序员把 Resources 相对路径（去 Assets/Resources/ 前缀去 .asset 后缀）配到 <c>Weapon.AnimSetPath</c>
///   详见 docs/Animancer 武器动画指南.md
///
/// **协议**：clip 为 null 时 view 跳过对应 state（不闪 / 不报错）。允许部分配置——只配 Idle + Shoot 也能跑。
/// </summary>
[CreateAssetMenu(fileName = "WeaponAnimSet", menuName = "TPS/WeaponAnimSet", order = 100)]
public class WeaponAnimSet : ScriptableObject
{
    [Header("Locomotion（无瞄准）")]
    public AnimationClip Idle;
    public AnimationClip Walk;
    public AnimationClip Run;
    public AnimationClip Sprint;

    [Header("Aim Locomotion（持枪瞄准时——1D fallback）")]
    public AnimationClip AimIdle;
    [Tooltip("1D 兜底：8 方向 strafe 缺失时用此 clip 走任意方向")]
    public AnimationClip AimWalk;

    [Header("Aim 8 方向 Strafe（CartesianMixerState 2D blend，按 AnimMoveX/Y 选 clip）")]
    [Tooltip("前 (0,1)。null 时 fallback AimWalk")]
    public AnimationClip AimWalkFwd;
    [Tooltip("后 (0,-1)")]
    public AnimationClip AimWalkBwd;
    [Tooltip("左 (-1,0)")]
    public AnimationClip AimStrafeLeft;
    [Tooltip("右 (1,0)")]
    public AnimationClip AimStrafeRight;
    [Tooltip("前左 (-0.707, 0.707)。null 时 fallback AimWalkFwd")]
    public AnimationClip AimStrafeFL;
    [Tooltip("前右 (0.707, 0.707)")]
    public AnimationClip AimStrafeFR;
    [Tooltip("后左 (-0.707, -0.707)。null 时 fallback AimWalkBwd")]
    public AnimationClip AimStrafeBL;
    [Tooltip("后右 (0.707, -0.707)")]
    public AnimationClip AimStrafeBR;

    [Header("Combat 触发")]
    public AnimationClip ShootLight;   // 小后坐力 HeavyRecoil=false 走这条
    public AnimationClip ShootHeavy;   // 大后坐力 HeavyRecoil=true 走这条
    public AnimationClip Reload;
    public AnimationClip Equip;        // 切到这把武器播
    public AnimationClip Holster;      // 切走这把武器播

    [Header("近战变体（MeleeType 索引）")]
    public AnimationClip MeleeHard;    // MeleeType=0 枪托砸
    public AnimationClip MeleeKick;    // MeleeType=1 前踢

    [Header("死亡变体（DeathVariant 索引）")]
    public AnimationClip DeathL;       // DeathVariant=0
    public AnimationClip DeathR;       // DeathVariant=1

    [Header("Locomotion 阈值（按 character.AnimSpeedRatio 真实 m/s 在相邻 clip 间平滑 blend）")]
    [Tooltip("第 0 档 Idle 对应速度（0=完全静止）")]
    public float IdleThreshold = 0f;
    [Tooltip("第 1 档 Walk 对应速度——建议 = MoveComponent.WalkSpeed (m/s)")]
    public float WalkThreshold = 5f;
    [Tooltip("第 2 档 Run 对应速度（介于 Walk/Sprint 之间）")]
    public float RunThreshold = 6f;
    [Tooltip("第 3 档 Sprint 对应速度——建议 = MoveComponent.SprintSpeed (m/s)")]
    public float SprintThreshold = 7f;

    [Header("Aim 阈值（独立——瞄准时速度范围 0~AimSpeed 远小于普通 locomotion）")]
    [Tooltip("第 0 档 AimIdle 对应速度（0）")]
    public float AimIdleThreshold = 0f;
    [Tooltip("第 1 档 AimWalk 对应速度——建议 = MoveComponent.AimSpeed (m/s)")]
    public float AimWalkThreshold = 1.5f;

    [Header("Fade 时长（秒）")]
    [Tooltip("Locomotion 之间切换 / 触发 state 进入的淡入时长")]
    public float DefaultFade = 0.1f;
    [Tooltip("Shoot 触发的淡入时长（一般 0 = 立刻播让节奏紧凑）")]
    public float ShootFade = 0f;

    [Header("分层（上下身分离）")]
    [Tooltip("上半身骨骼 mask。配了启用 Animancer Layer 1（Combat 走上半身 / Locomotion 走全身）；留空则 Combat 覆盖全身（单层模式）")]
    public AvatarMask UpperBodyMask;
}
