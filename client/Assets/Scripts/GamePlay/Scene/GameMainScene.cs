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

    protected override void OnEnterScene()
    {  
        
        // WarehouseEntity warehouse = new WarehouseEntity();
        // warehouse.SetEntity();
        // warehouse.SetInVision(true);
        // var warehouseObj = GameObject.Find("WareHouse");
        // warehouse.Location = warehouseObj.transform.position;
        // warehouse.InstanceGObj();
        
        //加载路径
        GotAStarManager.Instance.LoadPathFinding(() =>
        {
            TrainManager.Instance.ReStartGame(() =>
            {
                TowerManager.Instance.ReStartGame(() =>
                {
                    PlayerManager.Instance.ReStartGame(() =>
                    {
                        EnemiesManager.instance.ReStartGame(() =>
                        {
                            //局内背包
                            BagPlugin.Instance.ReStartGame(() =>
                            {
                                 
                                //资源
                                SceneResManager.Instance.ReStartGame(() =>
                                {
                                    GameDayNightManager.Instance.ReStartGame(() =>
                                    {
                                  
                            
                                        YFramework.uIMgr.Show(UIEnum.GameMainPanel);
                                        YFramework.uIMgr.Hide(UIEnum.StartPanel);
                                        YFramework.uIMgr.Show(UIEnum.GuidePanel);
                                        EnterSceneComplete();
                                    });
                                });
                         
                            });
                  
          
                        });
                    });
                });
            });
        }, new string[] { "GraphCache" });
    }

    private bool isInTrain = false;
    public override void Update(float dt)
    {
        base.Update(dt);
        GotAStarManager.Instance.Update();
        GameDayNightManager.Instance.Update(dt);
        FlyTextMgr.Instance.Update(dt);
        if (Input.GetKeyDown(KeyCode.F))
        {
           
            if (isInTrain)
            {
                PlayerManager.Instance.OnUsePlayer();
                TrainManager.Instance.OnUnUseTrain();
                isInTrain=!isInTrain;
            }
            else
            {
                if (TrainManager.Instance.CheckTrainIsInRange(PlayerManager.Instance.GetPlayerLocation(), 10))
                {
                    TrainManager.Instance.OnUseTrain();
                    PlayerManager.Instance.OnUnUsePlayer();  
                    isInTrain=!isInTrain;
                }
           
            }

        }
    }

    protected override void OnLeaveScene()
    {
        GotAStarManager.Instance.UnloadPathFinding();
        LeaveSceneComplete();
    }
}