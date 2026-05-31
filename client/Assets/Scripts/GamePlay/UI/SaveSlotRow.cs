using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YOTO;

/// <summary>
/// 读档界面里的一行存档项。模板挂在 <see cref="SaveSlotPanel"/> 预制体上(默认隐藏),
/// 运行时按存档槽数量克隆并 <see cref="Bind"/>。整行点击=载入,右侧按钮=删除。
/// </summary>
public class SaveSlotRow : MonoBehaviour
{
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI timeText;
    public Button loadBtn;   // 整行(载入)
    public Button deleteBtn; // 右侧(删除)

    /// <summary>用槽信息填充这一行,并接好载入/删除回调。</summary>
    public void Bind(SaveSlotInfo info, int index, Action onLoad, Action onDelete)
    {
        if (nameText != null)
        {
            nameText.text = string.IsNullOrEmpty(info.name) ? $"存档 {index + 1}" : info.name;
        }
        if (timeText != null)
        {
            timeText.text = info.lastPlayedUnix > 0
                ? DateTimeOffset.FromUnixTimeSeconds(info.lastPlayedUnix).LocalDateTime.ToString("yyyy-MM-dd HH:mm")
                : "—";
        }

        if (loadBtn != null)
        {
            loadBtn.onClick.RemoveAllListeners();
            loadBtn.onClick.AddListener(() => onLoad?.Invoke());
        }
        if (deleteBtn != null)
        {
            deleteBtn.onClick.RemoveAllListeners();
            deleteBtn.onClick.AddListener(() => onDelete?.Invoke());
        }
    }
}
