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
}
