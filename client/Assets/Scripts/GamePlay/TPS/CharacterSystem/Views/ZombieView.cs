/// <summary>
/// 僵尸 view。动画驱动器换成 <see cref="ZombieAnimancerController"/>（locomotion + 技能链 + 脚本根动画位移），
/// 其余（CharacterController 物理回写、受击闪白、死亡溶解、飘字、Tick 委托、根动画应用）全部继承自 <see cref="CharacterView"/>。
///
/// 用法：僵尸 prefab 挂 ZombieView，其 Character 的 CurrentCharacterAnimSetPath 指向一个 <see cref="CharacterAnimSet"/>.asset
/// （locomotion + death）。技能（攻击/飞扑）= <see cref="SkillDef"/> 资产，挂在 <see cref="SkillCastComponent"/> 上，由 <see cref="AIInputComponent"/> 的 OnCastSkill 事件触发。
/// </summary>
public class ZombieView : CharacterView
{
    protected override LocomotionAnimController CreateController() => new ZombieAnimancerController();
}
