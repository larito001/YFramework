using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 任务卡片 View(挂在卡片预制体 <c>Resources/UI/Task/TaskCard.prefab</c> 根上)。
/// 奖励格预置 3 个(固定位 x=40/408/776),默认隐藏;<see cref="TaskPanel"/> 绑定时按 物品→金币→体力 顺序填、多余的隐藏。
/// 卡片长相在 TaskCardBuilder/预制体里改。
/// </summary>
public class TaskCardView : MonoBehaviour
{
    public Image bg;                       // 卡片底框
    public TextMeshProUGUI titleText;      // 标题(左上)
    public TextMeshProUGUI progressText;   // 进度(右上,富文本)
    public GameObject[] rewardRoots;       // 3 个奖励格根(显隐)
    public Image[] rewardIcons;            // 3 个奖励图标(有图=精灵/无图=色块)
    public TextMeshProUGUI[] rewardCounts; // 3 个 ×数量
    public Image actionBg;                 // 右下按钮底(橙前往/绿领取/灰已领取)
    public Button actionButton;            // 右下按钮
    public TextMeshProUGUI actionLabel;    // 按钮文字
}
