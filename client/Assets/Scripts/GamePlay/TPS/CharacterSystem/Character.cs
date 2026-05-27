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
/// </summary>
public class Character : Actor
{
    // ── 状态（view 物理后回写，组件读取） ──
    public Vector3 Position;
    public bool IsGrounded;

    // ── 生命值（HealthComponent 写，UI / 死亡逻辑读）──
    /// <summary>最大生命值。HealthComponent.Attach 时从配置写入；后续可被增益/装备改动。</summary>
    public float MaxHealth = 100f;
    /// <summary>当前生命值，HealthComponent.ApplyDamage / Heal 修改。</summary>
    public float CurHealth = 100f;
    /// <summary>死亡标志位。HealthComponent 在 CurHealth&lt;=0 时置 true；其他组件按需 Tick 头部早退。</summary>
    public bool IsDead;

    // ── 意图（组件写，view 读取/应用） ──
    public Quaternion Rotation = Quaternion.identity;
    public Vector3 WishVelocity;
    public float AnimMoveX;
    public float AnimMoveY;
    public bool IsShooting;
    /// <summary>右键按住=瞄准=抬枪。AimComponent 写，view 喂 Animator IsAiming，WeaponComponent 用它门控 IsShooting。</summary>
    public bool IsAiming;
    /// <summary>归一化水平速度（horizontal speed / WalkSpeed，clamp[0,1]）。Run 1D BlendTree 用。</summary>
    public float AnimSpeedRatio;
    /// <summary>动画播放倍率（Animator.speed）。MoveComponent 按当前状态（walk/sprint/aim）写入，view 应用。</summary>
    public float AnimPlaybackRate = 1f;
    /// <summary>一次性 trigger：组件置 true，view 消费后清回 false</summary>
    public bool MeleeAttack;
    public int MeleeType;
    /// <summary>近战进行中，MoveComponent 锁水平位移。WeaponComponent 在 melee 触发时置 true，计时器到期清零。</summary>
    public bool IsMeleeing;

    /// <summary>切枪进行中，WeaponComponent 用它门控开火。计时器到期自动清零。</summary>
    public bool IsSwapping;
    /// <summary>切枪一次性 trigger：WeaponComponent 切槽时置 true，view 消费 SetTrigger 后清回。</summary>
    public bool WeaponSwap;

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

    /// <summary>枪口高度（相对角色脚下 Position.y 的偏移，米）。
    /// AimComponent 用它做鼠标→世界射线相交平面（俯视角倾斜相机下，点哪打哪要靠这层平面）；
    /// WeaponComponent 用它算 FireOrigin 的 Y。两边共用同一值才能保证准星和射线对齐。
    /// 后续要做"蹲下/匍匐改变高度"就改这个字段。</summary>
    public float MuzzleHeight = 1.2f;
}
