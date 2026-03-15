using System.Collections.Generic;
using Dreamteck.Splines;
using UnityEngine;
using YOTO;

public class TrainEntity : ObjectBase, IFixedTickable
{
    private static SceneReferenceService sharedSceneReferenceService;
    private static EventMgr sharedEventMgr;

    public static void Configure(SceneReferenceService sceneReferenceService, EventMgr eventMgr)
    {
        sharedSceneReferenceService = sceneReferenceService;
        sharedEventMgr = eventMgr;
    }

    public float acceleration = 3f;
    public float deceleration = -30f;
    public float maxSpeed = 5f;
    public SplineFollower follower;
    public bool canMove;
    public List<TrainWagonEntity> positioners = new List<TrainWagonEntity>();

    private float currentSpeed;
    private SplineComputer spline;
    private readonly List<Vector3> atkSlot = new List<Vector3>();

    public void TrainInit()
    {
        SetInVision(true);
        SetPrefabBundlePath("Train/Engine");
        InstanceGObj();
    }

    protected override void AfterInstanceGObj()
    {
        follower = ObjTrans.GetComponent<SplineFollower>();
        if (!sharedSceneReferenceService.TryGetTransform(SceneReferenceKeys.Spline, out var splineTransform))
        {
            Debug.LogError($"{SceneReferenceKeys.Spline} was not found.");
            return;
        }

        var targetSpline = splineTransform.GetComponent<SplineComputer>();
        if (targetSpline == null)
        {
            Debug.LogError($"SplineComputer component was not found on {SceneReferenceKeys.Spline}.");
            return;
        }

        var wagon = new TrainWagonEntity();
        wagon.TrainInit();
        wagon.SetFollowTarget(14, follower);
        positioners.Add(wagon);

        SetTracer(targetSpline);
        sharedEventMgr.TriggerEvent(YOTOEventType.RefreshTrainHP);
    }

    protected override void BeforeRecover(bool isDelete)
    {
    }

    public void SetTracer(SplineComputer targetSpline)
    {
        spline = targetSpline;
        follower.spline = targetSpline;
        for (var i = 0; i < positioners.Count; i++)
        {
            positioners[i].SetTracer(targetSpline);
        }
    }

    public Vector3 GetPosition()
    {
        return ObjTrans.position;
    }

    public List<Vector3> GetAtkSlot()
    {
        atkSlot.Clear();
        if (ObjTrans != null)
        {
            atkSlot.Add(ObjTrans.position);
        }

        return atkSlot;
    }

    public Vector3 GetForward()
    {
        return ObjTrans.forward;
    }

    public void OnHurtSomeone()
    {
    }

    public void OnSlowDown(float rate)
    {
    }

    public void OnMouseClick(Vector3 hitPoint, float dt)
    {
    }

    public void OnMouseUp()
    {
    }

    public void FixedTick(float fdt)
    {
        if (ObjTrans)
        {
            HandleInput();
        }
    }

    private void HandleInput()
    {
        float targetSpeed = 0f;

        if (canMove && Input.GetKey(KeyCode.W))
        {
            targetSpeed = maxSpeed;
        }
        else if (canMove && Input.GetKey(KeyCode.S))
        {
            targetSpeed = -maxSpeed;
        }

        float rate = Mathf.Abs(targetSpeed) < 0.01f ? Mathf.Abs(deceleration) : Mathf.Abs(acceleration);
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * Time.deltaTime);
        follower.followSpeed = Mathf.Abs(currentSpeed);

        if (currentSpeed > 0.01f)
        {
            follower.direction = Spline.Direction.Forward;
        }
        else if (currentSpeed < -0.01f)
        {
            follower.direction = Spline.Direction.Backward;
        }
    }
}
