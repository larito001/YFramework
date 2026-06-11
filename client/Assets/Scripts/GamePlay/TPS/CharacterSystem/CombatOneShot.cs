/// <summary>
/// 上身 combat one-shot 动作种类——叠在 Layer 1 持枪 pose 上播一段、播完回 base（由 <see cref="UpperBodyLayerDriver"/> 的 OneShot 态驱动）。
///
/// 取代旧的 4 个独立 bool trigger（WeaponHolster / WeaponSwap / Reload / Shoot）。在 <see cref="Character"/> 上以**位掩码**存储，
/// 写入走 <see cref="Character.RequestCombatOneShot"/>、消费走 <see cref="Character.TryTakeCombatOneShot"/>、清空走 <see cref="Character.ClearCombatOneShots"/>。
///
/// **声明序 = 消费优先级（高 → 低）**：同帧多个挂起时，TryTake 先取声明靠前的（每帧取一个，其余留到下帧）。
///   Holster（收回旧枪）&gt; Equip（取出新枪）&gt; Reload（换弹）&gt; Shoot（开火后坐力）。
///
/// **加新动作**（如丢手雷）只需三处：① 在合适优先级位插入一个枚举值；
/// ② <see cref="UpperBodyLayerDriver.TryConsumeCombatTrigger"/> 的解析 switch 加一个 case（选 clip / 参数）；
/// ③ <see cref="WeaponAnimSet"/> 加对应 clip。写入方调 <see cref="Character.RequestCombatOneShot"/> 即可——
/// 字段声明、优先级仲裁、清除、消费循环全部通用，**不再逐个改散落的位置**。
/// </summary>
public enum CombatOneShot
{
    /// <summary>收回旧枪（切枪 Holster 阶段开始）。clip = WeaponAnimSet.Holster，speed = Character.SwapAnimSpeed。</summary>
    Holster,
    /// <summary>取出新枪（切枪 Equip 阶段开始）。clip = WeaponAnimSet.Equip，speed = Character.SwapAnimSpeed。</summary>
    Equip,
    /// <summary>换弹（IsReloading 上升沿）。clip = WeaponAnimSet.Reload。</summary>
    Reload,
    /// <summary>开火后坐力（每发成功开火）。clip = HeavyRecoil ? ShootHeavy : ShootLight，fade = ShootFade，speed = RecoilAnimSpeed。</summary>
    Shoot,
}
