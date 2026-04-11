using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyPinState : IYState, PoolItem<object>
{
    public static DataObjPool<EnemyPinState, object> pool =
        new DataObjPool<EnemyPinState, object>("EnemyPinState", 200);

    private EnemyStateMachine _stateMachine;

    public string GetStateName()
    {
        return "EnemyPin";
    }

    public void EnterState(YStateMachine enemy,object param)
    {
        _stateMachine = enemy as EnemyStateMachine;
        _stateMachine.Enemy.OnPathComplete += OnPinPathComplete;
    }

    private void OnPinPathComplete()
    {
    }

    public void UpdateState(YStateMachine enemy, float dt)
    {
        if (_stateMachine == null) return;
        PathFind();
    }

    private void PathFind()
    {

        // if (target != null && target.GetProperties().State != RoleState.Dead)
        // {
        //     _stateMachine.Enemy.seeker.OncePathFinding(target.GetPosition());
        // }
        // else
        // {
        //     _stateMachine.SwitchState(EnemyIdelState.pool.GetItem(null),null);
        // }
    }

    public void ExitState(YStateMachine enemy)
    {
        _stateMachine.Enemy.OnPathComplete -= OnPinPathComplete;
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