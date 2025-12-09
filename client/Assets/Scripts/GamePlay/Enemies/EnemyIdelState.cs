using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyIdelState : IYState, PoolItem<object>
{
    public static DataObjPool<EnemyIdelState, object> pool =
        new DataObjPool<EnemyIdelState, object>("EnemyIdelState", 200);

    private EnemyStateMachine _stateMachine;

    public string GetStateName()
    {
        return "EnemyIdel";
    }
    
    private float timer = 0;
    public void EnterState(YStateMachine enemy)
    {
        _stateMachine = enemy as EnemyStateMachine;
        _stateMachine.Enemy.seeker.StopPathFinding();
    }

    public void UpdateState(YStateMachine enemy,float dt)
    {
        if (_stateMachine == null) return;
        if (_stateMachine.Enemy.NeedRound)
        {
            timer -= dt;
            if (timer <= 0)
            {
                timer = Random.Range(1, 3);
                _stateMachine.SwitchState(EnemyRoundState.pool.GetItem(null));
            }
        }
     
    }

    public void ExitState(YStateMachine enemy)
    {
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