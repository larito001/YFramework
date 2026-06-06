using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 装备卡片 View(挂在卡片预制体 <c>Resources/UI/Equip/EquipCard.prefab</c> 根上)。
/// 只持有控件引用,数据绑定/点击由 <see cref="EquipPanel"/> instantiate 后填。卡片长相在 EquipCardBuilder/预制体里改。
/// </summary>
public class EquipCardView : MonoBehaviour
{
    public Image bg;                 // 卡底(绿=已拥有/灰=未拥有),也是 Button targetGraphic
    public Button button;            // 选中按钮(未拥有不可选)
    public Outline outline;          // 选中黄色描边(enabled 切换显隐)
    public Image qualityFrame;       // 品质框(道具图之下,按品质染色 = icon 背景)
    public Image pic;                // 道具图(快照/图标,preserveAspect)
    public TextMeshProUGUI nameText; // 名称
    public LayoutElement layout;     // 卡片尺寸 320×340(横向行布局用)
}
