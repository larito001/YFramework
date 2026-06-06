using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 商店卡片 View(挂在卡片预制体 <c>Resources/UI/Shop/ShopCard.prefab</c> 根上)。
/// 只持有卡片内部各控件引用,不含业务逻辑——数据绑定/点击回调由 <see cref="ShopPanel"/> 在 instantiate 后填。
/// 卡片预制体由 <c>Tools/UI/Build ShopCard Prefab</c>(ShopCardBuilder)生成,改卡片长相直接改 Builder/预制体。
/// </summary>
public class ShopCardView : MonoBehaviour
{
    public Image bg;                  // 卡底(也是整卡点击的 targetGraphic)
    public Button cardButton;         // 整卡点击:选中并在上方展示模型(带模型的卡才启用)
    public Image qualityFrame;        // 品质框(道具图之下,按品质染色 = icon 背景)
    public Image pic;                 // 道具图(快照/图标,preserveAspect)
    public TextMeshProUGUI nameText;  // 名称
    public Image priceIcon;           // 价格币种图标(按 PriceType 运行时动态加载)
    public TextMeshProUGUI priceText; // 价格数字
    public Image buyBg;               // 购买按钮底(绿=可买/灰=买不起或已拥有)
    public Button buyButton;          // 购买按钮
    public TextMeshProUGUI buyLabel;  // 购买按钮文字(购买/已拥有)
    public GameObject selectFrame;    // 金色选中框(预置好,显隐即可,不再运行时拼 4 条边)
    public ItemIconDescButton infoBadge; // 右上角「i」描述按钮(预置好,运行时只填 itemId)
}
