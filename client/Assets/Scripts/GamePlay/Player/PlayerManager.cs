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


    public TrainEntity train;

    public PlayerEntity playerEntity;
    private bool _isReborn = false;

    public void Init(UnityAction loadEndCallback)
    {
        //初始化角色和火车
        train = new TrainEntity();
        train.TrainInit();

        playerEntity = PlayerEntity.pool.GetItem(null);
        playerEntity.Location = GameStarter.PlayerOrgPos.position;
        loadEndCallback();
    }

    public bool CheckPlayerIsInRange(Vector3 pos, float range)
    {
        if (playerEntity == null || playerEntity.ObjTrans == null) return false;
        return (pos - playerEntity.ObjTrans.position).magnitude < range;
    }

    public bool CheckTrainIsInRange(Vector3 pos, float range)
    {
        if (train == null || train.ObjTrans == null) return false;
        return (pos - train.ObjTrans.position).magnitude < range;
    }

    public void Switch()
    {
        if (_isReborn) return;
        if (train.ObjTrans!=null&&playerEntity != null && playerEntity.ObjTrans != null)
        {
            var dis = (train.ObjTrans.transform.position - playerEntity.ObjTrans.position).magnitude;
            if (dis < 10)
            {
                if (playerEntity != null)
                {
                    var orbitCamera = YFramework.cameraMgr.getMainCamera().GetComponent<OrbitCamera>();
                    orbitCamera.Uload();
                    orbitCamera.distance = 50;
                    orbitCamera.Init(train.ObjTrans);

                    PlayerEntity.pool.RecoverItem(playerEntity);
                    playerEntity = null;
                    train.canMove = true;
                }
            }
        }
        else if (playerEntity == null)
        {
            playerEntity = PlayerEntity.pool.GetItem(null);
            playerEntity.Location =
                train.ObjTrans.position + train.ObjTrans.right * 10 + new Vector3(0, 1, 0);
            var orbitCamera = YFramework.cameraMgr.getMainCamera().GetComponent<OrbitCamera>();
            orbitCamera.Uload();
            orbitCamera.distance = 20;
            orbitCamera.Init(playerEntity.ObjTrans);
            train.canMove = false;
        }
    }

    public void RebornPlayer()
    {
        playerEntity = PlayerEntity.pool.GetItem(null);
        playerEntity.Location =
            train.ObjTrans.position +train.ObjTrans.right * 10 + new Vector3(0, 1, 0);
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
        orbitCamera.Init(train.ObjTrans);
        PlayerEntity.pool.RecoverItem(playerEntity);
        playerEntity = null;
        _isReborn = true;
        Timers.inst.Add(3, (o) => { RebornPlayer(); });
    }

    public void OnMouseDown(Vector3 hitPoint, float dt)
    {
        if (playerEntity != null)
            playerEntity.OnMouseClick(hitPoint, dt);
    }

    public void OnMouseUp()
    {
    
    }
}