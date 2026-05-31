namespace YOTO
{
    /// <summary>
    /// 货币种类。数值即 <c>currency</c> 配表主键(id),名称/图标走配表(<see cref="CurrencySystem.DisplayName"/>)。
    /// 从 1 起编号:proto3 主键 0 保留。新增货币:配表加一行 + 此处加一项(数值对齐 id)。
    /// </summary>
    public enum CurrencyType
    {
        Gold = 1,    // 金币(软货币:打猎掉落/出售获得)
        Diamond = 2, // 钻石(硬货币:充值/稀有奖励)
        Energy = 3,  // 体力(出发消耗,随时间恢复)
    }
}
