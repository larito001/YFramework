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
        Timers.inst.Add(2,(o) =>
        {
            TowerManager.Instance.TrackInit();

            WarehouseEntity warehouse = new WarehouseEntity();
            warehouse.SetEntity();
            warehouse.SetInVision(true);
            var warehouseObj = GameObject.Find("WareHouse");
            warehouse.Location = warehouseObj.transform.position;
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
                    EnemiesManager.instance.Init(() =>
                    {
                        EnterSceneComplete();
                        GameDayNightManager.Instance.ResetDayNight();
                        YFramework.uIMgr.Show(UIEnum.GuidePanel);
                    });
                });
            }, new string[] { "GraphCache" });
        });

    }

    public override void Update(float dt)
    {
        base.Update(dt);
        GotAStarManager.Instance.Update();
        GameDayNightManager.Instance.Update(dt);
        FlyTextMgr.Instance.Update(Time.deltaTime);
        if (Input.GetKeyDown(KeyCode.F))
        {
            PlayerManager.Instance.Switch();
        }
        else if (Input.GetKeyDown(KeyCode.M))
        {
            // EnemiesManager.instance.GenerateAtRange(PlayerManager.Instance.playerEntity.ObjTrans.position);
        }
    }

    protected override void OnLeaveScene()
    {
        GotAStarManager.Instance.UnloadPathFinding();
        LeaveSceneComplete();
    }
}