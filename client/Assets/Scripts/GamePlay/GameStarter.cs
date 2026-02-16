// using System;
// using System.Collections;
// using System.Collections.Generic;
// using Steamworks;
// using UnityEngine;
// using YOTO;
//
// public class GameStarter : MonoBehaviour
// {
//     public Transform playerOrgPos;
//     public static Transform PlayerOrgPos;
//     public bool isTest = false;
//     // Start is called before the first frame update
//     void Awake()
//     {
//         PlayerOrgPos = playerOrgPos;
//         YFramework.Instance.Init(this.gameObject);
//         YFramework.Instance.isTest = isTest;
//         // 将帧率限制为60FPS
//         Application.targetFrameRate = 60;
//
//         // 可选：在移动设备上关闭垂直同步以获得更精确的帧率控制
// #if !UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID)
//         QualitySettings.vSyncCount = 0;
// #endif
//         FlyTextMgr.Instance.Init();
//         YFramework.uIMgr.Show(UIEnum.StartPanel);
//
//
//         // if (SteamManager.Initialized)
//         // {
//         //     SteamNetworkingUtils.InitRelayNetworkAccess();
//         //     string name = SteamFriends.GetPersonaName();
//         //     Debug.LogError("GetPersonaName:" + name);
//         //    
//         // }
//         Debug.Log("GameRoot 加载完成");
//         
//     }
//
//     private void Update()
//     {
//
//         if (Input.GetKeyDown(KeyCode.O))
//         {
//             BagPlugin.Instance.GMGetAllItem();
//         }
//     }
// }
