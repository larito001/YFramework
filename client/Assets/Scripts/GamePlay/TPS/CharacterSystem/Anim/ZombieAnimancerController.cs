/// <summary>
/// **僵尸 / AI** 动画驱动器。无武器、无瞄准——locomotion（idle/慢走/快跑 1D mixer）、death、**技能全身播放**
/// 全部在基类 <see cref="LocomotionAnimController"/> 通用层完成（技能由 <see cref="SkillCastComponent"/> 逻辑侧驱动，
/// 把当前段 clip 交到 Character.SkillClip，基类技能分支负责播放）。
///
/// 所以本类是**空的具体子类**，仅为给 <see cref="ZombieView"/> 一个可实例化的、无玩家武器/瞄准段的 controller。
/// 后续若需僵尸专属动画行为（特殊层、专属过渡等），在此 override 对应 virtual 钩子即可。
/// </summary>
public class ZombieAnimancerController : LocomotionAnimController
{
}
