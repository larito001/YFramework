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
    /// <summary>持枪人当前是否想开火（已经过瞄准/切枪/近战门控）。FireComponent 自己再叠射速冷却/弹夹等。</summary>
    public bool FireIntent;
    /// <summary>射线/弹道起点（世界坐标）。持枪人按角色胸高 + 朝前推算。</summary>
    public Vector3 FireOrigin;
    /// <summary>射击方向（单位向量，世界坐标）。持枪人按准星算。</summary>
    public Vector3 FireDirection;
    /// <summary>瞄准目标点（世界坐标）= AimComponent 投影到枪口高度平面的鼠标点。
    /// 直线武器用 Origin+Direction 即可；曲线/制导武器（导弹）需要精确目标点，用此字段。</summary>
    public Vector3 FireTarget;
}
