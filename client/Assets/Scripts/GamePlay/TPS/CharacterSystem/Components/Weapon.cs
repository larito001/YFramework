using UnityEngine;

/// <summary>
/// 武器数据。WeaponComponent 持有 Weapon 列表，按 slot 索引切换。
/// AnimOverride 可选——不设就继续用 Animator 默认的 playerController（角色保持双手 Rifle 姿势）。
/// ModelPath 是 Resources 相对路径（如 "PistolPlaceholder"），view 会 Instantiate 该 prefab 并挂到角色 RightHandProp 骨骼。
/// LocalPosition / LocalEuler 是挂载点上的微调，每把武器初始值靠 Editor 目测后再写回这里。
/// 后续扩属性（伤害/射速/弹量/换弹时间...）直接加字段。
/// </summary>
public class Weapon
{
    public string Name;
    public AnimatorOverrideController AnimOverride;
    public string ModelPath;
    public Vector3 LocalPosition;
    public Vector3 LocalEuler;
}
