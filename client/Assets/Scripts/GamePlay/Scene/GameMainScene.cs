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

        FlyTextMgr.Instance.Init();
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

                for (int i = 0; i < 1; i++)
                {
                    // todo：在范围内随机生成
                    Vector3 basePos = PlayerManager.Instance.playerEntity.Location;
           

                    Vector3 spawnPos = basePos + new Vector3(5, 5, 15);

                    EnemiesManager.instance.GenerateEnemyAt(spawnPos);
                }
                EnterSceneComplete();
            });
   
        }, new string[] { "GraphCache" });
    }

    public override void Update()
    {
        base.Update();
        GotAStarManager.Instance.Update();
        FlyTextMgr.Instance.Update(Time.deltaTime);
        if (Input.GetKeyDown(KeyCode.T))
        {
            PlayerManager.Instance.Switch(PlayerManager.PlayerCtrl.Train);
        }
        else if(Input.GetKeyDown(KeyCode.Y))
        {
            PlayerManager.Instance.Switch(PlayerManager.PlayerCtrl.Role);
            
        }
        else if (Input.GetKeyDown(KeyCode.M))
        {

            for (int i = 0; i < 1; i++)
            {
                // todo：在范围内随机生成
                Vector3 basePos = new Vector3();
                if (PlayerManager.Instance.playerEntity != null)
                {
                    basePos = PlayerManager.Instance.playerEntity.Location;
                }
                else
                {
                    basePos = PlayerManager.Instance.trainEngine.transform.position;
                }
                
                Vector3 spawnPos = basePos + new Vector3(5, 5, 15);

                EnemiesManager.instance.GenerateEnemyAt(spawnPos);
            }  
        }
    }

    protected override void OnLeaveScene()
    {
        GotAStarManager.Instance.UnloadPathFinding();
        LeaveSceneComplete();
    }
}
