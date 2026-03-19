using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SearchPanel : UIPageBase<List<Vector2Int>>
{
    public YOTOScrollView bagList;
    public Button closeBtn;
    public YOTOScrollView searchList;

    public override void OnLoad()
    {
    }

    protected override void OnBeforeShow(List<Vector2Int> param)
    {
    }

    public override void OnShow()
    {
        closeBtn.onClick.AddListener(CloseSelf);
        bagList.Initialize();
        bagList.SetRenderer(ItemRender);
        searchList.Initialize();
        searchList.SetRenderer(SearchRender);
        searchList.SetData(PageParam.Count);
    }

    private void SearchRender(YOTOScrollViewItem obj, int index)
    {
        var item = obj as CommonItem;
        item.SetData(PageParam[index]);
    }

    private void ItemRender(YOTOScrollViewItem obj, int index)
    {
    }

    public override void OnHide()
    {
        closeBtn.onClick.RemoveAllListeners();
    }

    public override void OnResize()
    {
    }
}
