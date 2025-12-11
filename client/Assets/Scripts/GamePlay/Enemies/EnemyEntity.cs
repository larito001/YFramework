using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class EnemyEntity : ObjectBase, PoolItem<Vector3>, IVictim
{
    public static DataObjPool<EnemyEntity, Vector3> pool =
        new DataObjPool<EnemyEntity, Vector3>("EnemyEntity", 200);

    #region stateMachine

    public UnityAction<IVictim> OnHurtCallbackStateMachine = null;
    public UnityAction<IVictim> OnEnterCallbackStateMachine = null;
    public UnityAction OnAtkFinishCallbackStateMachine = null;
    public UnityAction OnPathComplete;
    private EnemyStateMachine stateMachine;

    #endregion

    #region 属性

    List<IVictim> victims = new List<IVictim>();
    public Properties properties;
    public IGotSeeker seeker;
    public bool NeedRound = false;
    public Vector3 OrgPos = Vector3.zero;
    private IVictim _lockTarget = null;
    

    #endregion


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

    #region get

    public int GetId()
    {
        return _entityID;
    }

    public IVictim GetTarget()
    {
        return _lockTarget;
    }

    public void SetTarget(IVictim  target)
    {
        _lockTarget = target;
    }

    public Properties GetProperties()
    {
        return properties;
    }

    public Vector3 GetPosition()
    {
        if (objTrans != null)
        {
            return objTrans.position;
        }

        return Location;
    }

    #endregion

    #region 状态转换

    private void CheckVictimesIsAlive()
    {
        List<IVictim> removeList = new List<IVictim>();
        if (victims.Count > 0)
        {
            foreach (var victim in victims)
            {
                if (victim.GetProperties().State == RoleState.Dead)
                {
                    removeList.Add(victim);
                }
            }

            foreach (var victim in removeList)
            {
                victims.Remove(victim);
            }
        }
    }
    /// <summary>
    /// 尝试索敌
    /// </summary>
    public void TryExchangeTarget()
    {
        CheckVictimesIsAlive();
        if (victims.Count > 0)
        {
            foreach (var victim in victims)
            {
                if (victim.GetProperties().State != RoleState.Dead)
                {
                    OnEnterCallbackStateMachine?.Invoke( victim);
                    return;
                }
            }
        }


    }
    public void Atk()
    {
        var pos = _lockTarget.GetPosition();
        BaseBulletEntity b = BaseBulletEntity.pool.GetItem(new BulletConfig()
        {
            name = "Bullet/bullet",
            moveSpeed = 20,
            attackType = AttackType.Remote,
            damage = 5,
            TrggerCount = 1,
            duration = 1,
            triggerTimer = 0f,
            camp = Camp.Enemy,
            removeCallback = OnBulletFinish
        });
        // pos.y += Random.Range(0.5f, 2);
        b.Fire(this, ObjTrans.position, pos - ObjTrans.position);
    }

    public void OnHurt(IVictim fireRole, float hurt)
    {
        if (properties == null || properties.State == RoleState.Dead) return;
        FlyTextMgr.Instance.AddText(hurt.ToString(), objTrans.position);
        properties.HP -= hurt;
        fireRole.OnHurtSomeone();
        OnHurtCallbackStateMachine?.Invoke(fireRole);
    }

    public void OnEnter(Collider other)
    {
        if (other.TryGetComponent(out TheVictim victim))
        {
            if (victim.Victim.GetProperties().Camp == Camp.Player)
            {
                if (!victims.Contains(victim.Victim))
                {
                    victims.Add(victim.Victim);
                    OnEnterCallbackStateMachine?.Invoke(victim.Victim);
                }
            }
        }
    }

    /// <summary>
    /// 射出的子弹被销毁后调用
    /// </summary>
    public void OnBulletFinish()
    {
        OnAtkFinishCallbackStateMachine?.Invoke();
    }

    private void OnPathCompleteCallback()
    {
        OnPathComplete?.Invoke();
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


    /// <summary>
    /// 子弹打到对方
    /// </summary>
    public void OnHurtSomeone()
    {
    }

    #endregion
}