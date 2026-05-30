using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YFramework.Config;

/// <summary>
/// 背包单个格子视图（<see cref="YOTOScrollViewItem"/> 子类，配合 <see cref="YOTOScrollView"/> 池化复用）。
/// 由 <see cref="BagPanel"/> 在渲染回调里调 <see cref="Bind"/> 填充数据；点击触发回调（默认=使用物品）。
///
/// 预制体结构（在 Unity 里搭好，挂本组件，拖引用）：
///   BagItem (本组件 + Button)
///     ├─ Icon   (Image)        → icon
///     ├─ Count  (TextMeshProUGUI) → count（数量，1 个时可隐藏）
///     └─ Name   (TextMeshProUGUI) → nameText（可选）
/// </summary>
public class BagItemView : YOTOScrollViewItem
{
    public Button button;
    public Image icon;
    public TextMeshProUGUI count;
    public TextMeshProUGUI nameText;
    [Tooltip("空格子时显示的占位（可选，留空则隐藏 icon）")]
    public GameObject emptyHint;

    private int slotIndex = -1;
    private Action<int> onClick;

    /// <summary>
    /// 绑定一格数据。stack 为 null 表示空格子。
    /// </summary>
    /// <param name="slot">槽位下标</param>
    /// <param name="stack">该格内容（可空）</param>
    /// <param name="cfg">物品配表定义（可空）</param>
    /// <param name="iconSprite">已加载好的图标（可空）</param>
    /// <param name="clickCallback">点击回调，参数为槽位下标</param>
    public void Bind(int slot, ItemStack stack, Item cfg, Sprite iconSprite, Action<int> clickCallback)
    {
        slotIndex = slot;
        onClick = clickCallback;

        bool empty = stack == null || stack.IsEmpty;

        if (icon != null)
        {
            icon.enabled = !empty && iconSprite != null;
            icon.sprite = empty ? null : iconSprite;
        }
        if (emptyHint != null) emptyHint.SetActive(empty);

        if (count != null)
        {
            // 数量 >1 才显示，避免每格都挂个 "1"
            bool showCount = !empty && stack.count > 1;
            count.gameObject.SetActive(showCount);
            if (showCount) count.text = stack.count.ToString();
        }

        if (nameText != null)
            nameText.text = empty || cfg == null ? string.Empty : cfg.Name;

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
            button.interactable = !empty;
        }
    }

    private void HandleClick() => onClick?.Invoke(slotIndex);

    public override void OnHidItem()
    {
        if (button != null) button.onClick.RemoveListener(HandleClick);
        onClick = null;
        slotIndex = -1;
    }
}
