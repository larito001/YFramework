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
