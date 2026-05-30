using System;

/// <summary>
/// 网格背包中的一个已放置物品实例(空间背包模型:物品按形状占一片格子,不堆叠,可 4 向旋转)。
/// 同一 <see cref="itemId"/> 可同时存在多个实例,用 <see cref="instanceId"/> 唯一区分。
/// 锚点 (x,y) 是形状包围盒左上角所在格;<see cref="rotation"/> 为 0/1/2/3(顺时针 90° 步数)。
/// 实际占格/包围盒尺寸由 <see cref="ItemShape"/> 按 rotation 给出(见 GridBag),本类只存数据。
/// 标记 Serializable 以便 JsonUtility 存档。
/// </summary>
[Serializable]
public class PlacedItem
{
    public int instanceId;
    public int itemId;
    public int x;        // 锚点列(包围盒左上)
    public int y;        // 锚点行(包围盒左上)
    public int rotation; // 0..3,顺时针 90° 步数

    public PlacedItem() { }

    public PlacedItem(int instanceId, int itemId, int x, int y, int rotation = 0)
    {
        this.instanceId = instanceId;
        this.itemId = itemId;
        this.x = x;
        this.y = y;
        this.rotation = rotation & 3;
    }

    public override string ToString() => $"#{instanceId} item={itemId} @({x},{y}) rot={rotation}";
}
