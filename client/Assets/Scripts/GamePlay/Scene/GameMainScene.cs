 using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YOTO;

public class GameMainScene : GotSceneBase
{
    public override GotSceneType SceneType
    {
        get { return GotSceneType.GamePlay; }
    }

    public override string SceneName
    {
        get { return "GameMainScene"; }
    }

    protected override void OnLoadingEnd()
    {
        base.OnLoadingEnd();
        GameLoop.Instance.Ctx.Get<UIMgr>().Show(UIEnum.GameMainPanel);
        GameLoop.Instance.Ctx.Get<UIMgr>().Hide(UIEnum.StartPanel);
        GameLoop.Instance.Ctx.Get<UIMgr>().Show(UIEnum.GuidePanel);
    }

    protected override void OnEnterScene()
    {
        var ctx = GameLoop.Instance.Ctx;
        var playerManager = ctx.Get<PlayerManager>();
        playerManager.GeneratePlayer();
        
        EnterSceneComplete();
        // WarehouseEntity warehouse = new WarehouseEntity();
        // warehouse.SetEntity();
        // warehouse.SetInVision(true);
        // var warehouseObj = GameObject.Find("WareHouse");
        // warehouse.Location = warehouseObj.transform.position;
        // warehouse.InstanceGObj();
        // PlayerManager.Instance.ReStartGame(() =>
        // {
        //     EnterSceneComplete();
        // });
        //加载路径
        // GotAStarManager.Instance.LoadPathFinding(() =>
        // {
        //     TrainManager.Instance.ReStartGame(() =>
        //     {
        //
        //         EnterSceneComplete();
        //         TowerManager.Instance.ReStartGame(() =>
        //         {
        //             PlayerManager.Instance.ReStartGame(() =>
        //             {
        //                 EnemiesManager.instance.ReStartGame(() =>
        //                 {
        //                     //局内背包
        //                     BagPlugin.Instance.ReStartGame(() =>
        //                     {
        //                          
        //                         //资源
        //                         SceneResManager.Instance.ReStartGame(() =>
        //                         {
        //                             GameDayNightManager.Instance.ReStartGame(() =>
        //                             {
        //                           
        //                     
        //                        
        //                             });
        //                         });
        //                  
        //                     });
        //                 
        //                 
        //                 });
        //             });
        //         });x
        //     });
        // }, new string[] { "GraphCache" });
    }

    private bool isInTrain = false;
    // public override void Update(float dt)
    // {
    //     base.Update(dt);
    //     GotAStarManager.Instance.Update();
    //     GameDayNightManager.Instance.Update(dt);
    //
    //     if (Input.GetKeyDown(KeyCode.F))
    //     {
    //        
    //         // if (isInTrain)
    //         // {
    //         //     PlayerManager.Instance.OnUsePlayer();
    //         //     TrainManager.Instance.OnUnUseTrain();
    //         //     isInTrain=!isInTrain;
    //         // }
    //         // else
    //         // {
    //         //     if (TrainManager.Instance.CheckTrainIsInRange(PlayerManager.Instance.GetPlayerLocation(), 10))
    //         //     {
    //         //         TrainManager.Instance.OnUseTrain();
    //         //         PlayerManager.Instance.OnUnUsePlayer();  
    //         //         isInTrain=!isInTrain;
    //         //     }
    //         //
    //         // }
    //
    //     }
    // }

    protected override void OnLeaveScene()
    {
        GotAStarManager.Instance.UnloadPathFinding();
        LeaveSceneComplete();
    }
}