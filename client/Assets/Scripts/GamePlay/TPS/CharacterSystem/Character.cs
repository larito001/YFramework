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

    // ── 全身互斥动作统一通道（技能 / 闪避 / 未来受击共用，取代旧 Skill*/Dodge* 两套并行字段，见 FullBodyKind / FullBodyRequest）──
    //   写入：SkillCastComponent / DodgeComponent 等逻辑组件 → RequestFullBody / SetFullBodyClip / EndFullBody
    //   读取：动画层 LocomotionAnimController（kind-agnostic 播 FullBody.Clip）；gating 经下方 IsCastingSkill/IsDodging/IsBusy 计算属性
    public FullBodyRequest FullBody;

    /// <summary>正在释放技能（= 全身通道被技能占用）。gating + 动画兼容属性，所有旧读取点不变。</summary>
    public bool IsCastingSkill => FullBody.Kind == FullBodyKind.Skill;

    /// <summary>正在闪避（= 全身通道被闪避占用）。gating + 动画兼容属性，所有旧读取点不变。</summary>
    public bool IsDodging => FullBody.Kind == FullBodyKind.Dodge;

    /// <summary>角色是否正被"全身互斥动作"占用（技能 / 闪避 / 未来受击）。Move/Aim/Weapon 统一据此门控锁移动/转身/开火
    /// ——这类动作的位移与朝向都由各自组件权威写，不该被常规移动/瞄准覆盖。</summary>
    public bool IsBusy => FullBody.Kind != FullBodyKind.None;

    /// <summary>请求占用全身通道（起手）。**集中优先级仲裁**：已有更高优先级动作占用时返回 false（如技能想盖闪避）；
    /// 同动作再请求（连招接段）幂等。clip 由随后的 <see cref="SetFullBodyClip"/> 设。
    /// 优先级方向见 <see cref="FullBodyKind"/>：**声明越靠前 = 优先级越高**，故"靠后（int 更大）= 优先级更低"，被拒。</summary>
    public bool RequestFullBody(FullBodyKind kind)
    {
        if (FullBody.Kind != FullBodyKind.None && (int)kind > (int)FullBody.Kind) return false; // 新 kind 优先级更低（int 更大）→ 拒绝
        FullBody.Kind = kind;
        FullBody.RecoverFade = 0f; // 新动作占用：清掉上一动作残留的恢复淡入
        return true;
    }

    /// <summary>设当前要播的全身 clip（起手 / 技能进段）。置一次性 <see cref="FullBodyRequest.ClipDirty"/>，动画层 Play 后清回。</summary>
    public void SetFullBodyClip(AnimationClip clip, float clipFade)
    {
        FullBody.Clip = clip;
        FullBody.ClipFade = clipFade;
        FullBody.ClipDirty = true;
    }

    /// <summary>释放全身通道（动作结束 / 被打断）+ 记录恢复淡入（LocomotionState 恢复时消费）。
    /// 清 ClipDirty 避免动画层那帧误判仍在全身态、慢一帧才让位。</summary>
    public void EndFullBody(float recoverFade)
    {
        FullBody.Kind = FullBodyKind.None;
        FullBody.Clip = null;
        FullBody.ClipDirty = false;
        FullBody.RecoverFade = recoverFade;
    }

    /// <summary>切枪进行中，WeaponComponent 用它门控开火。计时器到期自动清零（覆盖 Holster + Equip 两阶段）。</summary>
    public bool IsSwapping;

    /// <summary>换弹进行中。WeaponComponent 从 currentWeapon.IsReloading 镜像写入，用于动画 + 开火/近战门控。</summary>
    public bool IsReloading;

    // ── 上身 combat one-shot trigger（旧 WeaponHolster/WeaponSwap/Reload/Shoot 四个 bool 收敛成一个位掩码，见 CombatOneShot）──
    //   写入：WeaponComponent（切枪/换弹/开火镜像）→ RequestCombatOneShot(...)
    //   消费：UpperBodyLayerDriver.TryConsumeCombatTrigger 按优先级 TryTake 一个 → Layer 1 one-shot
    //   清除：无 weaponAnimSet / 死亡 / Detach → ClearCombatOneShots()（一行盖全部，加新动作不再逐个漏清）
    private uint _combatOneShots;

    /// <summary>请求一个上身 combat one-shot（幂等：同动作重复请求只置一次位，与旧"bool=true"一致）。</summary>
    public void RequestCombatOneShot(CombatOneShot a) => _combatOneShots |= 1u << (int)a;

    /// <summary>清空所有挂起的 combat one-shot（无武器 / 死亡 / 卸载时用）。</summary>
    public void ClearCombatOneShots() => _combatOneShots = 0;

    /// <summary>按优先级（<see cref="CombatOneShot"/> 声明序，低位优先）取出并清除一个挂起动作；无挂起返 false。
    /// 每帧取一个、其余留到下帧——与旧"多个 bool 各帧依次消费"等价。</summary>
    public bool TryTakeCombatOneShot(out CombatOneShot action)
    {
        if (_combatOneShots == 0)
        {
            action = default;
            return false;
        }

        int i = 0;
        for (uint m = _combatOneShots; (m & 1u) == 0; m >>= 1) i++; // 最低 set 位下标 = 最高优先级
        _combatOneShots &= ~(1u << i);
        action = (CombatOneShot)i;
        return true;
    }

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