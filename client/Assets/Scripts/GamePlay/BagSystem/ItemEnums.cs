using UnityEngine;

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

/// <summary>
/// 物品品质。与配表 <c>Item.Quality</c>(uint32) 一一对应，决定背包格背景配色（暗黑/塔科夫风格）。
/// 新增品质时两边同步：先在 Excel 的 quality 列约定取值，再在此处与 <see cref="ItemQualityPalette"/> 补一项。
/// </summary>
public enum ItemQuality
{
    Common = 0,    // 普通（灰白）
    Uncommon = 1,  // 优秀（绿）
    Rare = 2,      // 稀有（蓝）
    Epic = 3,      // 史诗（紫）
    Legendary = 4  // 传说（橙）
}

/// <summary>
/// 品质配色表：格背景色（半透，图标盖其上）与强调色（tooltip 名称/边框）。
/// UI（<c>BagPanel</c>/tooltip）统一从这里取色，改配色只动这一处。
/// </summary>
public static class ItemQualityPalette
{
    /// <summary>格背景色（半透明，物品图标会盖在上面）。</summary>
    public static Color Background(ItemQuality q)
    {
        switch (q)
        {
            case ItemQuality.Uncommon:  return new Color(0.20f, 0.45f, 0.20f, 0.85f); // 绿
            case ItemQuality.Rare:      return new Color(0.18f, 0.35f, 0.62f, 0.85f); // 蓝
            case ItemQuality.Epic:      return new Color(0.45f, 0.25f, 0.62f, 0.85f); // 紫
            case ItemQuality.Legendary: return new Color(0.70f, 0.45f, 0.15f, 0.85f); // 橙
            default:                    return new Color(0.35f, 0.35f, 0.38f, 0.85f); // 普通灰白
        }
    }

    /// <summary>强调色（tooltip 名称文字 / 边框，不透明）。</summary>
    public static Color Accent(ItemQuality q)
    {
        switch (q)
        {
            case ItemQuality.Uncommon:  return new Color(0.40f, 0.85f, 0.40f, 1f);
            case ItemQuality.Rare:      return new Color(0.40f, 0.65f, 1.00f, 1f);
            case ItemQuality.Epic:      return new Color(0.75f, 0.50f, 1.00f, 1f);
            case ItemQuality.Legendary: return new Color(1.00f, 0.70f, 0.30f, 1f);
            default:                    return new Color(0.85f, 0.85f, 0.88f, 1f);
        }
    }
}
