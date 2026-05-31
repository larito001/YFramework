namespace YOTO
{
    /// <summary>
    /// 商店分类(对应 item 配表 <c>shopCategory</c> 列与商店底部页签)。
    /// 0 = 不在分类商店出售;1/2/3 对应武器 / 瞄准镜 / 子弹商店。
    /// </summary>
    public enum ShopCategory
    {
        None = 0,
        Weapon = 1, // 武器商店
        Scope = 2,  // 瞄准镜商店
        Bullet = 3, // 子弹商店
    }
}
