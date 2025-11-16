using System;
using System.Collections;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;
using YOTO;

public class GameStarter : MonoBehaviour
{
    
    void Start()
    {
        YOTOFramework.Instance.Init();
        // 将帧率限制为60FPS
        Application.targetFrameRate = 60;

        // 可选：在移动设备上关闭垂直同步以获得更精确的帧率控制
#if !UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID)
        QualitySettings.vSyncCount = 0;
#endif
        // if (SteamManager.Initialized)
        // {
        //     SteamNetworkingUtils.InitRelayNetworkAccess();
        //     string name = SteamFriends.GetPersonaName();
        //     Debug.LogError("GetPersonaName:" + name);
        //    
        // }
        FlyTextMgr.Instance.Init();
        CardPlugin.Instance.HideCards();
        YOTOFramework.uIMgr.Show(UIEnum.StartPanel);
        
    }

    private void Update()
    {
        FlyTextMgr.Instance.Update(Time.deltaTime);
    }
}
