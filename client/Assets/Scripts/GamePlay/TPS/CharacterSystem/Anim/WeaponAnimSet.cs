using UnityEngine;

/// <summary>
/// 武器级动画集：跟**武器**绑定（每把枪不同）。**只含上半身**——持枪/瞄准常驻 pose + Combat（Shoot/Reload/Equip/Holster）。
/// 跟 <see cref="CharacterAnimSet"/> 分工：
///   - **CharacterAnimSet（角色级）**：下半身 locomotion（Idle/Walk/Run/Sprint 1D + 瞄准 8 方向 strafe 2D）+ Death + UpperBodyMask
///   - **WeaponAnimSet（武器级）**：上半身 IdleGunPose/AimPose 常驻 pose + Shoot/Reload/Equip/Holster one-shot
///
/// 下半身（含瞄准 strafe）跟武器无关，全角色共用一份，放 CharacterAnimSet；切武器只重新加载这一份上身集，下半身 mixer 保持连续不重建。
///
/// **使用流程**（美工）：
///   1. Project 窗口 → Create → TPS → WeaponAnimSet
///   2. 放 Assets/Resources/Weapon/Anim/ 下，命名 &lt;武器名&gt;.asset（如 Pistol.asset / Rifle.asset）
///   3. 拖 IdleGunPose / AimPose（上身持枪 pose）+ Shoot/Reload/Equip/Holster
///   4. 程序员把 Resources 相对路径配到 Weapon.AnimSetPath
///   详见 docs/Animancer 武器动画指南.md
///
/// **协议**：clip 为 null 时跳过对应 state（不闪 / 不报错）。允许部分配置——只配 ShootLight + Reload 也能跑。
/// 未配任一持枪 pose（IdleGunPose/AimPose）时退化为旧式 Layer 1 one-shot（播完淡出整层）。
/// </summary>
[CreateAssetMenu(fileName = "WeaponAnimSet", menuName = "TPS/WeaponAnimSet", order = 100)]
public class WeaponAnimSet : ScriptableObject
{
    [Header("上身常驻 Pose（Layer 1 base，持武器时常驻；mask 来自 CharacterAnimSet.UpperBodyMask）")]
    [Tooltip("站立持枪（未瞄准）的常驻 pose（上身循环）。持武器且未瞄准时 Layer 1 常驻这条")]
    public AnimationClip IdleGunPose;
    [Tooltip("瞄准持枪的常驻 pose（上身循环）。IsAiming=true 时切到这里。IdleGunPose/AimPose 互为兜底")]
    public AnimationClip AimPose;

    [Header("Combat 触发（上身 one-shot，叠在常驻 pose 上，播完回 base）")]
    public AnimationClip ShootLight;   // 小后坐力 HeavyRecoil=false 走这条
    public AnimationClip ShootHeavy;   // 大后坐力 HeavyRecoil=true 走这条
    public AnimationClip Reload;
    public AnimationClip Equip;        // 切到这把武器播（拿出）
    public AnimationClip Holster;      // 切走这把武器播（收回）

    // 近战已上移为通用"技能"（SkillDef + SkillCastComponent），不再随武器配置。
    // 下半身 locomotion / 瞄准 8 方向 strafe 已上移到 CharacterAnimSet（角色级，跟武器无关）。

    [Header("Fade")]
    [Tooltip("Shoot 触发的淡入时长（秒）。0=立即切让连发节奏紧凑；0.05~0.1=轻微淡入平滑")]
    public float ShootFade = 0f;

    [Header("上身常驻 Pose Fade")]
    [Tooltip("IdleGunPose <-> AimPose 互切 / one-shot 播完回 base pose 的淡入时长（秒）。建议 0.12~0.2")]
    public float AimPoseFade = 0.15f;
    [Tooltip("装备瞬间（无武器->有）Layer 1 从 weight 0 升到常驻 base pose 的淡入（秒）。建议 0.1~0.2")]
    public float UpperBodyEnterFade = 0.15f;
    [Tooltip("卸下武器（有->无）Layer 1 淡出到 weight 0 的时长（秒）。建议 0.15")]
    public float UpperBodyExitFade = 0.15f;
}
