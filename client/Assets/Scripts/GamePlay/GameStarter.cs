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
        
        FlyTextMgr.Instance.Init();
        YOTOFramework.uIMgr.Show(UIEnum.StartPanel);
        
    }

    private void Update()
    {
        FlyTextMgr.Instance.Update(Time.deltaTime);
    }
}
