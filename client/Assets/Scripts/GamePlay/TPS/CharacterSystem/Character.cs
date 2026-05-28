using UnityEngine;

/// <summary>
/// 所有动态角色。纯数据，不持有 view 引用。
///
/// 数据流：
///   组件 Tick 写 "意图" 字段（WishVelocity / Rotation / AnimMoveX..）
///   CharacterView.LateUpdate 读意图驱动 CC / Transform / Animator
///   view 物理跑完后回写 "状态" 字段（Position / IsGrounded）
/// 组件下一帧基于回写的状态继续算。view 完全被动，Character 不知道它存在。
///
/// 组件容器（Add/Get/Tick/Dispose）由 Actor 基类提供。
///
/// **字段归属**：通用字段（Position / Rotation / WishVelocity / IsGrounded / HP 系 / Die / DeathVariant）
/// 已经下沉到 <see cref="Actor"/>。本类只持 Character 专属字段（动画 / 武器持有 / 瞄准）。
/// 详见 ARCHITECTURE.md "字段归属" 小节。
/// </summary>
public class Character : Actor
{
    // ── 角色动画（MoveComponent 写，view 读取 BlendTree）──
    public float AnimMoveX;
    public float AnimMoveY;
    /// <summary>**水平速度真实值 (m/s)**（之前是 ratio，现已改为 m/s）。view 的 Animancer LinearMixerState 用真实 m/s 阈值对齐 4 档 clip blend。</summary>
    public float AnimSpeedRatio;
    /// <summary>动画播放倍率（Animator.speed）。MoveComponent 按当前状态（walk/sprint/aim）写入，view 应用。</summary>
    public float AnimPlaybackRate = 1f;

    // ── 战斗状态（WeaponComponent / MeleeComponent 写，view + 其他组件读门控） ──
    public bool IsShooting;
    /// <summary>右键按住=瞄准=抬枪。AimComponent 写，view 喂 Animator IsAiming，WeaponComponent 用它门控 IsShooting。</summary>
    public bool IsAiming;
    /// <summary>一次性 trigger：组件置 true，view 消费后清回 false</summary>
    public bool MeleeAttack;
    public int MeleeType;
    /// <summary>近战进行中，MoveComponent 锁水平位移。MeleeComponent 在 V 键触发时置 true，计时器到期清零。</summary>
    public bool IsMeleeing;

    /// <summary>切枪进行中，WeaponComponent 用它门控开火。计时器到期自动清零（覆盖 Holster + Equip 两阶段）。</summary>
    public bool IsSwapping;
    /// <summary>取出新枪的一次性 trigger（Equip 阶段开始）：WeaponComponent 在 Holster 阶段结束时置 true，view 消费 SetTrigger("WeaponSwap") 后清回。</summary>
    public bool WeaponSwap;
    /// <summary>收回旧枪的一次性 trigger（Holster 阶段开始）：WeaponComponent 按数字键瞬间置 true，view 消费 SetTrigger("WeaponHolster") 后清回。</summary>
    public bool WeaponHolster;

    /// <summary>换弹进行中。WeaponComponent 从 currentWeapon.IsReloading 镜像写入，用于动画 + 开火/近战门控。</summary>
    public bool IsReloading;
    /// <summary>换弹一次性 trigger：IsReloading 上升沿时 WeaponComponent 置 true，view 消费 SetTrigger("Reload") 后清回。</summary>
    public bool Reload;

    /// <summary>射击一次性 trigger：FireComponent 每次成功开火 → WeaponComponent 镜像写入 → view 消费 SetTrigger("Shoot") 后清回。
    /// 用于驱动 Recoil 层的 ShootLight/ShootHeavy 单次动画（每发重新播放，节奏跟随实际开火）。</summary>
    public bool Shoot;

    /// <summary>当前装备武器是否用大后坐力动画。WeaponComponent 在 Equip 时从 currentWeapon.HeavyRecoil 写入。</summary>
    public bool HeavyRecoil;
    /// <summary>后坐力动画播放速度倍率。WeaponComponent 在 Equip 时从 currentWeapon.RecoilAnimSpeed 写入，
    /// view 写到 Animator Float 参数 RecoilSpeed，Recoil 层的 Shoot 状态 speedParameter 引用它。</summary>
    public float RecoilAnimSpeed = 1f;

    /// <summary>瞄准点世界坐标。AimComponent 写入，射击/UI 用它做命中检测、画准星等。</summary>
    public Vector3 AimTargetWorldPos;

    /// <summary>当前持有武器槽位。WeaponComponent 写入，HUD/业务读取。实际武器模型挂载走 Weapon Actor + WeaponView。</summary>
    public int CurrentWeaponSlot;

    /// <summary>当前武器对应的 <see cref="WeaponAnimSet"/> 资源路径（Resources 相对路径）。WeaponComponent.ApplySwap 时
    /// 从 currentWeapon.AnimSetPath 镜像写入。空 / null = 持有者 view 走默认 idle pose。</summary>
    public string CurrentWeaponAnimSetPath;
    /// <summary>切武器动画的一次性 trigger。WeaponComponent.ApplySwap 置 true；CharacterView 消费后 ResMgr.Load + 切换 Animancer 状态后清回 false。</summary>
    public bool WeaponAnimDirty;

    /// <summary>角色级 <see cref="CharacterAnimSet"/> 资源路径（Resources 相对路径）。Factory 创建时设一次，view Bind 时加载。
    /// 跟 CurrentWeaponAnimSetPath 分工：character set 含 locomotion + death + UpperBodyMask（跟角色走），weapon set 含 combat + aim（跟武器走）。</summary>
    public string CurrentCharacterAnimSetPath;

    /// <summary>枪口高度（相对角色脚下 Position.y 的偏移，米）。
    /// AimComponent 用它做鼠标→世界射线相交平面（俯视角倾斜相机下，点哪打哪要靠这层平面）；
    /// WeaponComponent 用它算 FireOrigin 的 Y。两边共用同一值才能保证准星和射线对齐。
    /// 后续要做"蹲下/匍匐改变高度"就改这个字段。</summary>
    public float MuzzleHeight = 1.2f;
}
