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
        //todo:如果没有moving且到达索敌半径
        var target = _stateMachine.Enemy.GetTarget();
        if (target != null)
        {
            if ((target.GetPosition() - _stateMachine.Enemy.GetPosition()).magnitude <= _stateMachine.Enemy.enemyConfig.atkRange+2)
            {
                _stateMachine.SwitchState(EnemyAtkState.pool.GetItem(null),null);
            }
        }
    }

    public void UpdateState(YStateMachine enemy, float dt)
    {
        if (_stateMachine == null) return;
        PathFind();
    }

    private void PathFind()
    {
        var target = _stateMachine.Enemy.GetTarget();
        if (target != null && target.GetProperties().State != RoleState.Dead)
        {
            _stateMachine.Enemy.seeker.OncePathFinding(target.GetPosition());
        }
        else
        {
            _stateMachine.SwitchState(EnemyIdelState.pool.GetItem(null),null);
        }
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