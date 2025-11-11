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
     }

     public void RemoveEnemy(EnemyEntity enemy)
     {
         EnemyEntity.pool.RecoverItem( enemy);
     }

     public void GenerateAtPlayerMoveDir()
     {
         GenerateEnemyAt(player.GetForwardPos(10));
     }
}
