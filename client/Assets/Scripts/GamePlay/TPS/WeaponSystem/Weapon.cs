using UnityEngine;

/// <summary>
/// 武器 Actor。数据 + 装备态字段；行为后续通过 IWeaponComponent 装配（FireComponent / AmmoComponent / SpreadComponent…）。
///
/// 字段分组：
///   数据（构造时定）：Name / ModelPath / LocalPosition / LocalEuler
///   装备态（WeaponManager.Mount/Unmount 写）：IsEquipped / OwnerActorId（基类字段）/ MountSocketName
///   —— WeaponView 监听这三个字段决定挂到角色 socket 还是隐藏
///
/// 持枪人 ID 复用 <see cref="Actor.OwnerActorId"/> 基类字段，不再单独定义 OwnerCharacterId。
/// ModelPath 是 Resources 相对路径，prefab 只是 mesh + materials，WeaponView 运行时 AddComponent 挂到实例上。
/// </summary>
public class Weapon : Actor
{
    public string Name;
    public string ModelPath;
    /// <summary>当前挂载的 localPosition（view 读）。Mount/MountOnBack 写入对应的 Hand/Back 配置值。</summary>
    public Vector3 LocalPosition;
    /// <summary>当前挂载的 localEuler（view 读）。Mount/MountOnBack 写入对应的 Hand/Back 配置值。</summary>
    public Vector3 LocalEuler;

    /// <summary>挂到手部 socket 时使用的 local pose（Factory 配，等同旧的 LocalPosition 默认值）。</summary>
    public Vector3 HandLocalPosition;
    public Vector3 HandLocalEuler;
    /// <summary>挂到背部 socket 时使用的 local pose。Factory 按武器形状/朝向各自调。</summary>
    public Vector3 BackLocalPosition;
    public Vector3 BackLocalEuler;

    public bool IsEquipped;
    public string MountSocketName;

    // ── 动画 ──
    /// <summary>武器对应的 <see cref="WeaponAnimSet"/> ScriptableObject 资源路径（Resources 相对路径，如 "Weapon/Animations/Pistol"）。
    /// 空 / null = 持有者 view 默认 idle pose（不切 Animancer state）。
    /// 切枪时 WeaponComponent.ApplySwap 把本路径写到 Character.CurrentWeaponAnimSetPath，
    /// CharacterView 检测 WeaponAnimDirty trigger 后 ResMgr.Load&lt;WeaponAnimSet&gt; + 用 Animancer.Play 替代 Animator state machine。
    /// 详见 ARCHITECTURE "动画接口协议" + docs/Animancer 武器动画指南.md。</summary>
    public string AnimSetPath;

    // ── 切枪过场时长（覆盖 WeaponComponent 默认；<0 = 用组件默认值）──
    /// <summary>收回(Holster)本武器的过场时长（秒）。**应 >= Holster clip 长度**，否则收刀/收枪动画没播完就被切到取出阶段。
    /// 收回播的是"被收起的旧武器"的 Holster，所以这个值跟旧武器走。&lt;0 = 用 <see cref="WeaponComponent.HolsterDuration"/> 默认。</summary>
    public float HolsterDuration = -1f;
    /// <summary>取出(Equip)本武器的过场时长（秒）= **Equip clip 自然长度**（原速）。一般不用动（默认即所有武器共用的 ≈1.8s）；
    /// 仅当本武器的取出动画长度不同才覆盖。&lt;0 = 用 <see cref="WeaponComponent.WeaponSwapDuration"/> 默认。实际过场 = 本值 / <see cref="SwapAnimSpeed"/>。</summary>
    public float EquipDuration = -1f;

    /// <summary>取出/收回(Equip/Holster)动画播放倍率（**切枪/收枪速度旋钮**）。1=原速；>1=更快（如 1.5=快 50%）；&lt;1=更慢。
    /// 过场时长按本倍率自动缩放（= clip 长度 / 倍率），动画始终完整播放、不被切。切枪时写到 Character.SwapAnimSpeed。</summary>
    public float SwapAnimSpeed = 1f;

