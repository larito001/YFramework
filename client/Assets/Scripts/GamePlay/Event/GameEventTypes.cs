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
        RefreshLoadout,  // 装备拥有/选中变化(LoadoutSystem 触发,装备界面刷新)
    }
}
