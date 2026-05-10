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

        OnItemPickup,
        OnItemDrop,
    }
}
