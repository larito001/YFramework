using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YOTO;

public class GameMainScene : GotSceneBase
{
    public override GotSceneType SceneType { get{return GotSceneType.GamePlay;} }
    public override string SceneName { get{return "GameMainScene";}}
    protected override void OnEnterScene()
    {

        WarehouseEntity warehouse = new WarehouseEntity();
        warehouse.SetEntity();
        warehouse.SetInVision(true);
        var warehouseObj =GameObject.Find("WareHouse");
        warehouse.Location =warehouseObj.transform.position ;
        warehouse.InstanceGObj();

        

        BagPlugin.Instance.ReStar();
        FlyTextMgr.Instance.Init();
        SceneResManager.Instance.Init();
        GotAStarManager.Instance.LoadPathFinding(() =>
        {
      
          
            YFramework.uIMgr.Show(UIEnum.GameMainPanel);
            
        
            YFramework.uIMgr.Hide(UIEnum.StartPanel);
            PlayerManager.Instance.Init(() =>
            {
                //
                // for (int i = 0; i < 20; i++)
                // {
                //     // todo：在范围内随机生成
                //     Vector3 basePos = PlayerManager.Instance.playerEntity.Location;
                //     float randomX = Random.Range(-50f, 50f);
                //     float randomZ = Random.Range(-50f, 50f);
                //     float randomY = 20f;
                //
                //     Vector3 spawnPos = basePos + new Vector3(randomX, randomY, randomZ);
                //
                //     EnemiesManager.instance.GenerateEnemyAt(spawnPos);
                // }
                EnemiesManager.instance.GenerateAtRange( PlayerManager.Instance.playerEntity.Location);
                EnterSceneComplete();
            });
   
        }, new string[] { "GraphCache" });
    }

    public override void Update()
    {
        base.Update();
        GotAStarManager.Instance.Update();
        FlyTextMgr.Instance.Update(Time.deltaTime);
        if (Input.GetKeyDown(KeyCode.F))
        {
      
            PlayerManager.Instance.Switch();
        }
        else if (Input.GetKeyDown(KeyCode.M))
        {

            EnemiesManager.instance.GenerateAtRange( PlayerManager.Instance.playerEntity.ObjTrans.position);
        }
    }

    protected override void OnLeaveScene()
    {
        GotAStarManager.Instance.UnloadPathFinding();
        LeaveSceneComplete();
    }
}
