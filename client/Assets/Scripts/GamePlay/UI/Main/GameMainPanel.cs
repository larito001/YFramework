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
    public override void OnLoad()
    {
        
    }

    public override void OnShow()
    {
        YOTOFramework.timeMgr.DelayCall(()=>
        {
            YOTOFramework.uIMgr.Hide(UIEnum.GameMapPanel);

            CardPlugin.Instance.ShowCards();
        },3);
        YOTOFramework.timeMgr.DelayCall(()=>
        {
            YOTOFramework.uIMgr.Hide(UIEnum.LoadingPanel);
        },4);
    }

    public override void OnHide()
    {
      
    }

    public override void OnResize()
    {
       
    }
}