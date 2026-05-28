/// <summary>
/// 伤害元素类型。Physical 是默认（无元素属性），其他对应"魔法 / 元素" 子类。
/// 目标方 ResistComponent（未实现）按 Element 查抗性算最终伤害折扣，详见 <see cref="DamageCalculator"/>。
///
/// 加新元素：在此 enum 末尾追加。同步更新 ResistComponent 配置 + UI 图标。
/// </summary>
public enum DamageElement
{
    Physical = 0,
    Fire = 1,
    Ice = 2,
    Lightning = 3,
    Holy = 4,
    Dark = 5,
}
