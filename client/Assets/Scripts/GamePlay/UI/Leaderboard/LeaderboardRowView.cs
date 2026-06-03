using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 排行榜行 View(挂在行预制体 <c>Resources/UI/Leaderboard/LeaderboardRow.prefab</c> 根上)。
/// <see cref="LeaderboardPanel"/> instantiate 后绑定名次/昵称/分数,并按是否自己/是否前三改底色与名次色。
/// 行长相在 LeaderboardRowBuilder/预制体里改。
/// </summary>
public class LeaderboardRowView : MonoBehaviour
{
    public Image bg;                  // 行底(普通灰/自己金)
    public TextMeshProUGUI rankText;  // 名次(前三橙,其余深色)
    public TextMeshProUGUI nameText;  // 昵称
    public TextMeshProUGUI scoreText; // 分数
}
