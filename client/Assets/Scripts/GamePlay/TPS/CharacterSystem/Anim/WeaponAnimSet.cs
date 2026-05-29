using UnityEngine;

/// <summary>
/// 武器级动画集：跟**武器**绑定（每把枪不同）。只含 Aim locomotion 8 方向 + Combat（Shoot/Reload/Equip/Holster/Melee）。
/// 跟 <see cref="CharacterAnimSet"/> 分工：locomotion / death / UpperBodyMask 在 CharacterAnimSet 上（角色级共通）。
///
/// 切武器只重新加载这一份，CharacterAnimSet（下半身 locomotion mixer）保持连续。
///
/// **使用流程**（美工）：
///   1. Project 窗口 → Create → TPS → WeaponAnimSet
///   2. 放 Assets/Resources/Weapon/Anim/ 下，命名 &lt;武器名&gt;.asset（如 Pistol.asset / Rifle.asset）
///   3. 拖 Aim 8 方向 strafe + Combat clip，调 aim 阈值
///   4. 程序员把 Resources 相对路径配到 Weapon.AnimSetPath
///   详见 docs/Animancer 武器动画指南.md
///
/// **协议**：clip 为 null 时 view 跳过对应 state（不闪 / 不报错）。允许部分配置——只配 ShootLight + Reload 也能跑。
/// </summary>
[CreateAssetMenu(fileName = "WeaponAnimSet", menuName = "TPS/WeaponAnimSet", order = 100)]
public class WeaponAnimSet : ScriptableObject
{
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

    // 近战已上移为通用"技能"（SkillDef + SkillCastComponent），不再随武器配置。

    [Header("Aim 阈值（按 AnimSpeedRatio 真实 m/s blend）")]
    [Tooltip("第 0 档 AimIdle 对应速度（0=完全静立瞄准）")]
    public float AimIdleThreshold = 0f;
    [Tooltip("第 1 档 AimWalk / strafe 单位向量对应速度——建议 = MoveComponent.AimSpeed (m/s)。Cartesian mixer 用作 1D fallback")]
    public float AimWalkThreshold = 1.5f;

    [Header("Fade")]
    [Tooltip("Shoot 触发的淡入时长（秒）。0=立即切让连发节奏紧凑；0.05~0.1=轻微淡入平滑")]
    public float ShootFade = 0f;
}
