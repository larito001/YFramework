using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using YOTO;

public class FinishPanel : UIPageBase
{
    public TextMeshProUGUI winName;
    public override void OnLoad()
    {
        
    }

    public override void OnShow()
    {
        YOTOFramework.timeMgr.DelayCall(() =>
        {
            YOTOFramework.uIMgr.Show(UIEnum.LoadingPanel);
        },2);
        YOTOFramework.timeMgr.DelayCall(() =>
        {
            YOTOFramework.uIMgr.Hide(UIEnum.GameMainPanel);
            YOTOFramework.uIMgr.Show(UIEnum.StartPanel);
            CardPlugin.Instance.HideCards();
            CloseSelf();
        },5);
        YOTOFramework.timeMgr.DelayCall(() =>
        {
            YOTOFramework.uIMgr.Hide(UIEnum.LoadingPanel);
        },6);
    }

    public override void OnHide()
    {
        
    }

    public override void OnResize()
    {
        
    }
}
