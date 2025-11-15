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
       var enemy =   EnemyEntity.pool.GetItem(null);
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

     public bool  GetEnemyPos(out Vector3 pos)
     {
         if (enemies.Count > 0&& enemies[0].ObjTrans!=null)
         {
             pos = enemies[0].ObjTrans.position;
              return true;
         }
         pos= new Vector3(0,0,0);
         return false;
     }
}
