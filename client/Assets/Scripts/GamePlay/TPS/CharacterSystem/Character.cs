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

    /// <summary>瞄准点世界坐标。AimComponent 写入，射击/UI 用它做命中检测、画准星等。</summary>
    public Vector3 AimTargetWorldPos;

    /// <summary>当前持有武器槽位。WeaponComponent 写入，业务/UI 读取。</summary>
    public int CurrentWeaponSlot;

    /// <summary>当前武器模型 Resources 路径。null/空 = 卸下武器。view 检测变化时挂/卸右手 socket。</summary>
    public string CurrentWeaponModelPath;
    public Vector3 CurrentWeaponLocalPosition;
    public Vector3 CurrentWeaponLocalEuler;
}
