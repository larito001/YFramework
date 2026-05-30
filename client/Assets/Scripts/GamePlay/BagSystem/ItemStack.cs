using System;

/// <summary>
/// 运行时的一格物品（itemId + 数量）。itemId 对应配表 ItemConfig.Id(int32)。
/// 背包每个槽位要么为 null（空），要么持有一个 ItemStack。标记为 Serializable，便于 JsonUtility 存档。
/// </summary>
[Serializable]
public class ItemStack
{
    public int itemId;
    public int count;

    public ItemStack() { }

    public ItemStack(int itemId, int count)
    {
        this.itemId = itemId;
        this.count = count;
    }

    public bool IsEmpty => itemId <= 0 || count <= 0;

    public ItemStack Clone() => new ItemStack(itemId, count);

    public override string ToString() => $"{itemId} x{count}";
}
