using Steamworks;
using UnityEngine;
using YOTO;

//控制游戏模式（场景）
public class GameRoot : SingletonMono<GameRoot>
{
    public void Init()
    {
        // 将帧率限制为60FPS
        Application.targetFrameRate = 60;

        // 可选：在移动设备上关闭垂直同步以获得更精确的帧率控制
#if !UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID)
        QualitySettings.vSyncCount = 0;
#endif
        if (SteamManager.Initialized)
        {
            SteamNetworkingUtils.InitRelayNetworkAccess();
            string name = SteamFriends.GetPersonaName();
            Debug.LogError("GetPersonaName:" + name);
           
        }
        FlyTextMgr.Instance.Init();
        Debug.Log("GameRoot 加载完成");
    }
    
    
}