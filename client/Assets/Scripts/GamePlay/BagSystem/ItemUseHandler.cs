using YFramework.Config;

/// <summary>
/// 物品使用时传给处理器的上下文。
/// </summary>
public struct ItemUseContext
{
    /// <summary>被使用物品的配表定义(protobuf 生成的 <see cref="Item"/>)。</summary>
    public Item Item;
    /// <summary>被使用的已放置实例。注意:这是**使用前**的快照,handler 返回后系统才扣减数量,
    /// 不要在 handler 里缓存它并指望读到扣减后的 <c>count</c>。</summary>
    public PlacedItem Placed;
}

/// <summary>
/// 物品使用逻辑接口。具体效果(回血、加 Buff、开宝箱等)实现此接口,通过
/// <see cref="BagSystem.RegisterUseHandler"/> 以物品 <c>Item.Id</c> 为键注册。
/// 返回是否使用成功(成功且物品为 <see cref="ItemType.Consumable"/> 时由 BagSystem 扣 1 个,堆叠减数量、扣到 0 才移除整堆)。
/// </summary>
public interface IItemUseHandler
{
    bool OnUse(in ItemUseContext context);
}
