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
        bagList.SetData(50);
    }

    private void ItemRender(YOTOScrollViewItem arg1, int arg2)
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
