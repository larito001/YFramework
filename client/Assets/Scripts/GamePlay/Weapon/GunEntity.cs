using System.Collections;
using System.Collections.Generic;
using Combat;
using UnityEngine;

public class GunEntity : ObjectBase, IWeapon
{
    private Transform _firePos;
    public void InitGun(Transform firepos, float cd = 0.05f, float beforeCD = 0.02f)
    {
        _firePos = firepos;
        SetPrefabBundlePath("Player/AK");
        SetInVision(true);
        weaponCD = cd;
        weaponTimer = weaponCD;
        beforeAtkTimer = beforeCD;
        beforeAtkCD = beforeCD;
        // InstanceGObj();
    }

    private float weaponTimer = 0.05f;
    private float weaponCD = 0.05f;
    private float beforeAtkTimer = 0.02f;
    private float beforeAtkCD = 0f;
    public bool isBefore = false; //0-cd，1-before-atk，2-after-atk

    public override void YOTOUpdate(float deltaTime)
    {
        if (weaponCD >= 0)
        {
            weaponCD -= deltaTime;
            return;
        }

        if (isBefore)
        {
            beforeAtkCD -= deltaTime;
            if (beforeAtkCD <= 0)
            {
                isBefore = false;
                weaponCD = weaponTimer;
                GenerateBullet();
            }
        }
    }


    public void OnUse()
    {
        // ObjTrans.gameObject.SetActive(true);
    }

    public void OnUnUse()
    {
        // ObjTrans.gameObject.SetActive(false);
    }
    
    Vector3 _hitPoint;

    private void GenerateBullet()
    {
        NormalBulletEntity b = NormalBulletEntity.pool.GetItem(new BulletConfig()
        {
            name = "Bullet/bullet",
            moveSpeed = 80,
            attackType = AttackType.Remote,
            damage = 5,
            TrggerCount = 1,
            duration = 2,
            triggerTimer = 0f,
            camp = Camp.Player,
            canAtkWall = true
        });

        //todo: 从相机发射射线，打到地面，开火方向是玩家 towards 鼠标点击位置;
        _hitPoint.y = _firePos.position.y+1.5f;
      
    
    }




    public void OnStop()
    {
    }


    public void OnDie()
    {
    }

    public override string GetModelLayer()
    {
        return "Default";
    }

    protected override void AfterInstanceGObj()
    {
    }

    protected override void BeforeRecover(bool isDelete)
    {
    }

    public TeamId Team { get; }
    public Vector3 Owner { get; }
    public WeaponConfigSO Config { get; }
    public bool TryFire(in FireRequest request)
    {
        return true;
    }
}