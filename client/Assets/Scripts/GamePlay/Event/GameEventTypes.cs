namespace YOTO
{
    public enum YOTOEventType
    {
        Space,

        // 刷新
        RefreshRoleList,
        RefreshProgress,
        RefreshPlayerProperty,
        GameTimerNotify,
        LootTimerNotify,
        VotEndNotify,

        RefreshBagList,
        RefreshTrainHP,
        RefreshTime,
        RefreshCurrency, // 货币余额变化(CurrencySystem 触发,UI 重读各币种刷新)
    }
}
