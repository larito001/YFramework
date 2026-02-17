using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YOTO;

public class TrainManager : IGameService
{
    public Dictionary<float, bool> trackFixDic = new Dictionary<float, bool>();


    private TrainEntity train;
    public static float firstFix = 0.26f;
    public static float secondFix = 0.38f;
    public static float thirdFix = 0.85f;
    List<TrackFixEntity> trackFixList = new List<TrackFixEntity>();
    

    public bool CheckTrainIsInRange(Vector3 pos, float range)
    {
        if (train == null || train.ObjTrans == null) return false;
        return (pos - train.ObjTrans.position).magnitude < range;
    }

    public void OnMouseDown(Vector3 hitPoint, float dt)
    {
        if (train != null)
        {
            train.OnMouseClick(hitPoint, dt);
        }
    }


    public void OnUseTrain()
    {
        if (train.ObjTrans != null)
        {
            // var orbitCamera = YFramework.cameraMgr.getMainCamera().GetComponent<OrbitCamera>();
            // orbitCamera.Uload();
            // orbitCamera.distance = 50;
            // orbitCamera.Init(train.ObjTrans);
            train.canMove = true;
        }
    }

    public void OnUnUseTrain()
    {
        if (train.ObjTrans != null)
        {
            train.canMove = false;
        }
    }

    public void OnMouseUp()
    {
        if (train != null)
        {
            train.OnMouseUp();
        }
    }

    public Vector3 GetTrainOutPos()
    {
        return train.ObjTrans.position + train.ObjTrans.right * 10 + new Vector3(0, 1, 0);
    }

 

    public Vector3 GetTrainPos()
    {
        return train.ObjTrans.position;
    }

    public void Init(GameContext ctx)
    {
        if (train != null)
        {
            train.RecoverObject();
            train = null;
        }

        //初始化角色和火车
        train = new TrainEntity();
        train.TrainInit();
        trackFixDic.Clear();
        trackFixDic.Add(firstFix, false);
        trackFixDic.Add(secondFix, false);
        trackFixDic.Add(thirdFix, false);
        foreach (var trackFixEntity in trackFixList)
        {
            trackFixEntity.RecoverObject();
        }

        trackFixList.Clear();
        TrackFixEntity trackFix = new TrackFixEntity();
        trackFix.SetEntity(firstFix);
        trackFix.SetInVision(true);
        var trackFixobj = GameObject.Find("trackFixPos");
        trackFix.Location = trackFixobj.transform.position;
        trackFix.InstanceGObj();
        trackFixList.Add(trackFix);

        TrackFixEntity trackFix2 = new TrackFixEntity();
        trackFix2.SetEntity(secondFix);
        trackFix2.SetInVision(true);
        var trackFixobj2 = GameObject.Find("trackFixPos2");
        trackFix2.Location = trackFixobj2.transform.position;
        trackFix2.InstanceGObj();
        trackFixList.Add(trackFix2);

        TrackFixEntity trackFix3 = new TrackFixEntity();
        trackFix3.SetEntity(thirdFix);
        trackFix3.SetInVision(true);
        var trackFixobj3 = GameObject.Find("trackFixPos3");
        trackFix3.Location = trackFixobj3.transform.position;
        trackFix3.InstanceGObj();
        trackFixList.Add(trackFix3);
    }

    public void Shutdown()
    {
        
    }
}