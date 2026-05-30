using YFramework.Config;

/// <summary>
/// 物品使用时传给处理器的上下文。
/// </summary>
public struct ItemUseContext
{
    /// <summary>被使用物品的配表定义（protobuf 生成的 <see cref="Item"/>）。</summary>
    public Item Item;
    /// <summary>所在背包槽位下标。</summary>
    public int SlotIndex;
    /// <summary>本次使用的数量（通常为 1）。</summary>
    public int Amount;
}

/// <summary>
/// 物品使用逻辑接口。具体效果（回血、加 Buff、开宝箱等）实现此接口，通过
/// <see cref="BagSystem.RegisterUseHandler"/> 以物品 <c>Item.Id</c> 为键注册。
/// 返回是否使用成功（成功且物品为 <see cref="ItemType.Consumable"/> 时由 BagSystem 扣除一个数量）。
///
/// 备注：当前 item 配表只有 Id/Name/Desc/Type/IconPath/MaxStack/SortPriority 字段，效果数值在 handler 内自定。
/// 若要把效果做成完全数据驱动，可在 item.xlsx 增列（如 useParam），重跑打表工具后在 handler 里读取。
/// </summary>
public interface IItemUseHandler
{
    bool OnUse(in ItemUseContext context);
}
