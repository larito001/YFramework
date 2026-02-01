using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Dreamteck.Splines;
using UnityEngine;
using YOTO;

public class TrainEntity : ObjectBase, IVictim
{
    GatlingEntity gatling;
    public Properties properties;
    public float acceleration = 3f; // 加速曲线（越大加速越猛）
    public float deceleration = -30f; // 减速曲线
    public float maxSpeed = 5f; // 最大速度（正反通用）
    public SplineFollower follower;
    private float currentSpeed = 0f; // 当前速度
    private SplineComputer spline;
    public bool canMove = false;
    public List<TrainWagonEntity> positioners = new List<TrainWagonEntity>();
    // private GunEntity gun;

    public void TrainInit()
    {
        properties = new Properties();
        properties.State = RoleState.Alive;
        properties.Camp = Camp.Player;
        properties.HP = 3000;
        properties.MaxHP = 3000;
        properties.OnDead = () =>
        {
            //todo:游戏结束
            properties.State = RoleState.Dead;
            YFramework.uIMgr.Show(UIEnum.FinishPanel);
        };
        properties.Camp = Camp.Player;
        SetInVision(true);
        SetPrefabBundlePath("Train/Engine");
        InstanceGObj();
    }

    public override string GetModelLayer()
    {
        return "Agent";
    }
    
    protected override void AfterInstanceGObj()
    {
        follower = ObjTrans.GetComponent<SplineFollower>();
        var spline = GameObject.Find("Spline").GetComponent<SplineComputer>();
        
        
        {
            var wagon = new TrainWagonEntity();
            wagon.TrainInit();
            wagon.SetFollowTarget(14, follower);
            positioners.Add(wagon);
        }
        //最后配置spline
        SetTracer(spline);
        YFramework.eventMgr.TriggerEvent(YOTOEventType.RefreshTrainHP);
        TowerManager.Instance.GenerateTowerBaseAtTransform(ObjTrans, new Vector3(-2.63f, 4.5f, -0.15f));
        TowerManager.Instance.GenerateTowerBaseAtTransform(ObjTrans, new Vector3(-2.63f, 4.5f, -3.5f));
        TowerManager.Instance.GenerateTowerBaseAtTransform(ObjTrans, new Vector3(2.8f, 4.5f, -3.5f));
        TowerManager.Instance.GenerateTowerBaseAtTransform(ObjTrans, new Vector3(2.8f, 4.5f, -0.15f));
        gatling = new GatlingEntity();
        gatling.Parent = ObjTrans;
        gatling.Location =  new Vector3(0f, 5f, 2f);
        gatling.GatlingInit();
    }

    public void SetTracer(SplineComputer spline)
    {
        this.spline = spline;
        follower.spline = spline;
        for (var i = 0; i < positioners.Count; i++)
        {
            positioners[i].SetTracer(spline);
        }
    }

    public override void YOTOFixedUpdate(float deltaTime)
    {
        base.YOTOFixedUpdate(deltaTime);
        if (ObjTrans)
        {
            HandleInput();
        }
    }
    

    void HandleInput()
    {
        // 1. 根据输入设定目标速度
        float targetSpeed = 0f;

        if (canMove && Input.GetKey(KeyCode.W))
        {
            targetSpeed = maxSpeed; // 前进
        }
        else if (canMove && Input.GetKey(KeyCode.S))
        {
            targetSpeed = -maxSpeed; // 后退
        }
        else
        {
            targetSpeed = 0f; // 刹车
        }

        var percent = follower.GetPercent();
        foreach (var keyValuePair in TrainManager.Instance.trackFixDic)
        {
            if (percent >= keyValuePair.Key - 0.05)
            {
                if (!keyValuePair.Value && currentSpeed > 0)
                {
                    targetSpeed = 0;
                }
            }
        }

        // 2. 选择加速或减速速率（必须为正数）
        float rate = (Mathf.Abs(targetSpeed) < 0.01f)
            ? Mathf.Abs(deceleration)
            : Mathf.Abs(acceleration);

        // 3. 平滑改变当前速度
        currentSpeed = Mathf.MoveTowards(
            currentSpeed,
            targetSpeed,
            rate * Time.deltaTime
        );

        // 4. 更新 SplineFollower
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

    protected override void BeforeRecover(bool isDelete)
    {
        // gun.RecoverObject();
    }

    public Properties GetProperties()
    {
        return properties;
    }

    public void OnHurt(IVictim fireRole, float hurt)
    {
        if (properties == null || properties.State == RoleState.Dead || objTrans == null) return;

        // FlyTextMgr.Instance.AddText(hurt.ToString(), objTrans.position, FlyTextType.PlayerHurt);
        properties.HP -= hurt;
        fireRole.OnHurtSomeone();
        YFramework.eventMgr.TriggerEvent(YOTOEventType.RefreshTrainHP);
    }

    public Vector3 GetPosition()
    {
        return ObjTrans.position;
    }

    List<Vector3> atkSlot = new List<Vector3>();

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
        if (ObjTrans != null)
        {
            gatling.OnShoot(this, hitPoint, dt);  
        }
    }

    public void OnMouseUp()
    {
        if (ObjTrans != null)
        {
            gatling.OnEndShoot();  
        }

    }
}