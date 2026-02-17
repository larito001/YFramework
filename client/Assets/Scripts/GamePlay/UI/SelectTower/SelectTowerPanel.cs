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
      
        // towerList.SetData(TowerManager.Instance.towerDatas.Count);
        closeBtn.onClick.RemoveAllListeners();
        closeBtn.onClick.AddListener(CloseSelf);
        closeBtn2.onClick.RemoveAllListeners();
        closeBtn2.onClick.AddListener(CloseSelf);
    }

    private void ItemRender(YOTOScrollViewItem arg1, int arg2)
    {
        var item =arg1 as SelectTowerItem;
        // item.RefreshItem(TowerManager.Instance.towerDatas[arg2]);
    }

    public override void OnHide()
    {
        // TowerManager.Instance.CurrentClickBase = null;
    }

    public override void OnResize()
    {
    }
}
