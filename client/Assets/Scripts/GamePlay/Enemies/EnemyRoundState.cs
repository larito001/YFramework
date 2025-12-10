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
        _stateMachine.Enemy.OnPathComplete += OnPathComplete;
        _stateMachine.Enemy.seeker.OncePathFinding(target);

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
