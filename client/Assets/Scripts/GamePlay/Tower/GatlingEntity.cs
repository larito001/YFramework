// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
//
// public class GatlingEntity : ObjectBase
// {
//     public void GatlingInit()
//     {
//         SetInVision(true);
//         SetPrefabBundlePath("Tower/TowerRendererNormal");
//         InstanceGObj();
//     }
//     private Properties properties;
//     private float CDtimer = 0;
//     private float attackCD =4f;
//     private bool isCdEnd = false;
//     Animation anim;
//     List<Vector3> atkSlot = new List<Vector3>();
//
//     public List<Vector3> GetAtkSlot()
//     {
//         atkSlot.Clear();
//         if (ObjTrans != null)
//         {
//             atkSlot.Add(ObjTrans.position);
//         }
//
//         return atkSlot;
//     }
//     public override void YOTOFixedUpdate(float dt)
//     {
//         if (objTrans == null) return;
//
//         if (CDtimer > 0)
//         {
//             CDtimer -= dt;
//         }
//         else
//         {
//             isCdEnd = true;
//         }
//
//
//         if (isShoot)
//         {
//             //todo:让ObjTrans，朝向lockTarget，只旋转y轴
//             // 计算水平方向（忽略Y轴高度差）
//             Vector3 direction = _hitPoint - ObjTrans.position;
//             direction.y = 0f;
//
//             // 防止零向量导致异常
//             if (direction.sqrMagnitude < 0.0001f) return;
//
//             // 计算目标旋转
//             Quaternion targetRotation = Quaternion.LookRotation(direction);
//
//             // 直接设置（立即转向）
//             //todo:lerp旋转，
//             ObjTrans.rotation = Quaternion.RotateTowards(
//                 ObjTrans.rotation,
//                 targetRotation,
//                 180 * dt
//             );
//
//             if (isCdEnd)
//             {
//                 isCdEnd = false;
//                 CDtimer = attackCD;
//                 GenerateBullet(_hitPoint);
//             }
//         }
//     }
//
//     private void GenerateBullet(Vector3 point)
//     {
//         var config = new ParticleEntityData();
//         config.scale = 1;
//         Vector3 startOffset = new Vector3(0, 0, 0);
//         Vector3 pos = point;
//         BaseBulletEntity b = null;
//         //寒冰蛋
//         b = FireBulletEntity.pool.GetItem(new BulletConfig()
//         {
//             name = "Bullet/bulletFire",
//             moveSpeed = 0,
//             attackType = AttackType.Near,
//             damage = 7+7*properties.Level * 0.05f,
//             TrggerCount = 999,
//             duration = 4,
//             triggerTimer = 0.25f,
//             camp = Camp.Player,
//             canAtkWall = false
//         });
//         pos += new Vector3(0, 1.5f, 0);
//         startOffset.y += 1.2f;
//         startOffset.z += 1f;
//         config.path = "Bullet/IceBulletFire";
//         config.pos = ObjTrans.position + ObjTrans.rotation * startOffset;
//         var particle = ParticleEntity.pool.GetItem(config);
//         particle.Play();
//         particle.Rotation = Quaternion.LookRotation(ObjTrans.forward, ObjTrans.up);
//
//         //todo:再加z轴方向
//         anim.Play();
//         b.Fire(this, ObjTrans.position + ObjTrans.rotation * startOffset, pos);
//     }
//
//     public override string GetModelLayer()
//     {
//         return "Agent";
//     }
//
//     protected override void AfterInstanceGObj()
//     {
//         properties = new Properties();
//         properties.HP = 99999;
//         properties.MaxHP = 99999;
//         properties.State = RoleState.Alive;
//         properties.Camp = Camp.Player;
//         anim = ObjTrans.GetComponentInChildren<Animation>();
//        var hud = ObjTrans.GetComponentInChildren<TowerBaseHud>(true);
//        hud.gameObject.SetActive(false);
//     }
//
//     protected override void BeforeRecover(bool isDelete)
//     {
//     }
//
//
//     public Properties GetProperties()
//     {
//         return properties;
//     }
//
//     public void OnHurt(IVictim fireRole, float hurt)
//     {
//     }
//
//     public Vector3 GetPosition()
//     {
//         return objTrans.position;
//     }
//
//     public Vector3 GetForward()
//     {
//         return objTrans.forward;
//     }
//
//     public void OnHurtSomeone()
//     {
//     }
//
//     public void OnSlowDown(float rate)
//     {
//     }
//
//     private Vector3 _hitPoint;
//     private bool isShoot = false;
//
//     public void OnShoot(TrainEntity trainEntity, Vector3 hitPoint, float dt)
//     {
//         _hitPoint = hitPoint;
//         if (PlayerManager.Instance.playerEntity==null&&!PlayerManager.Instance._isReborn)
//         {
//             isShoot = true;
//         }
//
//     }
//
//     public void OnEndShoot()
//     {
//         isShoot = false;
//     }
// }