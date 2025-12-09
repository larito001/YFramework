using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyStateMachine : YStateMachine
{
    public EnemyEntity Enemy;

    public void Init(EnemyEntity enemy)
    {
        Enemy = enemy;
    }
}