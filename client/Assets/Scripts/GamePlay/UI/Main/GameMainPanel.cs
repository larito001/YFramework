using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using YOTO;

public class GameMainPanel : UIPageBase
{
    public YOTOScrollView scrollView;
    public Button bagBtn;
    public override void OnLoad()
    {
        scrollView.Initialize();
        scrollView.SetRenderer(ItemRender);
    }

    private void ItemRender(YOTOScrollViewItem arg1, int index)
    {
        var item = arg1 as CommonItem; 
        item.SetData(BagPlugin.Instance.GetItemByIndex(index));
    }

    public override void OnShow()
    {
        bagBtn.onClick.AddListener(OnBagBtnClick);
        scrollView.SetData(BagPlugin.Instance.GetListCount>8?8:BagPlugin.Instance.GetListCount);
    }

    private void OnBagBtnClick()
    {
        YFramework.uIMgr.Show(UIEnum.BagPanel);
    }

    public override void OnHide()
    {
      
    }

    public override void OnResize()
    {
       
    }
}