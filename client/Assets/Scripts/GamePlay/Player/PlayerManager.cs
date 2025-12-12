using System.Collections;
using System.Collections.Generic;
using Dreamteck.Splines;
using Dreamteck.Splines.Examples;
using UnityEngine;
using UnityEngine.Events;
using YOTO;


public class PlayerManager : LogicPluginBase
{
    public static PlayerManager Instance;

    public PlayerManager()
    {
        Instance = this;
    }


   public enum PlayerCtrl
    {
        Train,
        Role
    }

    public TrainEngine trainEngine;
    public PlayerEntity playerEntity;
    private bool _isReborn = false;

    public void Init(UnityAction loadEndCallback)
    {
        //初始化角色和火车
        YFramework.resMgr.LoadGameObject("Train/Train", (obj) =>
        {
            var train = UnityEngine.Object.Instantiate(obj);
            trainEngine = train.GetComponentInChildren<TrainEngine>();
            var spline = GameObject.Find("Spline").GetComponent<SplineComputer>();

            trainEngine.SetTracer(spline);
            playerEntity = PlayerEntity.pool.GetItem(null);
            playerEntity.Location = GameStarter.PlayerOrgPos.position;
            EnemiesManager.instance.SetPlayer(playerEntity);
            loadEndCallback();
        });
    }

    public bool CheckPlayerIsInRange(Vector3 pos, float range)
    {
        if (playerEntity == null) return false;
        return (pos - playerEntity.Location).magnitude < range;
    }
    public void Switch(PlayerCtrl ctrl)
    {
        if (_isReborn) return;
        
        if (ctrl == PlayerCtrl.Train)
        {
            if (playerEntity!=null&&playerEntity.ObjTrans != null)
            {
                var dis = (trainEngine.transform.position - playerEntity.ObjTrans.position).magnitude;
                if (dis < 10)
                {
                    if (playerEntity != null)
                    {
                        var orbitCamera = YFramework.cameraMgr.getMainCamera().GetComponent<OrbitCamera>();
                        orbitCamera.Uload();
                        orbitCamera.distance = 50;
                        orbitCamera.Init(trainEngine.transform);

                        PlayerEntity.pool.RecoverItem(playerEntity);
                        playerEntity = null;
                        trainEngine.canMove = true;
                    }
                }
            }
        }
        else if (ctrl == PlayerCtrl.Role)
        {
            if (playerEntity == null)
            {
                playerEntity = PlayerEntity.pool.GetItem(null);
                playerEntity.Location = trainEngine.transform.position + new Vector3(5, 0, 5);
                var orbitCamera = YFramework.cameraMgr.getMainCamera().GetComponent<OrbitCamera>();
                orbitCamera.Uload();
                orbitCamera.distance = 20;
                orbitCamera.Init(playerEntity.ObjTrans);
                trainEngine.canMove = false;
            }
        }
    }

    public void RebornPlayer()
    {
        playerEntity = PlayerEntity.pool.GetItem(null);
        playerEntity.Location = trainEngine.transform.position + new Vector3(5, 0, 5);
        var orbitCamera = YFramework.cameraMgr.getMainCamera().GetComponent<OrbitCamera>();
        orbitCamera.Uload();
        orbitCamera.distance = 20;
        orbitCamera.Init(playerEntity.ObjTrans);
        _isReborn = false;
    }

    public void PlayerDie()
    {
        var orbitCamera = YFramework.cameraMgr.getMainCamera().GetComponent<OrbitCamera>();
        orbitCamera.Uload();
        orbitCamera.distance = 50;
        orbitCamera.Init(trainEngine.transform);
        PlayerEntity.pool.RecoverItem(playerEntity);
        playerEntity = null;
        _isReborn = true;
        Timers.inst.Add(3, (o) =>
        {
            RebornPlayer();
        });

    }
}