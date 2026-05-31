namespace YOTO
{
    /// <summary>
    /// 货币种类。新增一种货币只需在此加一项,<see cref="CurrencySystem"/> 与其存档自动支持。
    /// 显式赋值:存档按数值落盘,枚举重排不会让旧档错位。
    /// </summary>
    public enum CurrencyType
    {
        Gold = 0,    // 金币(软货币:打猎掉落/出售获得)
        Diamond = 1, // 钻石(硬货币:充值/稀有奖励)
        Energy = 2,  // 体力(出发消耗,随时间恢复)
    }
}
