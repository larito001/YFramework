using System.Collections;
using System.Collections.Generic;
using Pathfinding.Examples;
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
            if(stateMachine.GetCurrentStateName()=="EnemyIdel"||stateMachine.GetCurrentStateName()=="EnemyRound")
            TryExchangeTarget();
        }

        if (objTrans != null)
        {
            if (objTrans.position.y < -100)
            {
                EnemiesManager.instance.RemoveEnemy(this);
            }
        }
    
  
    }

    public override string GetModelLayer()
    {
        return "Agent";
    }

    protected override void AfterInstanceGObj()
    {

        OnPathComplete = null;
        seeker = PathFindingFactory.GetSeeker();
        var config = new AStarHighSeekerConfig(objTrans.gameObject);
        config.UseObstacleAvoidance = true;
        config.modifierType = ModifierType.FunnelModifier;
        config.speed = 5f;
        config.stopDistance =4f;
        config.isUpdate = true;
        config.enableGravity = true;
        config.OnPathComplete = OnPathCompleteCallback;
        var scale = Random.Range(1.5f, 3f);
         config.radio =0.5f*scale;
        seeker.Init(config);
        NeedRound = true;
        OrgPos = Location;
        stateMachine = new EnemyStateMachine();
        stateMachine.Init(this);
        stateMachine.SwitchState(EnemyIdelState.pool.GetItem(null));
            //todo:test
          var anim =  objTrans.GetComponentInChildren<MineBotAnimation>();
          anim.transform.localScale = Vector3.one * scale;
          anim.ai = seeker.GetSeeker();
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
        properties = null;
    }

    public void SetData(Vector3 serverData)
    {
        Location = serverData;
        SetInVision(true);
        SetPrefabBundlePath("Enemies/Enemy");

        properties = new Properties();
        properties.HP = 50;
        properties.OnDead = () =>
        {
            properties.State = RoleState.Dead;
            EnemiesManager.instance.RemoveEnemy(this);
        };
        properties.Camp = Camp.Enemy;
        properties.State = RoleState.Alive;
        InstanceGObj();
    }

    #endregion

    #region get

    public Transform GetTransform()
    {
        return objTrans;
    }

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


    /// <summary>
    /// 尝试索敌
    /// </summary>
    public void TryExchangeTarget()
    {
        //todo:获取索敌对象
        if (TowerManager.Instance.CheckTowerIsInRange(out TowerEntity tower,this.objTrans.position))
        {
            OnEnterCallbackStateMachine?.Invoke(tower);
            return;
        }
        
        if (PlayerManager.Instance.CheckPlayerIsInRange(objTrans.position, 20))
        {
            var victim = PlayerManager.Instance.playerEntity;
            OnEnterCallbackStateMachine?.Invoke(victim);
        }
        
    }
    public void Atk()
    {
        var pos = _lockTarget.GetPosition();
        BaseBulletEntity b = BaseBulletEntity.pool.GetItem(new BulletConfig()
        {
            name = "Bullet/bulletEnemy",
            moveSpeed = 0,
            attackType = AttackType.Remote,
            damage = 15,
            TrggerCount = 1,
            duration = 2,
            triggerTimer = 1f,
            camp = Camp.Enemy,
            removeCallback = OnBulletFinish,
        });
        // pos.y += Random.Range(0.5f, 2);
        b.Fire(this, pos, pos - ObjTrans.position);
    }

    public void OnHurt(IVictim fireRole, float hurt)
    {
        if (properties == null || properties.State == RoleState.Dead) return;
        FlyTextMgr.Instance.AddText(hurt.ToString(), objTrans.position);
        properties.HP -= hurt;
        fireRole.OnHurtSomeone();
        OnHurtCallbackStateMachine?.Invoke(fireRole);
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
    
    

    /// <summary>
    /// 子弹打到对方
    /// </summary>
    public void OnHurtSomeone()
    {
    }

    #endregion
}