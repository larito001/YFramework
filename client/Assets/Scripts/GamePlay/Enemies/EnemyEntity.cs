using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class EnemyEntity : ObjectBase, PoolItem<object>, IVictim
{
    public static DataObjPool<EnemyEntity, object> pool =
        new DataObjPool<EnemyEntity, object>("EnemyEntity", 200);

    public Properties Properties;
    public IGotSeeker seeker;
    private EnemyStateMachine stateMachine;
    public bool NeedRound = false;
    protected override void YOTOOnload()
    {
    }

    public override void YOTOStart()
    {
    }

    public void Atk(UnityAction callback)
    {
        callback?.Invoke();
    }
    
    
    public override void YOTOUpdate(float deltaTime)
    {
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
        if (seeker != null)
        {
            if (!seeker.GetIsMoving())
            {
                
                
            }
        }
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

        seeker = PathFindingFactory.GetSeeker();
        var config = new AStarMidSeekerConfig(objTrans.gameObject);
        config.UseObstacleAvoidance=true;
        config.modifierType = ModifierType.FunnelModifier;
        config.speed = 2f;
        config.constrainInsideGraph = true;
        config.stopDistance = 1;
        config.OnPathComplete += OnPathComplete;
        seeker.Init(config);
        victim.Init(new Vector3(5,1,5),this);
        NeedRound=true;
        stateMachine = new EnemyStateMachine();
        stateMachine.Init(this);
        stateMachine.SwitchState(EnemyIdelState.pool.GetItem(null));
    }

    public UnityAction OnPathCompleteAction ;
    private void OnPathComplete()
    {
        OnPathCompleteAction?.Invoke();
    }

    protected override void BeforeRecover(bool isDelete)
    {
        stateMachine = null;
        seeker.Remove();
        seeker = null;
    }

    public void AfterIntoObjectPool()
    {
        RecoverObject();
    
    }

    public void SetData(object serverData)
    {
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
                stateMachine.SwitchState(EnemyPinState.pool.GetItem(null));
            }
        }
    }

    public void OnExit(Collider other)
    {
    }
}