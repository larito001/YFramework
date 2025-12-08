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
        btn_setting.onClick.AddListener(OnSettingClick);
    }

    private void OnSettingClick()
    {
        YFramework.uIMgr.Show(UIEnum.SettingPanel);
    }

    private void OnNewClick()
    {
     
        YFramework.sceneMgr.SwitchScene(GotSceneType.GamePlay);
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