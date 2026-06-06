using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 图鉴卡片 View(挂在卡片预制体 <c>Resources/UI/Codex/CodexCard.prefab</c> 根上)。
/// 解锁的卡在 <see cref="picHost"/> 上挂 3D 模型转台(运行时由 <see cref="CodexPanel"/> 创建并管理生命周期),
/// 未解锁显示 <see cref="lockedText"/>(「？」)。卡片长相在 CodexCardBuilder/预制体里改。
/// </summary>
public class CodexCardView : MonoBehaviour
{
    public Image bg;                   // 卡片底框(根上挂 Outline 黑色描边)
    public TextMeshProUGUI indexText;  // 左上角编号(动物 Id;解锁与否都显示)
    public RectTransform picHost;      // 模型预览/问号 容器(解锁:挂 3D 转台;未解锁:显示问号)
    public TextMeshProUGUI lockedText; // 「？」(未解锁显示,解锁隐藏)
    public Image badgeBg;              // 徽标底(绿=已解锁/灰=未解锁)
    public TextMeshProUGUI badgeLabel; // 「积分 X」/「未解锁」
}
