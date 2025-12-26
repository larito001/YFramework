using System.Collections;
using System.Collections.Generic;
using Dreamteck.Splines;
using UnityEngine;

public class TrainWagonEntity : ObjectBase
{
    SplinePositioner positioner;
    private SplineComputer temp;
    public override string GetModelLayer()
    {
        return "Default";
    }

    protected override void AfterInstanceGObj()
    {
        positioner = ObjTrans.GetComponent<SplinePositioner>();
        if (positioner != null)
        {
            positioner.spline = temp;
            positioner.followTarget = _follower;
            positioner.followTargetDistance = _offset;
        }
        
        TowerManager.Instance.GenerateTowerBaseAtTransform(ObjTrans, new Vector3(0, 2, 3));
        TowerManager.Instance.GenerateTowerBaseAtTransform(ObjTrans, new Vector3(0, 2, -3));
        TowerManager.Instance.GenerateTowerBaseAtTransform(ObjTrans, new Vector3(0, 2, 1.5f));
        TowerManager.Instance.GenerateTowerBaseAtTransform(ObjTrans, new Vector3(0, 2, -1.5f));
    }

    protected override void BeforeRecover(bool isDelete)
    {
    }

    public void TrainInit()
    {
        SetInVision(true);
        SetPrefabBundlePath("Train/Wagon");
        InstanceGObj();
    }

    private float _offset;
    SplineFollower _follower;

    public void SetFollowTarget(float offset,SplineFollower follower)
    {
        if (positioner)
        {
            positioner.followTarget = follower;
            positioner.followTargetDistance = offset;
        }
        else
        {
            _offset = offset;
            _follower = follower;
        }
        
   
    }
    public void SetTracer(SplineComputer spline)
    {
        if (ObjTrans != null)
        {
            positioner.spline = spline;
        }
        else
        {
            temp= spline;
        }
        
    }
}