/// <summary>
/// 防御塔 Actor。纯数据 + 组件容器，按 ARCHITECTURE 目录约定每个 Actor 子类对应一个 XxxSystem/ 顶级子目录。
///
/// 当前无塔专属字段——通用字段（Position / Rotation / TeamId / HP 系）都在 <see cref="Actor"/> 基类，
/// 塔的行为（targeting + 持武器开火）封装在 <see cref="TowerWeaponComponent"/>（通用 IActorComponent）。
///
/// 未来如果出现塔专属概念（如基座 / 炮塔分离 / 升级等级 / 攻击范围可视化）再加字段，并视情况引入 ITowerComponent 子家族。
/// </summary>
public class Tower : Actor
{
}
