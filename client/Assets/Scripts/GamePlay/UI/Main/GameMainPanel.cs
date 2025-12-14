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
    public GameObject parent;
    public List<MCardCtrl> mCards = new List<MCardCtrl>();

    public override void OnLoad()
    {
        var cards = parent.GetComponentsInChildren<MCardCtrl>();
        mCards.Clear();
        for (var i = 0; i < cards.Length; i++)
        {
            mCards.Add(cards[i]);
        }
    }

    public override void OnShow()
    {
        YOTOFramework.timeMgr.DelayCall(() => { YOTOFramework.uIMgr.Hide(UIEnum.GameMapPanel); }, 3);
        YOTOFramework.timeMgr.DelayCall(() => { YOTOFramework.uIMgr.Hide(UIEnum.LoadingPanel); }, 4);
    }

    public override void OnHide()
    {
    }

    public override void OnResize()
    {
    }
}