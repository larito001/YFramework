
using System;
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




    public PlayerEntity playerEntity;
    public bool _isReborn = false;
    public override void ReStartGame(Action callBack = null)
    {
        if (playerEntity != null)
        {
            PlayerEntity.pool.RecoverItem(playerEntity);
            playerEntity = null;
        }
        playerEntity = PlayerEntity.pool.GetItem(null);
        playerEntity.Location = GameLoop.Instance.transform.Find("PlayerPos").position;
        base.ReStartGame(callBack);
    }
    
    public bool CheckPlayerIsInRange(Vector3 pos, float range)
    {
        if (playerEntity == null || playerEntity.ObjTrans == null) return false;
        return (pos - playerEntity.ObjTrans.position).magnitude < range;
    }



    public void OnUsePlayer()
    {
        if (_isReborn) return;
        // var orbitCamera = YFramework.cameraMgr.getMainCamera().GetComponent<OrbitCamera>();
        // orbitCamera.Uload();
        // orbitCamera.distance = 20;
        playerEntity = PlayerEntity.pool.GetItem(null);
        playerEntity.Location = TrainManager.Instance.GetTrainOutPos();
        // if (train.ObjTrans!=null&&playerEntity != null && playerEntity.ObjTrans != null)
        // {
        //     var dis = (train.ObjTrans.transform.position - playerEntity.ObjTrans.position).magnitude;
        //     if (dis < 10)
        //     {
        //         if (playerEntity != null)
        //         {
        //             var orbitCamera = YFramework.cameraMgr.getMainCamera().GetComponent<OrbitCamera>();
        //             orbitCamera.Uload();
        //             orbitCamera.distance = 50;
        //             orbitCamera.Init(train.ObjTrans);
        //
        //             PlayerEntity.pool.RecoverItem(playerEntity);
        //             playerEntity = null;
        //             train.canMove = true;
        //         }
        //     }
        // }
        // else if (playerEntity == null)
        // {
        //     playerEntity = PlayerEntity.pool.GetItem(null);
        //     playerEntity.Location =
        //         train.ObjTrans.position + train.ObjTrans.right * 10 + new Vector3(0, 1, 0);
        //     var orbitCamera = YFramework.cameraMgr.getMainCamera().GetComponent<OrbitCamera>();
        //     orbitCamera.Uload();
        //     orbitCamera.distance = 20;
        //     orbitCamera.Init(playerEntity.ObjTrans);
        //     train.canMove = false;
        // }
    }

    public void OnUnUsePlayer()
    {
        PlayerEntity.pool.RecoverItem(playerEntity);
        playerEntity = null;
    }
    public void RebornPlayer()
    {
        TrainManager.Instance.OnUnUseTrain();
        _isReborn = false;
        OnUsePlayer();
    }

    public void PlayerDie()
    {
        TrainManager.Instance.OnUseTrain();
        PlayerEntity.pool.RecoverItem(playerEntity);
        playerEntity = null;
        _isReborn = true;
        Timers.inst.Add(3, (o) => { RebornPlayer(); });
    }

    public void OnMouseDown(Vector3 hitPoint, float dt)
    {
        if (playerEntity != null)
        {     playerEntity.OnMouseClick(hitPoint, dt);
            
        }

       
    }

    public void OnMouseUp()
    {
        if (playerEntity != null)
        {
            playerEntity.OnMouseUp();
        }
  
    
    }
    public Vector3 GetPlayerLocation()
    {
        if (playerEntity != null)
        {
            return playerEntity.ObjTrans.position;
        }
        return Vector3.zero;
    }
}