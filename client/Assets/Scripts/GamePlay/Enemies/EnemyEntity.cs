using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class EnemyEntity : ObjectBase, PoolItem<Vector3>, IVictim
{
    public static DataObjPool<EnemyEntity, Vector3> pool =
        new DataObjPool<EnemyEntity, Vector3>("EnemyEntity", 200);

    public Properties Properties;
    public IGotSeeker seeker;
    private EnemyStateMachine stateMachine;
    public bool NeedRound = false;
    public Vector3 OrgPos = Vector3.zero;

    protected override void YOTOOnload()
    {
    }

    public override void YOTOStart()
    {
    }

    
    private UnityAction atkCallback = null;
    private bool isCD = false;
    private float cdTimer = 2;
    public void Atk(UnityAction callback)
    {
        isCD = true;
        cdTimer = 2;
        atkCallback = callback;
        var pos = EnemiesManager.instance.GetPlayerPos();
        BaseBulletEntity b = BaseBulletEntity.pool.GetItem(new BulletConfig()
        {
            name = "Bullet/bullet",
            moveSpeed = 20,
            attackType = AttackType.Remote,
            damage = 50,
            TrggerCount = 1,
            duration = 10,
            triggerTimer = 0f,
            camp = Camp.Enemy
        });
        pos.y += Random.Range(0.5f, 2);
        b.Fire(ObjTrans.position, pos - ObjTrans.position);
        
    }


    public override void YOTOUpdate(float deltaTime)
    {
        if (isCD)
        {
            cdTimer-= deltaTime;
            if (cdTimer <= 0)
            {
                cdTimer = 2;
                isCD = false;
                atkCallback?.Invoke();
            }
        }
        if (seeker != null)
        {
            CheckDistance();
        }

        if (stateMachine != null)
        {
            stateMachine.Update(deltaTime);
        }
    }

    private void CheckDistance()
    {
        // var dis = objTrans.position - EnemiesManager.instance.GetPlayerPos();
        // if (dis.magnitude > 100)
        // {
        //     EnemiesManager.instance.RemoveEnemy(this);
        // }
    }

    public override void YOTONetUpdate()
    {
    }

    public override void YOTOFixedUpdate(float deltaTime)
    {
    }

    public override void YOTOOnHide()
    {
    }

    public void SetTarget()
    {
    }

    protected override void AfterInstanceGObj()
    {
        if (!ObjTrans.gameObject.TryGetComponent<TheVictim>(out TheVictim victim))
        {
            victim = ObjTrans.gameObject.AddComponent<TheVictim>();
        }

        OnPathComplete = null;
        seeker = PathFindingFactory.GetSeeker();
        var config = new AStarMidSeekerConfig(objTrans.gameObject);
        config.UseObstacleAvoidance = true;
        config.modifierType = ModifierType.FunnelModifier;
        config.speed = 2f;
        config.constrainInsideGraph = true;
        config.stopDistance =1.5f;
        config.slowDownDistance = 0;
        config.isUpdate = true;
        config.OnPathComplete = OnPathCompleteCallback;
        seeker.Init(config);
        victim.Init(new Vector3(5, 1, 5), this);
        NeedRound = true;
        isCD = false;
        OrgPos =Location;
        stateMachine = new EnemyStateMachine();
        stateMachine.Init(this);
        stateMachine.SwitchState(EnemyIdelState.pool.GetItem(null));
    }

    public UnityAction OnPathComplete;
    private void OnPathCompleteCallback()
    {
        OnPathComplete?.Invoke();
    }
    
    protected override void BeforeRecover(bool isDelete)
    {
        isCD = false;
        OnPathComplete = null;
        stateMachine = null;
        seeker.Remove();
        seeker = null;
    }

    public void AfterIntoObjectPool()
    {
        RecoverObject();
    }

    public void SetData(Vector3 serverData)
    {
        Location = serverData;
        SetInVision(true);
        SetPrefabBundlePath("Enemies/Enemy");
        InstanceGObj();
        Properties = new Properties();
        Properties.HP = 100;
        Properties.OnDead = () => { EnemiesManager.instance.RemoveEnemy(this); };
        Properties.Camp = Camp.Enemy;
    }

    public Properties GetProperties()
    {
        return Properties;
    }

    public void OnHurt(float hurt)
    {
        FlyTextMgr.Instance.AddText(hurt.ToString(), objTrans.position);
        if (stateMachine.GetCurrentStateName() == "EnemyIdel" || stateMachine.GetCurrentStateName() == "EnemyRound")
        {
            stateMachine.SwitchState(EnemyPinState.pool.GetItem(null));
        }

        Properties.HP -= hurt;
    }

    public void OnEnter(Collider other)
    {
        if (other.TryGetComponent(out TheVictim victim))
        {
            if (victim.Victim.GetProperties().Camp == Camp.Player)
            {
                if(stateMachine.GetCurrentStateName() =="EnemyIdel"|| stateMachine.GetCurrentStateName() == "EnemyRound")
                stateMachine.SwitchState(EnemyPinState.pool.GetItem(null));
            }
        }
    }

    public void OnExit(Collider other)
    {
    }
}