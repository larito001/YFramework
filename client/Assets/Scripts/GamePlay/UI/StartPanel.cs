using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YOTO;

public class StartPanel : UIPageBase
{
    public Button btn_new;
    public Button btn_continue;
    public Button btn_setting;
    public Button btn_quit;

    public override void OnLoad()
    {
        btn_new.onClick.AddListener(OnNewClick);
        btn_continue.onClick.AddListener(OnContinueClick);
        btn_setting.onClick.AddListener(OnSettingClick);
        btn_quit.onClick.AddListener(OnQuitClick);
    }

    private void OnNewClick()
    {
        GetService<YSceneManager>().SwitchScene(YSceneType.Home);
    }

    // 读取存档：存档系统接入后在此加载并进入对应场景
    private void OnContinueClick()
    {
        Debug.Log("[StartPanel] 读取存档功能待接入");
    }

    private void OnSettingClick()
    {
        Show<SettingPanel>();
    }

    private void OnQuitClick()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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
