using System.Collections;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;
using YOTO;

public class GameStarter : MonoBehaviour
{
    // Start is called before the first frame update
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
        PlayerEntity playerEntity = PlayerEntity.pool.GetItem(null);
        playerEntity.Location= new Vector3(0, 0, 0);
        FlyTextMgr.Instance.Init();
        Debug.Log("GameRoot 加载完成");
        EnemiesManager.instance.SetPlayer(playerEntity);
        // for (int i = 0; i < 100; i++)
        // {
        //     EnemiesManager.instance.GenerateEnemyAt(new Vector3(Random.Range(-20, 20), Random.Range(-20, 20), Random.Range(-20, 20)));
        // }
        YOTOFramework.timeMgr.LoopCall(() =>
        {

            EnemiesManager.instance.GenerateAtPlayerMoveDir();
        },3);
    }

  
}
