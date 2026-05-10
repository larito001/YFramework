using YOTO;

public class BagDataContaner : DataContaner<BagData>
{
    private BagData data = new BagData();

    public override string SaveKey => "bag_v1";

    public override BagData GetData()
    {
        return data;
    }

    public override void __SetData(BagData v)
    {
        data = v ?? new BagData();
        if (data.slots == null) data.slots = new System.Collections.Generic.List<BagSlot>();
        if (data.currencyEntries == null) data.currencyEntries = new System.Collections.Generic.List<CurrencyEntry>();
    }
}
