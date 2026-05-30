using System;

/// <summary>
/// 网格背包中的一个已放置物品实例(空间背包模型:物品占一片矩形格子,不堆叠,可 90° 旋转)。
/// 同一 <see cref="itemId"/> 可同时存在多个实例,用 <see cref="instanceId"/> 唯一区分。
/// 锚点 (x,y) 是物品左上角所在格;<see cref="baseW"/>/<see cref="baseH"/> 是配表原始宽高,
/// <see cref="rotated"/> 为 true 时显示/占格的有效宽高互换(<see cref="W"/>/<see cref="H"/>)。
/// 标记 Serializable 以便 JsonUtility 存档。
/// </summary>
[Serializable]
public class PlacedItem
{
    public int instanceId;
    public int itemId;
    public int x;       // 锚点列(左上)
    public int y;       // 锚点行(左上)
    public int baseW;   // 配表原始宽 (>=1)
    public int baseH;   // 配表原始高 (>=1)
    public bool rotated; // 是否旋转 90°

    public PlacedItem() { }

    public PlacedItem(int instanceId, int itemId, int x, int y, int baseW, int baseH, bool rotated = false)
    {
        this.instanceId = instanceId;
        this.itemId = itemId;
        this.x = x;
        this.y = y;
        this.baseW = baseW;
        this.baseH = baseH;
        this.rotated = rotated;
    }

    /// <summary>当前有效占格宽(随旋转交换)。</summary>
    public int W => rotated ? baseH : baseW;
    /// <summary>当前有效占格高(随旋转交换)。</summary>
    public int H => rotated ? baseW : baseH;

    /// <summary>格子 (cx,cy) 是否被本物品占据。</summary>
    public bool Covers(int cx, int cy) => cx >= x && cx < x + W && cy >= y && cy < y + H;

    public override string ToString() => $"#{instanceId} item={itemId} @({x},{y}) {W}x{H}{(rotated ? " R" : "")}";
}
