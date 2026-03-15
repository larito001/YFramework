using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using YOTO;
using Random = UnityEngine.Random;


public class EnemiesManager : IGameService
{
    WaitForSeconds wait = new WaitForSeconds(0.05f);
    public IEnumerator generateEnemyIE;
    private Action initCallback;
    private EnemyCamp nightCamp;
    List<EnemyCamp> enemiesCamp = new();
    List<(int, Vector3)> enemyBornPoint = new();
    public Dictionary<int, EnemyGroupData> enemyGroupdata = new();
    public Dictionary<int, EnemyData> enemyDatas = new();

 
    

    public void OnNightGenerate(Vector3 center, int enemyCount)
    {
        float minRadius = 50f;
        float maxRadius = 55f;

        for (int i = 0; i < enemyCount; i++)
        {
            float angle = Random.Range(0f, 360f);
            float radius = Random.Range(minRadius, maxRadius);

            float x = center.x + Mathf.Cos(angle * Mathf.Deg2Rad) * radius;
            float z = center.z + Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
            Vector3 pos = new Vector3(x, center.y + 10, z);
            nightCamp.GenerateEnemyAt(enemyGroupdata[100004], pos, true);
        }
    }

    IEnumerator ReGeneratEnemys()
    {
        foreach (var config in enemyBornPoint)
        {
            yield return wait;
            EnemyCamp enemyCamp = new EnemyCamp();
            enemyCamp.GenerateEnemyAt(enemyGroupdata[config.Item1], config.Item2);
            enemiesCamp.Add(enemyCamp);
        }

        Timers.inst.Add(2, (o) =>
        {
            initCallback?.Invoke();
            initCallback = null;
        });
    }

    public void RemoveEnemy(EnemyEntity enemy)
    {
        for (var i = 0; i < enemiesCamp.Count; i++)
        {
            enemiesCamp[i].RemoveEnemy(enemy);
        }
    }

    public bool GetEnemyIsInRange(Vector3 pos, float range, List<EnemyEntity> enemyList)
    {
        enemyList.Clear();
        for (var i = 0; i < enemiesCamp.Count; i++)
        {
            enemiesCamp[i].GetEnemyIsInRange(pos, range, enemyList);
        }

        if (enemyList.Count > 0)
        {
            return true;
        }

        return false;
    }

    public void Init(GameContext ctx)
    {
        EnemyEntity.Configure(ctx.Get<ICoroutineRunner>(), RemoveEnemy);
        EnemyCamp.Configure(GetEnemyData);
        TowerEntity.Configure(GetEnemyIsInRange);
    }

    public void Shutdown()
    {
       
    }

    private EnemyData GetEnemyData(int enemyId)
    {
        return enemyDatas[enemyId];
    }
}
