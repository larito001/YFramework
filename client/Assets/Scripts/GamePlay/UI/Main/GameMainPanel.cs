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
    public override void OnLoad()
    {
        scrollView.Initialize();
        scrollView.SetRenderer(ItemRender);
    }

    private void ItemRender(YOTOScrollViewItem arg1, int arg2)
    {
        
    }

    public override void OnShow()
    {
        scrollView.SetData(8);
    }

    public override void OnHide()
    {
      
    }

    public override void OnResize()
    {
       
    }
}