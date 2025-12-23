using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AxeEntity :  ObjectBase,IWeapon
{
    public void Init()
    {
        SetPrefabBundlePath("Player/PlayerBase");
        SetInVision(true);
        // InstanceGObj();
    }

    public void OnUse()
    {
        
    }
    private float weaponTimer =0.5f;
    private float weaponTimerTemp = 0.5f;
    public void OnShoot(IVictim fireRole, Vector3 hitPoint, float dt)
    {
        if (weaponTimerTemp >= weaponTimer)
        {
            weaponTimerTemp -= weaponTimer;
        }
        else
        {
            weaponTimerTemp += dt;
            return;
        }

        NormalBulletEntity b = NormalBulletEntity.pool.GetItem(new BulletConfig()
        {
            name = "Bullet/bulletAex",
            moveSpeed = 0,
            attackType = AttackType.Remote,
            damage = 30,
            TrggerCount = 999,
            duration = 0.1f,
            triggerTimer = 0f,
            camp = Camp.Player,
        });

        //todo: 从相机发射射线，打到地面，开火方向是玩家 towards 鼠标点击位置;
        hitPoint.y = 0.5f;
        b.Fire(fireRole, fireRole.GetPosition(), hitPoint);
    }


    public void OnDie()
    {
        
    }
    public override string GetModelLayer()
    {
       return  "Default";
    }

    protected override void AfterInstanceGObj()
    {
        
    }

    protected override void BeforeRecover(bool isDelete)
    {
    }
}
