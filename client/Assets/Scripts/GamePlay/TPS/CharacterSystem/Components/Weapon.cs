using UnityEngine;

/// <summary>
/// 武器 Actor。数据 + 装备态字段；行为后续通过 IWeaponComponent 装配（FireComponent / AmmoComponent / SpreadComponent…）。
///
/// 字段分组：
///   数据（构造时定）：Name / ModelPath / LocalPosition / LocalEuler
///   装备态（WeaponManager.Mount/Unmount 写）：IsEquipped / OwnerCharacterId / MountSocketName
///   —— WeaponView 监听这三个字段决定挂到角色 socket 还是隐藏
///
/// ModelPath 是 Resources 相对路径，prefab 只是 mesh + materials，WeaponView 运行时 AddComponent 挂到实例上。
/// </summary>
public class Weapon : Actor
{
    public string Name;
    public string ModelPath;
    public Vector3 LocalPosition;
    public Vector3 LocalEuler;

    public bool IsEquipped;
    public int OwnerCharacterId = -1;
    public string MountSocketName;

    // ── 开火意图（持枪人每帧写，FireComponent 子组件读）──
    /// <summary>持枪人当前是否想开火（已经过瞄准/切枪/近战/换弹门控）。FireComponent 自己再叠射速冷却/弹夹等。</summary>
    public bool FireIntent;
    /// <summary>射线/弹道起点（世界坐标）。持枪人按角色胸高 + 朝前推算。</summary>
    public Vector3 FireOrigin;
    /// <summary>射击方向（单位向量，世界坐标）。持枪人按准星算。</summary>
    public Vector3 FireDirection;
    /// <summary>瞄准目标点（世界坐标）= AimComponent 投影到枪口高度平面的鼠标点。
    /// 直线武器用 Origin+Direction 即可；曲线/制导武器（导弹）需要精确目标点，用此字段。</summary>
    public Vector3 FireTarget;

    // ── 弹药 / 换弹（WeaponComponent 管，FireComponent 读/扣）──
    /// <summary>弹匣容量。Factory 配。0 = 无限弹药（FireComponent 不扣不查）。</summary>
    public int MagCapacity = 30;
    /// <summary>当前弹匣余弹。Factory 初始化时一般等于 MagCapacity。</summary>
    public int CurrentAmmo = 30;
    /// <summary>换弹总时长（秒），匹配 Animator Reload 动画长度。WeaponComponent 倒计时。</summary>
    public float ReloadDuration = 1.5f;
    /// <summary>换弹进行中。WeaponComponent 唯一 writer；FireComponent 当作"禁火"读。</summary>
    public bool IsReloading;

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
