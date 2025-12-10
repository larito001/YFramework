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

     public void GenerateAtPlayerMoveDir()
     {
         GenerateEnemyAt(player.GetForwardPos(10));
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
