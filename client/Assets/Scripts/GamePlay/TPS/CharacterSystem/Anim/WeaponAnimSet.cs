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

    [Header("近战变体（MeleeType 索引）")]
    public AnimationClip MeleeHard;    // MeleeType=0 枪托砸
    public AnimationClip MeleeKick;    // MeleeType=1 前踢

    [Header("Aim 阈值（按 AnimSpeedRatio 真实 m/s blend）")]
    [Tooltip("第 0 档 AimIdle 对应速度（0=完全静立瞄准）")]
    public float AimIdleThreshold = 0f;
    [Tooltip("第 1 档 AimWalk / strafe 单位向量对应速度——建议 = MoveComponent.AimSpeed (m/s)。Cartesian mixer 用作 1D fallback")]
    public float AimWalkThreshold = 1.5f;

    [Header("近战僵直（覆盖 MeleeComponent.SwingDuration）")]
    [Tooltip("近战挥击锁定移动时长（秒）。0=不覆盖，用 MeleeComponent.SwingDuration 默认（一般 1.2s）。" +
             "美工调完 MeleeHard/MeleeKick clip 后填实际 clip 长度（如 clip 0.6s 填 0.6）让锁定跟动画对齐。" +
             "**比 SwingDuration 小也生效**——想要短锁定快接连击就填小值（如 0.4s），动作游戏感更强。" +
             "WeaponComponent 切枪时通过 ResMgr 加载本 .asset 读这字段写到 Character.MeleeLockDuration。")]
    public float MeleeLockDuration = 0f;

    [Header("近战前冲（覆盖 MeleeComponent.ForwardSpeed / ForwardDuration）")]
    [Tooltip("近战挥击的前冲峰值速度 (m/s)。0=不覆盖，用 MeleeComponent.ForwardSpeed 默认。" +
             ">0=覆盖，0.0 时角色完全原地不前冲，3=轻动作，6~8=重击带强位移。" +
             "WeaponComponent.ApplySwap 镜像到 Character.MeleeForwardSpeed。" +
             "**约定**：填 <0 (如 -1) 表示\"显式覆盖为 0 不前冲\"，因为 0 被当作\"不覆盖\"信号。")]
    public float MeleeForwardSpeed = 0f;
    [Tooltip("近战前冲衰减时长 (s)。0=不覆盖，用 MeleeComponent.ForwardDuration 默认（2s）。" +
             "**重要**：建议设 ≤ MeleeLockDuration，否则连按近战时前冲会被反复重启不归零，造成\"持续被推到正前方\"bug。" +
             "推荐配法：MeleeLockDuration=0.5 + MeleeForwardDuration=0.3 → 推力前 0.3s 衰减到 0，剩 0.2s 完全静止再接下击。")]
    public float MeleeForwardDuration = 0f;
    [Tooltip("近战冷却时长 (s)。swing 结束后到下一次允许触发的间隔。0=不覆盖，用 MeleeComponent.Cooldown 默认（0=无冷却，落地立即可再挥）。" +
             ">0 = 强制连击节奏。例：LockDuration=0.5 + Cooldown=0.3 → 真实连击节奏 0.8s/击，连按 V 也只能按这个频率出。" +
             "适合：想要明显\"按一下出一招\"节奏感的轻武器；重武器一般 Cooldown=0 让 LockDuration 自己决定节奏即可。")]
    public float MeleeCooldown = 0f;

    [Header("Fade")]
    [Tooltip("Shoot 触发的淡入时长（秒）。0=立即切让连发节奏紧凑；0.05~0.1=轻微淡入平滑")]
    public float ShootFade = 0f;
}
