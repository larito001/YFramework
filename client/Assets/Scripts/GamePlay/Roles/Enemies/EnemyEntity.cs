using System.Collections;
using System.Collections.Generic;
using Pathfinding.Examples;
using UnityEngine;
using UnityEngine.Events;
using YOTO;

public class EnemyEntity : ObjectBase, PoolItem<(EnemyData, Vector3)>,ITickable
{
    public static DataObjPool<EnemyEntity, (EnemyData, Vector3)> pool =
        new DataObjPool<EnemyEntity, (EnemyData, Vector3)>("EnemyEntity", 200);

    private ICoroutineRunner coroutineRunner;
    private System.Action<EnemyEntity> removeEnemyAction;
    private YAStarManager pathFindingManager;

    public IYSeeker seeker;
    public bool NeedRound = false;
    public Vector3 OrgPos = Vector3.zero;
 
    
    #region stateMachine

    public UnityAction OnAtkFinishCallbackStateMachine = null;
    public UnityAction OnPathComplete;
    private EnemyStateMachine stateMachine;

    #endregion


    #region Lifecycle

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
                removeEnemyAction?.Invoke(this);
            }
        }
    }


    protected override void AfterInstanceGObj()
    {
        OnPathComplete = null;
        seeker = pathFindingManager?.CreateSeeker(coroutineRunner);
        if (seeker == null)
        {
            Debug.LogError("Pathfinding seeker is not available.");
            return;
        }
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

    public void ConfigureRuntime(ICoroutineRunner runner, System.Action<EnemyEntity> removeEnemy, YAStarManager manager)
    {
        coroutineRunner = runner;
        removeEnemyAction = removeEnemy;
        pathFindingManager = manager;
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
        foreach (Transform child in parent)
        {
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
                    renderer.material.color = Color.red;
                }
            }

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

    #region Combat Hooks

    /// <summary>
    /// Attempts to refresh the current combat target.
    /// </summary>
    public void TryExchangeTarget()
    {
        if (objTrans == null)
        {
            return;
        }
    }

    /// <summary>
    /// 灏勫嚭鐨勫瓙寮硅閿€姣佸悗璋冪敤
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
    /// Called when this entity successfully damages another target.
    /// </summary>
    public void OnHurtSomeone()
    {
    }
    private Coroutine slowDownCoroutine;

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
                slowDownCoroutine = null;
                break;
            }
        }
    }
    public void OnSlowDown(float rate)
    {
        if (slowDownCoroutine != null)
        {
            coroutineRunner.Stop(slowDownCoroutine);
            slowDownCoroutine = null;
        }

        slowDownCoroutine = coroutineRunner.Run(slowDownFunIE(rate));
    }

    #endregion
    
    #region Damageable State

  
    private bool _isTargetable = false;
    private bool _isAlive = false;
    private float _maxHP=100;
    private float _hp=100;
    private float _atk=10;
    private float _def=10;
    private int _level=1;
    

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



    #endregion
    

   
}

