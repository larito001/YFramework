/// <summary>
/// 物品类型。与配表 <c>Item.Type</c>(uint32) 一一对应——配表里填 int，代码里
/// <c>(ItemType)item.Type</c> 转换读取，用于分类、整理排序与使用/丢弃规则。
/// 新增类型时两边同步：先在 Excel 的 type 列约定取值，再在此处补一项。
/// </summary>
public enum ItemType
{
    Other = 0,      // 其它
    Consumable = 1, // 消耗品（药水、食物等，可使用；UseItem 成功后扣除一个）
    Equipment = 2,  // 装备
    Material = 3,   // 材料
    QuestItem = 4,  // 任务物品（不可丢弃）
    Currency = 5    // 货币
}
