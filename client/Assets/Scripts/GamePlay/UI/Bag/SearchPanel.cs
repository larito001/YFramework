using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SearchPanel : UIPageBase
{
    public YOTOScrollView bagList;
    public Button closeBtn;
    public YOTOScrollView searchList;
    public override void OnLoad()
    {
    }

    private List<Vector2Int> rewardList;
    public override void BeforeShow(object param)
    {
        rewardList = (List<Vector2Int>)param;
        base.BeforeShow(param);
    }

    public override void OnShow()
    {
        closeBtn.onClick.AddListener(CloseSelf);
        bagList.Initialize();
        bagList.SetRenderer(ItemRender);
        // bagList.SetData(BagPlugin.Instance.GetListCount);
        searchList.Initialize();
        searchList.SetRenderer(SearchRender);
        searchList.SetData(rewardList.Count);
    }

    private void SearchRender(YOTOScrollViewItem obj, int index)
    {
        var item = obj as CommonItem;
        item.SetData(rewardList[index]);
    }

    private void ItemRender(YOTOScrollViewItem obj, int index)
    {
        var item = obj as CommonItem;
        // item.SetData(BagPlugin.Instance.GetItemByIndex(index));
    }

    public override void OnHide()
    {
        closeBtn.onClick.RemoveAllListeners();
    }

    public override void OnResize()
    {
    }
}
