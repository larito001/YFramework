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
    public Image objectiveIcon;            // 左侧目标图标(配表 iconPath;暂用白图占位)
    public TextMeshProUGUI titleText;      // 标题
    public Slider progressSlider;          // 进度条(美术 Slider_02_LightGreen;value=进度比)
    public TextMeshProUGUI progressText;   // 进度条上的 X/Y
    public Image rewardIcon;               // 奖励图标(单个:物品/金币/体力)
    public TextMeshProUGUI rewardCount;    // ×数量
    public Image actionBg;                 // 按钮底(橙前往/绿领取/灰已领取)
    public Button actionButton;            // 按钮
    public TextMeshProUGUI actionLabel;    // 按钮文字
}
