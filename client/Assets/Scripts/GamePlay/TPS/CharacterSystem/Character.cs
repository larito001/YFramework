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
public class 
    
    Character : Actor
{
    // ── 角色动画（MoveComponent 写，view 读取 BlendTree）──
    public float AnimMoveX;
    public float AnimMoveY;
    /// <summary>**水平速度真实值 (m/s)**（之前是 ratio，现已改为 m/s）。view 的 Animancer LinearMixerState 用真实 m/s 阈值对齐 4 档 clip blend。</summary>
    public float AnimSpeedRatio;
    /// <summary>动画播放倍率（Animator.speed）。MoveComponent 按当前状态（walk/sprint/aim）写入，view 应用。</summary>
    public float AnimPlaybackRate = 1f;

    // ── 战斗状态（WeaponComponent 写，view + 其他组件读门控） ──
    public bool IsShooting;
    /// <summary>右键按住=瞄准=抬枪。AimComponent 写，view 喂 Animator IsAiming，WeaponComponent 用它门控 IsShooting。</summary>
    public bool IsAiming;

    // ── 技能（SkillCastComponent 写，view 读：全身不可打断技能链，见 SkillDef / SkillCastComponent）──
    /// <summary>技能播放中。SkillCastComponent 起技能置 true、结束清 false。
    /// **gating 总开关**：Move/Aim/Weapon 在它为 true 时锁移动/转身/开火（释放途中不可打断）；controller 用它锁全身覆盖。</summary>
    public bool IsCastingSkill;
    /// <summary>当前技能段要播的 clip。SkillCastComponent 进段时写，controller 消费 <see cref="SkillClipDirty"/> 时播放。</summary>
    public AnimationClip SkillClip;
    /// <summary>一次性：有新段 clip 待播。SkillCastComponent 进段置 true，controller 全身分支 Play(SkillClip) 后清回。</summary>
    public bool SkillClipDirty;
    /// <summary>当前段进入淡入时长（秒）。0 = 用 CharacterAnimSet.DefaultFade。</summary>
    public float SkillClipFade;
    /// <summary>技能结束回 locomotion 的淡入时长（秒）。SkillCastComponent EndCast 时写（= SkillDef.RecoverFade）。0 = 默认。</summary>
    public float SkillRecoverFade;

    // ── 闪避（DodgeComponent 写，view 读 + 其他组件门控）。与技能并列的另一类"全身、自带位移、可带无敌帧"动作，
    //    但方向在运行时按移动意图选（4 向 clip 来自 CharacterAnimSet），由专用 DodgeComponent 驱动，不走技能/连招的资产编排。──
    /// <summary>闪避进行中。DodgeComponent 起闪避置 true、结束清 false。
    /// 经 <see cref="IsBusy"/> 与 <see cref="IsCastingSkill"/> 一起门控 Move/Aim/Weapon（闪避途中锁移动/转身/开火）；controller 用它锁全身覆盖。</summary>
    public bool IsDodging;
    /// <summary>当前闪避要播的 clip（DodgeComponent 按方向从 CharacterAnimSet 选 DodgeFwd/Bwd/Left/Right）。controller 消费 <see cref="DodgeClipDirty"/> 时播放。</summary>
    public AnimationClip DodgeClip;
    /// <summary>一次性：有新闪避 clip 待播。DodgeComponent 起闪避置 true，controller 全身分支 Play(DodgeClip) 后清回。</summary>
    public bool DodgeClipDirty;
    /// <summary>闪避进入淡入时长（秒）。0 = 用 CharacterAnimSet.DefaultFade。</summary>
    public float DodgeClipFade;
    /// <summary>闪避结束回 locomotion 的淡入时长（秒）。DodgeComponent 结束时写。0 = 默认。</summary>
    public float DodgeRecoverFade;

    /// <summary>角色是否正被"全身互斥动作"占用（技能释放 <see cref="IsCastingSkill"/> 或闪避 <see cref="IsDodging"/>）。
    /// Move/Aim/Weapon 统一据此门控锁移动/转身/开火——这两类动作的位移与朝向都由各自组件权威写，不该被常规移动/瞄准覆盖。</summary>
    public bool IsBusy => IsCastingSkill || IsDodging;

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

    /// <summary>取出/收回(Equip/Holster)动画播放速度倍率。WeaponComponent 切枪时从对应武器 SwapAnimSpeed 写入
    /// （收回阶段=旧武器、取出阶段=新武器，随阶段切换自然对上）。driver 把它设到 Equip/Holster one-shot 的 Speed，
    /// 切枪过场时长同步 = clip 长度 / 本倍率，保证动画完整播放、只改快慢。1=原速。</summary>
    public float SwapAnimSpeed = 1f;

    /// <summary>瞄准点世界坐标。AimComponent 写入，射击/UI 用它做命中检测、画准星等。</summary>
    public Vector3 AimTargetWorldPos;

    /// <summary>当前持有武器槽位。WeaponComponent 写入，HUD/业务读取。实际武器模型挂载走 Weapon Actor + WeaponView。</summary>
    public int CurrentWeaponSlot;

    /// <summary>当前武器的左键技能下标（指向 <see cref="SkillCastComponent"/>.SkillPaths）。WeaponComponent 切枪时从 currentWeapon.PrimarySkillIndex 镜像写入，InputComponent 读。
    /// -1 = 左键正常开火（常规枪）；&gt;=0 = 近战/技能武器：左键放该技能、不开火。</summary>
    public int WeaponPrimarySkill = -1;
    /// <summary>当前武器的 V 键技能下标。WeaponComponent 切枪时从 currentWeapon.SecondarySkillIndex 镜像写入，InputComponent 读。
    /// -1 = 回退技能 0（保持旧"V 近战"行为）。</summary>
    public int WeaponSecondarySkill = -1;

    /// <summary>当前武器的连招图（<see cref="ComboGraph"/>）资源路径。WeaponComponent.ApplySwap 从 currentWeapon.ComboGraphPath 镜像写入。
    /// 空 / null = 当前武器无连招（ComboComponent 回退单招 WeaponPrimarySkill/WeaponSecondarySkill）。ComboComponent 轮询本字段变化即重载图。</summary>
    public string CurrentComboGraphPath;

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
