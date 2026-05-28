/// <summary>
/// 防御塔 Actor。纯数据 + 组件容器，按 ARCHITECTURE 目录约定每个 Actor 子类对应一个 XxxSystem/ 顶级子目录。
///
/// 字段归属：通用字段（Position / Rotation / TeamId / OwnerActorId / HP 系）都在 <see cref="Actor"/> 基类。
/// 本类只持塔专属字段。
///
/// 数据流：
///   <see cref="TowerTargetingComponent"/>（先 Tick）扫敌写 <see cref="TargetActorId"/> + 旋转 Owner.Rotation 朝目标
///   <see cref="TowerWeaponComponent"/>（后 Tick）读 TargetActorId 反查目标 + 写 currentWeapon 开火字段
/// </summary>
public class Tower : Actor
{
    /// <summary>当前锁定的目标 Actor.ID，-1=无目标。<see cref="TowerTargetingComponent"/> 写，<see cref="TowerWeaponComponent"/> 读。
    /// 目标变化时 WeaponComponent 内部重置 aim timer 实现 telegraph 延迟开火。</summary>
    public int TargetActorId = -1;
}
