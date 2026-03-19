using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using YOTO;
using Random = UnityEngine.Random;


public class EnemiesManager : IGameService
{
    private readonly WaitForSeconds wait = new WaitForSeconds(0.05f);
    public IEnumerator generateEnemyIE;
    private Action initCallback;
    private EnemyCamp nightCamp;
    private readonly List<EnemyCamp> enemiesCamp = new();
    private readonly List<(int, Vector3)> enemyBornPoint = new();
    public Dictionary<int, EnemyGroupData> enemyGroupdata = new();
    public Dictionary<int, EnemyData> enemyDatas = new();
    private ICoroutineRunner coroutineRunner;

 
    

    public void OnNightGenerate(Vector3 center, int enemyCount)
    {
        if (nightCamp == null)
        {
            Debug.LogWarning("Night enemy camp is not initialized.");
            return;
        }

        if (!enemyGroupdata.TryGetValue(100004, out var nightGroup))
        {
            Debug.LogWarning("Night enemy group 100004 was not found.");
            return;
        }

        float minRadius = 50f;
        float maxRadius = 55f;

        for (int i = 0; i < enemyCount; i++)
        {
            float angle = Random.Range(0f, 360f);
            float radius = Random.Range(minRadius, maxRadius);

            float x = center.x + Mathf.Cos(angle * Mathf.Deg2Rad) * radius;
            float z = center.z + Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
            Vector3 pos = new Vector3(x, center.y + 10, z);
            nightCamp.GenerateEnemyAt(nightGroup, pos, true);
        }
    }

    IEnumerator ReGeneratEnemys()
    {
        foreach (var config in enemyBornPoint)
        {
            yield return wait;
            if (!enemyGroupdata.TryGetValue(config.Item1, out var groupData))
            {
                Debug.LogWarning($"Enemy group {config.Item1} was not found.");
                continue;
            }

            EnemyCamp enemyCamp = CreateCamp();
            enemyCamp.GenerateEnemyAt(groupData, config.Item2);
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
        coroutineRunner = ctx.Get<ICoroutineRunner>();
        nightCamp = CreateCamp();
    }

    public void Shutdown()
    {
        for (int i = 0; i < enemiesCamp.Count; i++)
        {
            enemiesCamp[i].ClearAllEnemies();
        }

        enemiesCamp.Clear();
        nightCamp?.ClearAllEnemies();
        nightCamp = null;
        coroutineRunner = null;
    }

    private EnemyData GetEnemyData(int enemyId)
    {
        if (!enemyDatas.TryGetValue(enemyId, out var enemyData))
        {
            throw new KeyNotFoundException($"Enemy data {enemyId} was not found.");
        }

        return enemyData;
    }

    private EnemyCamp CreateCamp()
    {
        return new EnemyCamp(GetEnemyData, ConfigureEnemy);
    }

    private void ConfigureEnemy(EnemyEntity enemy)
    {
        enemy.ConfigureRuntime(coroutineRunner, RemoveEnemy);
    }
}
