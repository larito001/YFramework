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

    public void EnterState(YStateMachine enemy)
    {
        _stateMachine = enemy as EnemyStateMachine;
        _stateMachine.Enemy.OnPathCompleteAction+=OnPathComplete;
    }

    private void OnPathComplete()
    {
        //todo:如果没有moving且到达索敌半径
        if (true)
        {
            _stateMachine.SwitchState(EnemyAtkState.pool.GetItem(null));
        }
    }

    public void UpdateState(YStateMachine enemy, float dt)
    {
        if (_stateMachine == null) return;
        _stateMachine.Enemy.seeker.OncePathFinding(EnemiesManager.instance.GetPlayerPos());
    }

    public void ExitState(YStateMachine enemy)
    {
        _stateMachine.Enemy.OnPathCompleteAction -= OnPathComplete;
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
