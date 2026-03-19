using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyCamp
{
    private readonly System.Func<int, EnemyData> enemyDataProvider;
    private readonly System.Action<EnemyEntity> configureEnemy;
    List<EnemyEntity> enemyList = new List<EnemyEntity>();

    public EnemyCamp(System.Func<int, EnemyData> getEnemyData, System.Action<EnemyEntity> configureEnemyEntity)
    {
        enemyDataProvider = getEnemyData;
        configureEnemy = configureEnemyEntity;
    }

    public void GenerateEnemyAt(EnemyGroupData groupData,Vector3 pos,bool isNight=false)
    {
        if (groupData == null)
        {
            return;
        }

        foreach (var idAndNumber in groupData.enemyIdAndNumber)
        {
            var enemyData = new EnemyData(enemyDataProvider(idAndNumber.x));
            if (isNight)
            {
                enemyData.indexRange *= 5;  
            }
     
            for (int i = 0; i < idAndNumber.y; i++)
            {
                var enemy = EnemyEntity.pool.GetItem((enemyData, pos));
                configureEnemy?.Invoke(enemy);
                enemyList.Add(enemy);
            }
        }
        
    }
    public void RemoveEnemy(EnemyEntity enemy)
    {
        if (enemyList.Contains(enemy))
        {
            EnemyEntity.pool.RecoverItem(enemy);
            enemyList.Remove(enemy);
        }
    }

    public void ClearAllEnemies()
    {
        foreach (var enemyEntity in enemyList)
        {
            EnemyEntity.pool.RecoverItem(enemyEntity);
        }
        enemyList.Clear();
    }
    public bool GetEnemyPos(Vector3 currentPos, out Vector3 pos)
    {
        pos = Vector3.zero;

        if (enemyList == null || enemyList.Count == 0)
            return false;

        // 找到最近的有效敌人
        EnemyEntity nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (var enemyEntity in enemyList)
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

    public void GetEnemyIsInRange(Vector3 pos, float range, List<EnemyEntity> outlist)
    {
        if(outlist==null) outlist = new List<EnemyEntity>();
        
        for (var i = 0; i < enemyList.Count; i++)
        {
            if ( enemyList[i].ObjTrans!=null&&Vector3.Distance(pos, enemyList[i].ObjTrans.position) < range)
            {
                outlist.Add(enemyList[i]);
            }
        }
    }
}
