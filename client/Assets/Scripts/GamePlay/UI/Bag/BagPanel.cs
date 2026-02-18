using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BagPanel : UIPageBase
{
    public YOTOScrollView bagList;
    public Button closeBtn;

    public override void OnLoad()
    {
    }

    public override void OnShow()
    {
        closeBtn.onClick.AddListener(CloseSelf);
        bagList.Initialize();
        bagList.SetRenderer(ItemRender);
        // bagList.SetData(BagPlugin.Instance.GetListCount);
    }

    private void ItemRender(YOTOScrollViewItem obj, int index)
    {
        // var item = obj as CommonItem;
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