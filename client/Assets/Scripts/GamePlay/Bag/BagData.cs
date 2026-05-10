using System;
using System.Collections.Generic;

[Serializable]
public class CurrencyEntry
{
    public int itemId;
    public long count;

    public CurrencyEntry()
    {
    }

    public CurrencyEntry(int itemId, long count)
    {
        this.itemId = itemId;
        this.count = count;
    }
}

[Serializable]
public class BagData
{
    public List<BagSlot> slots = new List<BagSlot>();
    public List<CurrencyEntry> currencyEntries = new List<CurrencyEntry>();
}
