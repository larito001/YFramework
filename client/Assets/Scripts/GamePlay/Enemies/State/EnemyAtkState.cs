using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAtkState : IYState, PoolItem<object>
{
    public static DataObjPool<EnemyAtkState, object> pool =
        new DataObjPool<EnemyAtkState, object>("EnemyAtkState", 200);

    private EnemyStateMachine _stateMachine;

    public string GetStateName()
    {
        return "EnemyAtk";
    }

    public void EnterState(YStateMachine enemy)
    {
        _stateMachine = enemy as EnemyStateMachine;
        _stateMachine.Enemy.OnAtkFinishCallbackStateMachine += OnAtkCallback;
        _stateMachine.Enemy.Atk();
    }

    private void OnAtkCallback()
    {
        
        Timers.inst.Add(1/_stateMachine.Enemy.enemyConfig.atkSpeed, (o) =>
        {
            _stateMachine.SwitchState(EnemyIdelState.pool.GetItem(null)); 
        });
    
    }

    public void UpdateState(YStateMachine enemy, float dt)
    {
        if (_stateMachine == null) return;
    }

    public void ExitState(YStateMachine enemy)
    {
        _stateMachine.Enemy.OnAtkFinishCallbackStateMachine -= OnAtkCallback;
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
