using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YOTO;

/// <summary>
/// 纯背包面板(<see cref="UIEnum.BagPanel"/>):单个居中网格,渲染玩家背包 <see cref="BagSystem.Bag"/>。
/// 拖拽/合并/交换/旋转/拆分/丢弃/使用/tooltip 全部继承自 <see cref="GridHostPanelBase"/>。
/// 容量文本与整理/关闭按钮为本面板特有。
/// </summary>
public class BagPanel : GridHostPanelBase
{
    [Header("背包专属")]
    public BagGridView bagGrid;
    public Button sortBtn;
    public Button closeBtn;
    public TextMeshProUGUI capacityText;

    public override void OnLoad()
    {
        InitServices();
        if (sortBtn != null) sortBtn.onClick.AddListener(() => bagSystem.SortBag());
        if (closeBtn != null) closeBtn.onClick.AddListener(CloseSelf);
    }

    public override void OnShow()
    {
        SetupGrid(bagGrid);
        bagGrid.Bind(bagSystem.Bag);
        eventMgr.Add(YOTOEventType.RefreshBagList, RefreshCapacity);
        RefreshCapacity();
    }

    public override void OnHide()
    {
        eventMgr.Remove(YOTOEventType.RefreshBagList, RefreshCapacity);
        CommonHide();
    }

    public override void OnResize() { }

    private void RefreshCapacity()
    {
        if (capacityText == null || bagSystem?.Bag == null) return;
        var bag = bagSystem.Bag;
        int used = 0;
        var items = bag.Items;
        for (int i = 0; i < items.Count; i++)
            used += bag.LocalCells(items[i].itemId, items[i].rotation).Length;
        capacityText.text = $"{used}/{bag.Width * bag.Height}";
    }
}
