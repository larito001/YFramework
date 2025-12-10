using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class EnemyEntity : ObjectBase, PoolItem<Vector3>, IVictim
{
    public static DataObjPool<EnemyEntity, Vector3> pool =
        new DataObjPool<EnemyEntity, Vector3>("EnemyEntity", 200);

    public Properties properties;
    public IGotSeeker seeker;
    private EnemyStateMachine stateMachine;
    public bool NeedRound = false;
    public Vector3 OrgPos = Vector3.zero;

    private IVictim _lockTarget = null;

    private IVictim LockTarget
    {
        get { return _lockTarget; }
        set
        {
            _lockTarget = value;
      
        }
    }

    private UnityAction atkCallback = null;
    public UnityAction OnPathComplete;

    #region 生命周期

    public override void YOTOUpdate(float deltaTime)
    {

        if (stateMachine != null)
        {
            stateMachine.Update(deltaTime);
        }
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
        config.speed = 5f;
        config.constrainInsideGraph = true;
        config.stopDistance = 3f;
        config.slowDownDistance = 0;
        config.isUpdate = true;
        config.OnPathComplete = OnPathCompleteCallback;
        seeker.Init(config);
        victim.Init(new Vector3(5, 1, 5), this);
        NeedRound = true;
        OrgPos = Location;
        stateMachine = new EnemyStateMachine();
        stateMachine.Init(this);
        stateMachine.SwitchState(EnemyIdelState.pool.GetItem(null));
    }

    protected override void BeforeRecover(bool isDelete)
    {
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
        properties = new Properties();
        properties.HP = 100;
        properties.OnDead = () => { EnemiesManager.instance.RemoveEnemy(this); };
        properties.Camp = Camp.Enemy;
        properties.State = RoleState.Alive;
    }

    #endregion

    #region victim

    public Properties GetProperties()
    {
        return properties;
    }

    public void OnHurt(IVictim fireRole,float hurt)
    {
        FlyTextMgr.Instance.AddText(hurt.ToString(), objTrans.position);
        LockTarget = fireRole;
        StartPin();
        properties.HP -= hurt;
        fireRole.OnHurtSomeone();
    }

    private void StartPin()
    {
        if (_lockTarget != null)
        {
            if (stateMachine.GetCurrentStateName() == "EnemyIdel" || stateMachine.GetCurrentStateName() == "EnemyRound")
                stateMachine.SwitchState(EnemyPinState.pool.GetItem(null));
        }
        else
        {
            stateMachine.SwitchState(EnemyIdelState.pool.GetItem(null));
        }
    }
    List<IVictim> victims = new List<IVictim>();
    public void OnEnter(Collider other)
    {
        if (other.TryGetComponent(out TheVictim victim))
        {
            if (victim.Victim.GetProperties().Camp == Camp.Player)
            {
                if (!victims.Contains(victim.Victim))
                {
                    victims.Add(victim.Victim);
                    if (LockTarget == null)
                    {
                        LockTarget = victim.Victim;
                        StartPin();
                    }
                }
            
            }
        }
    }

    public void OnExit(Collider other)
    {
        if (other.TryGetComponent(out TheVictim victim))
        {
            if (victim.Victim.GetProperties().Camp == Camp.Player)
            {
                if (victims.Contains(victim.Victim))
                {
                    victims.Remove(victim.Victim); 
                }
           
            }
        }
    }

    public Vector3 GetPosition()
    {
        if (objTrans != null)
        {
            return objTrans.position;
        }

        return Location;
    }

    public void OnHurtSomeone()
    {
        
    }

    public void OnBulletEnd()
    {
      
        //攻击完毕，检查是否有目标
        var targetState = LockTarget.GetProperties().State;
        if (targetState == RoleState.Dead)
        {
            victims.Remove(LockTarget);
            LockTarget = null;
            for (var i = 0; i < victims.Count; i++)
            {
                if (victims[i].GetProperties().State != RoleState.Dead)
                {
                    LockTarget = victims[i];
               
                    break;
                }
            }

            StartPin();
        }
        else
        {
            atkCallback?.Invoke();
        }
    }

    #endregion

    public void Atk(UnityAction callback)
    {
        atkCallback = callback;
        var pos = _lockTarget.GetPosition();
        BaseBulletEntity b = BaseBulletEntity.pool.GetItem(new BulletConfig()
        {
            name = "Bullet/bullet",
            moveSpeed = 20,
            attackType = AttackType.Remote,
            damage = 50,
            TrggerCount = 1,
            duration = 1,
            triggerTimer = 0f,
            camp = Camp.Enemy,
            removeCallback = OnBulletEnd
        });
        // pos.y += Random.Range(0.5f, 2);
        b.Fire(this,ObjTrans.position, pos - ObjTrans.position);
    }

    private void OnPathCompleteCallback()
    {
        OnPathComplete?.Invoke();
    }

    public IVictim GetTarget()
    {
        return LockTarget;
    }
}