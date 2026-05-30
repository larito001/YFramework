using YFramework.Config;

/// <summary>
/// 物品使用时传给处理器的上下文。
/// </summary>
public struct ItemUseContext
{
    /// <summary>被使用物品的配表定义(protobuf 生成的 <see cref="Item"/>)。</summary>
    public Item Item;
    /// <summary>被使用的已放置实例(网格空间背包中,物品按实例存在,不堆叠)。</summary>
    public PlacedItem Placed;
}

/// <summary>
/// 物品使用逻辑接口。具体效果(回血、加 Buff、开宝箱等)实现此接口,通过
/// <see cref="BagSystem.RegisterUseHandler"/> 以物品 <c>Item.Id</c> 为键注册。
/// 返回是否使用成功(成功且物品为 <see cref="ItemType.Consumable"/> 时由 BagSystem 移除该实例)。
/// </summary>
public interface IItemUseHandler
{
    bool OnUse(in ItemUseContext context);
}
