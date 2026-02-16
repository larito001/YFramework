using System.Collections;
using System.Collections.Generic;
using Combat;
using UnityEngine;

public class AxeEntity :  ObjectBase,IWeapon
{
    public void InitAex(Transform parent)
    {
        Parent = parent;
        SetPrefabBundlePath("Player/Aex");
        SetInVision(true);
        // InstanceGObj();
    }
    private float weaponTimer =0.7f;
    private float weaponCD= 0.7f;
    private float beforeAtkTimer = 0.3f;
    private float beforeAtkCD = 0f;
    public bool isBefore = false;//0-cd，1-before-atk，2-after-atk
    public override void YOTOUpdate(float deltaTime)
    {
        if (weaponCD >= 0)
        {
            weaponCD -= deltaTime;
            return;
        }

        if (isBefore)
        {
            beforeAtkCD-= deltaTime;
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
    IVictim _fireRole = null;
    Vector3 _hitPoint;
    
    private void GenerateBullet()
    {
        NormalBulletEntity b = NormalBulletEntity.pool.GetItem(new BulletConfig()
        {
            name = "Bullet/bulletAex",
            moveSpeed = 0,
            attackType = AttackType.Remote,
            damage =16,
            TrggerCount = 4,
            duration = 0.1f,
            triggerTimer = 0f,
            camp = Camp.Player,
            canAtkWall =  false
        });

        //todo: 从相机发射射线，打到地面，开火方向是玩家 towards 鼠标点击位置;
        _hitPoint.y = 0.5f;
        b.Fire(_fireRole, _fireRole.GetPosition(), _hitPoint);
        weaponCD =weaponTimer;
        var config = new ParticleEntityData();
        config.path = "Player/Slash";
        config.pos = _fireRole.GetPosition();
        config.scale = 1;
        var particle = ParticleEntity.pool.GetItem(config);
        particle.Play(2f);
        particle.Rotation = Quaternion.LookRotation(_fireRole.GetForward(), Vector3.up);
    }
    public void OnShoot(IVictim fireRole, Vector3 hitPoint, float dt)
    {
        
        if (weaponCD<=0&&!isBefore)
        {   isBefore = true;
            _fireRole=fireRole;
            _hitPoint = hitPoint;
            beforeAtkCD= beforeAtkTimer;
        }
    }



    public void OnStop()
    {
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

    public TeamId Team { get; }
    public Vector3 Owner { get; }
    public WeaponConfigSO Config { get; }
    public bool TryFire(in FireRequest request)
    {
        
    }
}
