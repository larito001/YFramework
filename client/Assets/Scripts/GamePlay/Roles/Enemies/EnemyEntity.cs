using System.Collections;
using System.Collections.Generic;
using Combat;
using Pathfinding.Examples;
using UnityEngine;
using UnityEngine.Events;
using YOTO;

public class EnemyEntity : ObjectBase, PoolItem<(EnemyData, Vector3)>,IDamageable,IThreatTarget,ITickable
{
    public static DataObjPool<EnemyEntity, (EnemyData, Vector3)> pool =
        new DataObjPool<EnemyEntity, (EnemyData, Vector3)>("EnemyEntity", 200);

    public IGotSeeker seeker;
    public bool NeedRound = false;
    public Vector3 OrgPos = Vector3.zero;
    private IThreatTarget _lockTarget = null;
    
    #region stateMachine

    public UnityAction<IThreatTarget> OnHurtCallbackStateMachine = null;
    public UnityAction<IThreatTarget> OnEnterCallbackStateMachine = null;
    public UnityAction OnAtkFinishCallbackStateMachine = null;
    public UnityAction OnPathComplete;
    private EnemyStateMachine stateMachine;

    #endregion


    #region 生命周期

    public void Tick(float dt)
    {
        if (stateMachine != null)
        {
            stateMachine.Update(dt);
            if (stateMachine.GetCurrentStateName() == "EnemyIdel" || stateMachine.GetCurrentStateName() == "EnemyRound")
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


    protected override void AfterInstanceGObj()
    {
        OnPathComplete = null;
        seeker = PathFindingFactory.GetSeeker();
        var config = new AStarHighSeekerConfig(objTrans.gameObject);
        config.UseObstacleAvoidance = true;
        config.modifierType = ModifierType.FunnelModifier;
        config.speed = enemyConfig.moveSpeed;
        config.stopDistance = enemyConfig.atkRange;
        config.isUpdate = true;
        config.enableGravity = true;
        config.OnPathComplete = OnPathCompleteCallback;
        var anim = objTrans.GetComponentInChildren<MineBotAnimation>();
        config.radio = 0.5f * ChangeScale(anim.transform);
        seeker.Init(config);
        anim.ai = seeker.GetSeeker();
        NeedRound = true;
        OrgPos = Location;
        stateMachine = new EnemyStateMachine();
        stateMachine.Init(this);
        stateMachine.SwitchState(EnemyIdelState.pool.GetItem(null),null);

        ChangeAllChildrenColor(objTrans);
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

    public EnemyData enemyConfig;

    public void SetData((EnemyData, Vector3) serverData)
    {
        enemyConfig = serverData.Item1;
        Location = serverData.Item2;
        SetInVision(true);
        SetPrefabBundlePath("Enemies/Enemy");
        InstanceGObj();
    }

    void ChangeAllChildrenColor(Transform parent)
    {
        // 遍历当前父对象下的所有子对象
        foreach (Transform child in parent)
        {
            // 获取子对象的MeshRenderer组件
            MeshRenderer renderer = child.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                if (enemyConfig.enemyType == EnemyType.Normal)
                {
                    renderer.material.color = Color.black;
                }
                else if (enemyConfig.enemyType == EnemyType.Speed)
                {
                    renderer.material.color = Color.magenta;
                }
                else if (enemyConfig.enemyType == EnemyType.Far)
                {
                    renderer.material.color = Color.blue;
                }
                else if (enemyConfig.enemyType == EnemyType.Summon)
                {
                    renderer.material.color = Color.yellow;
                }
                else if (enemyConfig.enemyType == EnemyType.Boss)
                {
                    // 设置材质颜色为红色
                    renderer.material.color = Color.red;
                }
            }

            // 递归处理子对象的子对象（孙对象）
            if (child.childCount > 0)
            {
                ChangeAllChildrenColor(child);
            }
        }
    }

    private float ChangeScale(Transform trans)
    {
        float scale = 1.5f;
        if (enemyConfig.enemyType == EnemyType.Normal)
        {
            scale = 1.5f;
        }
        else if (enemyConfig.enemyType == EnemyType.Speed)
        {
            scale = 0.8f;
        }
        else if (enemyConfig.enemyType == EnemyType.Far)
        {
            scale = 1.5f;
        }
        else if (enemyConfig.enemyType == EnemyType.Summon)
        {
            scale = 2.5f;
        }
        else if (enemyConfig.enemyType == EnemyType.Boss)
        {
            scale = 4;
        }

        trans.localScale = Vector3.one * scale;

        return scale;
    }

    #endregion

    #region get

    public Transform GetTransform()
    {
        return objTrans;
    }
    

    public IThreatTarget GetTarget()
    {
        return _lockTarget;
    }

    public void SetTarget(IThreatTarget target)
    {
        _lockTarget = target;
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
        if (objTrans != null)
        {
            return objTrans.forward;
        }

        return Vector3.down;
    }

    #endregion

    #region 状态转换

    /// <summary>
    /// 尝试索敌
    /// </summary>
    public void TryExchangeTarget()
    {
        if (objTrans != null)
        {
            // if (PlayerManager.Instance.CheckPlayerIsInRange(objTrans.position, enemyConfig.indexRange))
            // {
            //     var victim = PlayerManager.Instance.playerEntity;
            //     OnEnterCallbackStateMachine?.Invoke(victim);
            //     return;
            // }
            //
            //
            // //todo:获取索敌对象
            // if (TowerManager.Instance.CheckTowerIsInRange(out TowerEntity tower, this.objTrans.position,
            //         enemyConfig.indexRange))
            // {
            //     // OnEnterCallbackStateMachine?.Invoke(tower);
            //     return;
            // }
            //
            //
            // if (TrainManager.Instance.CheckTrainIsInRange(objTrans.position, enemyConfig.indexRange))
            // {
            //     
            //     return;
            // }
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
        //如果距离到达
        OnPathComplete?.Invoke();
    }


    /// <summary>
    /// 子弹打到对方
    /// </summary>
    public void OnHurtSomeone()
    {
    }
    IEnumerator slowDownIE=null;

    IEnumerator slowDownFunIE(float rate)
    {
        float timer = 3;
        if (seeker != null)
        {
            seeker.SetSpeed(enemyConfig.moveSpeed*rate); 
        }

        float time = 0;
        while (true)
        {
            yield return new WaitForSeconds(0.1f);
            time+=0.1f;

            if (time>=3)
            {
                if (seeker != null)
                {
                    seeker.SetSpeed(enemyConfig.moveSpeed);
                }
                break;
            }
        }
    }
    public void OnSlowDown(float rate)
    {
        if (slowDownIE != null)
        {
            GameLoop.Instance.StopCoroutine(slowDownIE);
            slowDownIE = null;
        }
        slowDownIE = slowDownFunIE(rate);
        GameLoop.Instance.StartCoroutine(slowDownIE);
    }

    #endregion
    
    #region 属性

    private TeamId _team = new TeamId(0);
    private bool _isTargetable = false;
    private bool _isAlive = false;
    private float _maxHP=100;
    private float _hp=100;
    private float _atk=10;
    private float _def=10;
    private int _level=1;
    
    public TeamId Team
    {
        get { return _team; }
    }
    public bool IsTargetable { get { return _isTargetable; } }
    public bool IsAlive { get{ return _isAlive;} }

    public Vector3 Position
    {
        get
        {
            if (ObjTrans != null)
            {
                return ObjTrans.position;
            }
            else
            {
                return Location;
            }
        }
    }

    public Vector3 AimPoint
    {
        get;
    }

    public float MaxHP
    {
        get { return _maxHP; }
    }
    public float Hp { get{ return _hp;} }
    
    public float Atk { get{ return _atk;} }
    public float Def { get{ return _def;} }
    public int Level { get{ return _level;} }

    public bool ApplyDamage(in DamageSpec spec, in HitInfo hit, IProjectile instigator)
    {
        return true;
    }

    #endregion
    

   
}