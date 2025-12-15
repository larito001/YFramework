using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyRoundState : IYState, PoolItem<object>
{
    public static DataObjPool<EnemyRoundState, object> pool =
        new DataObjPool<EnemyRoundState, object>("EnemyRoundState", 200);
    private EnemyStateMachine _stateMachine;

    public string GetStateName()
    {
        return "EnemyRound";
    }

    
    private Vector3 center;
    public void EnterState(YStateMachine enemy)
    {
        _stateMachine = enemy as EnemyStateMachine;
        center= _stateMachine.Enemy.OrgPos;
        var target = center + new Vector3(Random.Range(-5, 5), 0, Random.Range(-5, 5));
        target.y = 0;
        _stateMachine.Enemy.OnPathComplete += OnPathComplete;
        _stateMachine.Enemy.OnEnterCallbackStateMachine += OnEnterCallback;
        _stateMachine.Enemy.OnHurtCallbackStateMachine += OnHurtCallback;
        _stateMachine.Enemy.seeker.OncePathFinding(target);

    }

    private void OnHurtCallback(IVictim target)
    {
        _stateMachine.Enemy.SetTarget(target);
        _stateMachine.SwitchState(EnemyPinState.pool.GetItem(null));
    }

    private void OnEnterCallback(IVictim target)
    {
        _stateMachine.Enemy.SetTarget(target);
        _stateMachine.SwitchState(EnemyPinState.pool.GetItem(null));
    }
    private void OnPathComplete()
    {
        _stateMachine.SwitchState(EnemyIdelState.pool.GetItem(null));
    }

    public void UpdateState(YStateMachine enemy,float dt)
    {
        if (_stateMachine == null) return;
    }

    public void ExitState(YStateMachine enemy)
    {
        _stateMachine.Enemy.OnPathComplete -= OnPathComplete;
        _stateMachine.Enemy.OnEnterCallbackStateMachine -= OnEnterCallback;
        _stateMachine.Enemy.OnHurtCallbackStateMachine -= OnHurtCallback;
        _stateMachine = null;
        pool.RecoverItem(this);
    }

    public void AfterIntoObjectPool()
    {
        //归零数据
    }

    public void SetData(object serverData)
    {
        //初始化数据
    }
}
