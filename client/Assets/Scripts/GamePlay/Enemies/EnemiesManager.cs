using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemiesManager : LogicPluginBase
{
     public static EnemiesManager instance;
     public EnemiesManager()
     {
          instance = this;
     }
     List<EnemyEntity> enemies = new List<EnemyEntity>();
     PlayerEntity player;
     public void SetPlayer(PlayerEntity  player)
     {
         this.player = player;
     }

     public Vector3 GetPlayerPos()
     {
         if (player.ObjTrans)
         {
             return player.ObjTrans.position;
         }
         
       return new Vector3(0,0,0);
     }

     public void GenerateAtRange(Vector3 center)
     {
         //todo: 围绕center生成敌人
         // 参数配置
         int enemyCount = 50; // 生成数量
         float minRadius = 30f; // 最小半径
         float maxRadius = 40f; // 最大半径
    
         for (int i = 0; i < enemyCount; i++)
         {
             // 随机角度和半径
             float angle = Random.Range(0f, 360f);
             float radius = Random.Range(minRadius, maxRadius);
            
             // 计算位置
             float x = center.x + Mathf.Cos(angle * Mathf.Deg2Rad) * radius;
             float z = center.z + Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
             Vector3 pos = new Vector3(x, center.y, z);
        
             // 生成敌人
             GenerateEnemyAt(pos+new Vector3(0,10,0));
         }
     }
     public void GenerateEnemyAt(Vector3  pos)
     {
       var enemy =   EnemyEntity.pool.GetItem(pos);
       enemy.Location = pos;
       enemies.Add(enemy);
     }

     public void RemoveEnemy(EnemyEntity enemy)
     {
         EnemyEntity.pool.RecoverItem( enemy);
         enemies.Remove(enemy);
     }
     
     public bool GetEnemyPos(Vector3 currentPos,out Vector3 pos)
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
