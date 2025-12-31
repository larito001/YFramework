using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using YOTO;



public class EnemiesManager : LogicPluginBase
{
    public static EnemiesManager instance;

    public EnemiesManager()
    {
        instance = this;
    }

    WaitForSeconds wait = new WaitForSeconds(0.05f);
    public IEnumerator generateEnemyIE;
    private UnityAction initCallback;
    private EnemyCamp nightCamp;
    List<EnemyCamp> enemies = new();
    List<(int, Vector3)> enemyBornPoint = new();
    public Dictionary<int, EnemyGroupData>  enemyGroupdata =new ();
    public Dictionary<int, EnemyData> enemyDatas = new();

    
    public void Init(UnityAction callback)
    {
        enemyGroupdata.Clear();
       var  enemyGroupSo = Resources.Load<EnemyGroupSO>("Config/EnemyGroupSO");
       
       foreach (var enemyGroupData in enemyGroupSo.EnemyGroupDatas)
       {
           enemyGroupdata.Add(enemyGroupData.id, new EnemyGroupData(enemyGroupData));
       }
       //卸载SO
       Resources.UnloadAsset(enemyGroupSo);

       var enemyDataSo = Resources.Load<EnemyDataSO>("Config/EnemyDataSO");
       enemyDatas.Clear();
       foreach (var enemyData in enemyDataSo.EnemyDatas)
       {
           enemyDatas.Add(enemyData.id, new EnemyData(enemyData));
       }
       Resources.UnloadAsset(enemyDataSo);
       
        initCallback = callback;
        enemyBornPoint.Clear();
        GameObject enemyRoot = GameObject.Find("EnemyRoot");
        var poss = enemyRoot.GetComponentsInChildren<Transform>();
        foreach (var child in poss)
        {
            if (int.TryParse(child.name, out int id))
            {
                if (id <= 0) continue;
                var data = enemyGroupdata[id];
                data.locationTemp = child.position;
                enemyBornPoint.Add((id, child.position));
                
            }

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

    public void OnNightGenerate(Vector3 center,int enemyCount)
    {

        //todo: 围绕center生成敌人
        // 参数配置
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
            Vector3 pos = new Vector3(x, center.y+10, z);
            nightCamp.GenerateEnemyAt(enemyGroupdata[100004], pos,true);
        }
    }

    IEnumerator ReGeneratEnemys()
    {
        //生成敌人
        foreach (var config in enemyBornPoint)
        {
            yield return wait;
            EnemyCamp enemyCamp = new EnemyCamp();
            enemyCamp.GenerateEnemyAt(enemyGroupdata[config.Item1], config.Item2);
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