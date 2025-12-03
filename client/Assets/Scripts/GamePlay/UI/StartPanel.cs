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
    public Button btn_new;
    public Button btn_continue;
    public Button btn_setting;
    public override void OnLoad()
    {
        btn_new.onClick.AddListener(OnNewClick);
    }

    private void OnNewClick()
    {
        YFramework.uIMgr.Show(UIEnum.LoadingPanel);

        Timers.inst.Add(1,(o) =>
        {
            CloseSelf();
            PlayerEntity playerEntity = PlayerEntity.pool.GetItem(null);
            playerEntity.Location = new Vector3(20, 1, -25);
            YFramework.uIMgr.Show(UIEnum.GameMainPanel);
            // EnemiesManager.instance.SetPlayer(playerEntity);
        });
        Timers.inst.Add(5, (o) =>
        {
            YFramework.uIMgr.Hide(UIEnum.LoadingPanel);
        });
    }

    public override void OnShow()
    {
        
    }

    public override void OnHide()
    {
   
    }

    public override void OnResize()
    {
      
    }
}
