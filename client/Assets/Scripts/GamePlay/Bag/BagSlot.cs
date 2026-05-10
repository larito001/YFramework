using System;

[Serializable]
public class BagSlot
{
    public int itemId;
    public int count;
    public int slotIndex;

    public BagSlot()
    {
    }

    public BagSlot(int itemId, int count, int slotIndex)
    {
        this.itemId = itemId;
        this.count = count;
        this.slotIndex = slotIndex;
    }
}
