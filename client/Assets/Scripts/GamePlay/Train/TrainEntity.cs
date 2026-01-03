using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Dreamteck.Splines;
using UnityEngine;
using YOTO;

public class TrainEntity : ObjectBase, IVictim
{
    public Properties properties;
    public float acceleration = 10f; // 加速曲线（越大加速越猛）
    public float deceleration = -15f; // 减速曲线
    public float maxSpeed = 15f; // 最大速度（正反通用）
    public SplineFollower follower;
    private float currentSpeed = 0f; // 当前速度
    private SplineComputer spline;
    public bool canMove = false;
    public List<TrainWagonEntity> positioners = new List<TrainWagonEntity>();
    private GunEntity gun;

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
            properties.State =  RoleState.Dead;
            FlyTextMgr.Instance.AddTextAtScreenCenter("你输了");
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
        var wagon = new TrainWagonEntity();
        wagon.TrainInit();
        wagon.SetFollowTarget(14, follower);
        positioners.Add(wagon);
        var wagon2 = new TrainWagonEntity();
        wagon2.TrainInit();
        wagon2.SetFollowTarget(26, follower);
         positioners.Add(wagon2);
        SetTracer(spline);
        gun = new GunEntity();
        gun.InitGun(ObjTrans,0.3f, 0.2f);
        YFramework.eventMgr.TriggerEvent(YOTOEventType.RefreshTrainHP);
        
        
        TowerManager.Instance.GenerateTowerBaseAtTransform(ObjTrans, new Vector3(-2.63f, 4.5f, -0.15f));
        TowerManager.Instance.GenerateTowerBaseAtTransform(ObjTrans, new Vector3(-2.63f, 4.5f, -3.5f));
        TowerManager.Instance.GenerateTowerBaseAtTransform(ObjTrans, new Vector3(2.8f,4.5f, -3.5f));
        TowerManager.Instance.GenerateTowerBaseAtTransform(ObjTrans, new Vector3(2.8f, 4.5f, -0.15f));
        
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

    List<EnemyEntity> enemies = new List<EnemyEntity>();

    public override void YOTOFixedUpdate(float deltaTime)
    {
        base.YOTOFixedUpdate(deltaTime);
        if (ObjTrans)
        {
            HandleInput();
            enemies.Clear();
            if (EnemiesManager.instance.GetEnemyIsInRange(objTrans.position, 20, enemies))
            {
                var lockTarget = GetNearestEnemyPos();
                gun.OnShoot(this, lockTarget.GetPosition(), deltaTime);
            }
        }
    }

    private IVictim GetNearestEnemyPos()
    {
        return enemies.OrderBy(x => Vector3.Distance(x.GetPosition(), ObjTrans.position)).FirstOrDefault();
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
        foreach (var keyValuePair in TowerManager.Instance.trackFixDic)
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
        gun.RecoverObject();
    }

    public Properties GetProperties()
    {
        return properties;
    }

    public void OnHurt(IVictim fireRole, float hurt)
    {
        if (properties == null || properties.State == RoleState.Dead || objTrans == null) return;

        FlyTextMgr.Instance.AddText(hurt.ToString(), objTrans.position, FlyTextType.PlayerHurt);
        properties.HP -= hurt;
        fireRole.OnHurtSomeone();
        YFramework.eventMgr.TriggerEvent(YOTOEventType.RefreshTrainHP);
    }

    public Vector3 GetPosition()
    {
        return ObjTrans.position;
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
}