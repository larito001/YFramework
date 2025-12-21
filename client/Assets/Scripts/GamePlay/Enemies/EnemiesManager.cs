using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using YOTO;

public enum EnemyActionType
{
    Idel = 0,
    Round,
    Nigt,
    Boss
}

public struct EnemyConfig
{
    public int posId;
    public Vector3 Location;
    public EnemyActionType ActionType;
    public int number;
}

public class EnemiesManager : LogicPluginBase
{
    public static EnemiesManager instance;

    public EnemiesManager()
    {
        instance = this;
    }
    WaitForSeconds wait = new WaitForSeconds(0.05f);
    List<EnemyEntity> enemies = new List<EnemyEntity>();

    Dictionary<int, EnemyConfig> enemyBornPoint = new();
    public IEnumerator generateEnemyIE;
    private UnityAction initCallback;
    public void Init(UnityAction callback)
    {
        initCallback = callback;
        enemyBornPoint.Clear();
        GameObject enemyRoot = GameObject.Find("EnemyRoot");
        var poss = enemyRoot.GetComponentsInChildren<Transform>();
        foreach (var child in poss)
        {
            var infos = child.name.Split("|");
            if (infos.Length < 4) continue;
            EnemyConfig config = new EnemyConfig();
            config.posId = int.Parse(infos[1]);
            config.Location = child.position;
            config.ActionType = (EnemyActionType)int.Parse(infos[3]);
            config.number = int.Parse(infos[2]);
            enemyBornPoint.Add(int.Parse(infos[1]), config);
        }

        if (generateEnemyIE != null)
        {
            YFramework.Instance.StopCoroutine(generateEnemyIE);
            generateEnemyIE = null;
        }
        generateEnemyIE = ReGeneratEnemys();
        YFramework.Instance.StartCoroutine(generateEnemyIE);
    }

    IEnumerator ReGeneratEnemys()
    {
        //生成敌人
        foreach (var config in enemyBornPoint.Values)
        {
            yield return wait;
            GenerateEnemyAt(config, config.number);
        }
        initCallback?.Invoke();
        initCallback = null;
    }


    // public void GenerateAtRange(Vector3 center)
    // {
    //     //todo: 围绕center生成敌人
    //     // 参数配置
    //     int enemyCount = 50; // 生成数量
    //     float minRadius = 30f; // 最小半径
    //     float maxRadius = 40f; // 最大半径
    //
    //     for (int i = 0; i < enemyCount; i++)
    //     {
    //         // 随机角度和半径
    //         float angle = Random.Range(0f, 360f);
    //         float radius = Random.Range(minRadius, maxRadius);
    //
    //         // 计算位置
    //         float x = center.x + Mathf.Cos(angle * Mathf.Deg2Rad) * radius;
    //         float z = center.z + Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
    //         Vector3 pos = new Vector3(x, center.y, z);
    //
    //         // 生成敌人
    //         GenerateEnemyAt(pos + new Vector3(0, 10, 0),1);
    //     }
    // }

    public void GenerateEnemyAt(EnemyConfig pos, int number)
    {
        for (int i = 0; i < number; i++)
        {
            var enemy = EnemyEntity.pool.GetItem(pos);
            enemy.Location = pos.Location;
            enemies.Add(enemy);
        }
    }

    public void RemoveEnemy(EnemyEntity enemy)
    {
        EnemyEntity.pool.RecoverItem(enemy);
        enemies.Remove(enemy);
    }

    public bool GetEnemyPos(Vector3 currentPos, out Vector3 pos)
    {
        pos = Vector3.zero;

        if (enemies == null || enemies.Count == 0)
            return false;

        // 找到最近的有效敌人
        EnemyEntity nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (var enemyEntity in enemies)
        {
            // 检查敌人是否有效
            if (enemyEntity == null || enemyEntity.ObjTrans == null)
                continue;

            // 计算距离（假设调用者是当前对象）
            // 如果你需要从特定位置计算，可以添加参数：Vector3 fromPosition
            float distance = Vector3.Distance(currentPos, enemyEntity.ObjTrans.position);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = enemyEntity;
            }
        }

        // 如果找到有效敌人，返回位置
        if (nearest != null && nearest.ObjTrans != null)
        {
            pos = nearest.ObjTrans.position;
            return true;
        }

        return false;
    }
}