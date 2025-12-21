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
    public float atkRange;
}

public class EnemiesManager : LogicPluginBase
{
    public static EnemiesManager instance;

    public EnemiesManager()
    {
        instance = this;
    }

    WaitForSeconds wait = new WaitForSeconds(0.5f);
    // List<EnemyEntity> enemies = new List<EnemyEntity>();

    List<EnemyCamp> enemies = new();
    Dictionary<int, EnemyConfig> enemyBornPoint = new();
    public IEnumerator generateEnemyIE;
    private UnityAction initCallback;

    private EnemyCamp nightCamp;
    
    public void OnNightGenerate(Vector3 center)
    {
       
        //todo: 围绕center生成敌人
        // 参数配置
        int enemyCount = 40; // 生成数量
        float minRadius = 50f; // 最小半径
        float maxRadius = 55f; // 最大半径
     
        for (int i = 0; i < enemyCount; i++)
        {
            // 随机角度和半径
            float angle = Random.Range(0f, 360f);
            float radius = Random.Range(minRadius, maxRadius);
            
            // 计算位置
            float x = center.x + Mathf.Cos(angle * Mathf.Deg2Rad) * radius;
            float z = center.z + Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
            Vector3 pos = new Vector3(x, center.y, z);
            EnemyConfig  config = new EnemyConfig();
            config.posId = -1;
            config.number = 1;
            config.atkRange = 60f;
            config.Location = pos+new Vector3(0,10,0);
            nightCamp.GenerateEnemyAt(config);
        }
    }
    
    
    public void Init(UnityAction callback)
    {
        nightCamp = new EnemyCamp();
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
            config.atkRange = 20;
            enemyBornPoint.Add(int.Parse(infos[1]), config);
        }

        if (generateEnemyIE != null)
        {
            YFramework.Instance.StopCoroutine(generateEnemyIE);
            generateEnemyIE = null;
        }
        nightCamp= new EnemyCamp();
        enemies.Add(nightCamp);
        generateEnemyIE = ReGeneratEnemys();
        YFramework.Instance.StartCoroutine(generateEnemyIE);
  
    }

    IEnumerator ReGeneratEnemys()
    {
        //生成敌人
        foreach (var config in enemyBornPoint.Values)
        {
            yield return wait;
            EnemyCamp enemyCamp = new EnemyCamp();
            enemyCamp.GenerateEnemyAt(config);
            enemies.Add(enemyCamp);
        }

        initCallback?.Invoke();
        initCallback = null;
    }
    public void RemoveEnemy(EnemyEntity enemy)
    {
        for (var i = 0; i < enemies.Count; i++)
        {
            enemies[i].RemoveEnemy(enemy); 
        }
    }

    public bool GetEnemyIsInRange(Vector3 pos, float range,List<EnemyEntity> enemyList)
    {
        enemyList.Clear();
        for (var i = 0; i < enemies.Count; i++)
        {
            enemies[i].GetEnemyIsInRange(pos, range,enemyList);
        }

        if (enemyList.Count > 0)
        {
            return true;
        }
        return false;
    }
}