using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YOTO;

public class StartPanel : UIPageBase
{
    [SerializeField]
    private Button btnStart;
    public override void OnLoad()
    {
        
    }

    public override void OnShow()
    {
        btnStart.onClick.AddListener(OnStartClick);
    }

    private void OnStartClick()
    {
        YOTOFramework.uIMgr.Show(UIEnum.GameMapPanel);
        CloseSelf();
    }

    public override void OnHide()
    {
        btnStart.onClick.RemoveListener(OnStartClick);
    }

    public override void OnResize()
    {
      
    }
}
