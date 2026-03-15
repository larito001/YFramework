using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SelectTowerPanel : UIPageBase
{
    public YOTOScrollView towerList;
    public Button closeBtn;
    public Button closeBtn2;
    public override void OnLoad()
    {
        
    }

    public override void OnShow()
    {
        towerList.Initialize();
        towerList.SetRenderer(ItemRender);
        closeBtn.onClick.RemoveAllListeners();
        closeBtn.onClick.AddListener(CloseSelf);
        closeBtn2.onClick.RemoveAllListeners();
        closeBtn2.onClick.AddListener(CloseSelf);
    }

    private void ItemRender(YOTOScrollViewItem arg1, int arg2)
    {
    }

    public override void OnHide()
    {
    }

    public override void OnResize()
    {
    }
}
