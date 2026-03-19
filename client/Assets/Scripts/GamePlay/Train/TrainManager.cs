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
        if (train?.ObjTrans != null)
        {
            train.canMove = true;
        }
    }

    public void OnUnUseTrain()
    {
        if (train?.ObjTrans != null)
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
        TrainEntity.Configure(ctx.Get<SceneReferenceService>(), ctx.Get<EventMgr>());
    }

    public void Shutdown()
    {
        TrainEntity.Configure(null, null);
    }
}
