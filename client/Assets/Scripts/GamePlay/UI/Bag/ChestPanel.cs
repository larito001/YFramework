using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YOTO;

/// <summary>
/// 宝箱面板(<see cref="UIEnum.ChestPanel"/>):**左侧玩家背包 + 右侧宝箱**双网格。
/// 左侧复用与纯背包同一份 <see cref="BagSystem.Bag"/>(同一 GridBag,数据共享);
/// 右侧渲染 <see cref="ChestSystem.Current"/>。两网格都是 <see cref="BagGridView"/>,
/// 拖拽跨网格转移由 <see cref="GridHostPanelBase"/> 统一处理(背包↔宝箱互拖)。
/// 标题色区分:宝箱侧标题用暖色,与纯背包面板视觉区分。
/// </summary>
public class ChestPanel : GridHostPanelBase
{
    [Header("宝箱专属")]
    public BagGridView bagGrid;     // 左:玩家背包
    public BagGridView chestGrid;   // 右:宝箱
    public TextMeshProUGUI chestTitle;
    public Button closeBtn;

    private ChestSystem chestSystem;

    public override void OnLoad()
    {
        InitServices();
        chestSystem = GetService<ChestSystem>();
        if (closeBtn != null) closeBtn.onClick.AddListener(CloseSelf);
    }

    public override void OnShow()
    {
        // 宝箱面板自带左侧背包,与纯背包面板互斥:打开时先隐藏纯背包,避免两个背包 UI 重叠。
        Hide<BagPanel>();

        SetupGrid(bagGrid);
        SetupGrid(chestGrid);

        bagGrid.Bind(bagSystem.Bag);
        var chest = chestSystem.Current;
        chestGrid.Bind(chest);

        if (chestTitle != null && chestSystem.CurrentConfig != null)
            chestTitle.text = chestSystem.CurrentConfig.Name;
    }

    public override void OnHide()
    {
        CommonHide();
        chestSystem.CloseChest(); // 清当前引用(宝箱内容仍由世界实例持有)
    }

    /// <summary>双击快速转移:背包物品→宝箱,宝箱物品→背包(自动找空位/并堆)。</summary>
    public override void OnItemDoubleClick(BagGridView view, BagItemWidget widget)
    {
        var from = view.Bag;
        var to = (view == bagGrid) ? chestGrid.Bag : bagGrid.Bag;
        if (from == null || to == null) return;

        // 目标锚点传 (0,0):Transfer 精确落位失败会自动并堆/找空位,等价于"丢进另一容器"。
        if (GridBag.Transfer(from, widget.InstanceId, to, 0, 0, 0))
        {
            HideTooltip(); // 源 widget 已销毁,OnPointerExit 不会触发,主动隐藏 tooltip 避免残留
            view.Refresh();
            (view == bagGrid ? chestGrid : bagGrid).Refresh();
        }
    }

    public override void OnResize() { }
}