    // ── 技能映射（近战 / 武器专属技能）──
    /// <summary>左键（OnFireDown）释放的技能下标，指向持枪人 <see cref="SkillCastComponent"/>.SkillPaths。
    /// -1（默认）= 左键正常开火（常规枪）。&gt;=0 = 近战/技能武器：左键改放该技能、且永不开火（WeaponComponent 门控 + InputComponent 不写 FireHeld）。
    /// 切枪时 WeaponComponent.ApplySwap 写到 Character.WeaponPrimarySkill，InputComponent 读。</summary>
    public int PrimarySkillIndex = -1;
    /// <summary>V 键释放的技能下标。-1（默认）= 回退技能 0（保持旧"V 近战"行为，不必每把枪显式配）。
    /// 切枪时写到 Character.WeaponSecondarySkill。</summary>
    public int SecondarySkillIndex = -1;

    /// <summary>本武器的连招招式图（<see cref="ComboGraph"/>）资源路径（Resources 相对路径，如 "Character/Player/Skills/KnifeCombo"）。
    /// 空 / null（默认）= 本武器无连招：攻击键回退到单招（左键放 <see cref="PrimarySkillIndex"/>、V 放 <see cref="SecondarySkillIndex"/>）。
    /// 配了图 = 走连招（左键/V 按图分支接招）。切枪时 WeaponComponent.ApplySwap 镜像到 Character.CurrentComboGraphPath，ComboComponent 据此重载图。</summary>
    public string ComboGraphPath;

    // ── 开火几何（武器自配，持枪人读着算 FireOrigin） ──
    /// <summary>枪口相对持有者 (Position + Rotation) 的本地坐标偏移（米）。x=横向、y=高度、z=朝前推。
    /// 持枪人组件（玩家 WeaponComponent / 塔 TowerWeaponComponent）算 FireOrigin 都是：
    /// <c>FireOrigin = holder.Position + holder.Rotation * MuzzleLocalOffset</c>
    ///
    /// 让"枪口位置" 配置归武器侧（不同武器枪口长度不一样、装在不同持有者上偏移也不一样）。
    /// Factory 按 武器 × 持有者 组合各自配——同一把 RifleA prefab 给玩家用 (0,1.2,0.6)，给塔用 (0,1.5,0.4)。
    /// 默认值匹配 Character 持枪：身高 1.2m + 朝前 0.6m 避开自己 capsule。</summary>
    public Vector3 MuzzleLocalOffset = new Vector3(0f, 1.2f, 0.6f);

    // ── 开火意图（持枪人每帧写，FireComponent 子组件读）──
    /// <summary>持枪人当前是否想开火（已经过瞄准/切枪/近战/换弹门控）。FireComponent 自己再叠射速冷却/弹夹等。</summary>
    public bool FireIntent;
    /// <summary>射线/弹道起点（世界坐标）。持枪人按 MuzzleLocalOffset 算后写入。</summary>
    public Vector3 FireOrigin;
    /// <summary>射击方向（单位向量，世界坐标）。持枪人按准星算。</summary>
    public Vector3 FireDirection;
    /// <summary>瞄准目标点（世界坐标）= AimComponent 投影到枪口高度平面的鼠标点。
    /// 直线武器用 Origin+Direction 即可；曲线/制导武器（导弹）需要精确目标点，用此字段。</summary>
    public Vector3 FireTarget;

    // ── 弹药 / 换弹（ReloadComponent 管，FireComponent 读/扣）──
    /// <summary>弹匣容量。Factory 配。0 = 无限弹药（FireComponent 不扣不查）。</summary>
    public int MagCapacity = 30;
    /// <summary>当前弹匣余弹。Factory 初始化时一般等于 MagCapacity。</summary>
    public int CurrentAmmo = 30;
    /// <summary>换弹进行中。ReloadComponent 唯一 writer（外部可置 false 强制打断）；FireComponent 当作"禁火"读。</summary>
    public bool IsReloading;
    /// <summary>WeaponComponent 转发 R 键事件用的一次性 trigger。ReloadComponent 消费后清回 false。</summary>
    public bool ReloadRequest;

    // ── 后坐力动画 ──
    /// <summary>是否用大后坐力动画（Rifle_ShootGrenade）。false=小后坐力(Rifle_ShootOnce)。</summary>
    public bool HeavyRecoil;
    /// <summary>后坐力动画播放速度倍率（Animator state.speed via RecoilSpeed param）。
    /// 应配成 clipDuration / FireInterval 量级，让单次动画能在两次开火之间播完。</summary>
    public float RecoilAnimSpeed = 1f;
    /// <summary>一次性 trigger：FireComponent 在一次成功射击后置 true，
    /// WeaponComponent 消费并清回，转写到 Owner.Shoot 喂动画 trigger。</summary>
    public bool ShootEvent;
}
