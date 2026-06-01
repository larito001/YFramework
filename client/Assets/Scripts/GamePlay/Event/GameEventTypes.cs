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
        Shoot,           // 开枪(GameMainPanel 触发;相机抖动/音效/枪口闪等监听)
        RefreshTask,     // 任务进度/领取状态变化(TaskProgressSystem 触发,任务界面刷新)
        RefreshCodex,    // 动物图鉴解锁变化(CodexSystem 触发,图鉴界面刷新)
    }
}
