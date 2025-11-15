using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 子弹基类，4种，近战，远程子弹，投掷，固定点爆破
/// </summary>

//阵营
public enum Camp
{
    Player = 0,
    Enemy = 1
}

public enum AttackType
{
    //近战
    Near = 0,

    //远程
    Remote = 1,

    //投掷
    Throw = 2,

    //固定点爆破
    FixedPoint = 3
}

public struct BulletConfig
{
    public string name;
    public float moveSpeed; //移动速度
    public float damage; //伤害
    public float attackRange; //攻击范围
    public AttackType attackType; //攻击类型
    public Camp camp; //阵营
    public float duration; //持续时间
    public bool isTrack; //是否追踪
    public int TrggerCount ;
}

public class BaseBulletEntity : ObjectBase, PoolItem<BulletConfig>
{
    public static DataObjPool<BaseBulletEntity, BulletConfig> pool =
        new DataObjPool<BaseBulletEntity, BulletConfig>("BaseBulletEntity", 50);

    BulletConfig _config;
    private Vector3 pos;
    private Vector3 dir;
    private bool TryFire = false;
    private float timer = 0;
    private bool isLive = false;
    private int triggerCount=1;
    public void Fire(Vector3 pos, Vector3 dir)
    {
        timer = 0;
        this.dir = dir;
        this.pos = pos;
        TryFire = true;
      
        if (objTrans)
        {
         
            StartFire();
        }
    }

    private void StartFire()
    {
        TryFire = false;
        objTrans.position = pos;
        objTrans.forward = dir;
   
    }

    public void AfterIntoObjectPool()
    {
        isLive = false;
        timer = 0;
        var trigger = ObjTrans.GetComponent<BulletTrigger>();
        trigger.Unload();
        SetInVision(false);
        RecoverObject();
    }

    public void SetData(BulletConfig config)
    {
        isLive = true;
        timer = 0;
        _config = config;
        SetInVision(true);
        SetPrefabBundlePath(config.name);
        InstanceGObj();
    }

   
    protected override void AfterInstanceGObj()
    {
        triggerCount = 2;
        var trigger = ObjTrans.GetComponent<BulletTrigger>();
       
        trigger.Init(this);
        if (TryFire)
        {
            StartFire();
        }
    }

    public void TriggerEnter(Collider other)
    {
        if (!isLive) return;
        if (triggerCount > 0)
        {
          
       
            if (other.TryGetComponent<TheVictim>(out TheVictim victim))
            {
                //todo:根据配置伤害
                victim.Victim?.OnHurt(50);
                triggerCount--;
            }
            if (triggerCount <=0)
            {
                pool.RecoverItem(this);
            }
        }
      
     
        
    }

    public void TriggerExit(Collider other)
    {
    }

    public void TriggerStay(Collider other)
    {
    }

    protected override void YOTOOnload()
    {
    }

    public override void YOTOStart()
    {
    }

    public override void YOTOUpdate(float deltaTime)
    {
        if (!isLive) return;
        timer+= deltaTime;
        if (timer>=_config.duration)
        {
            BaseBulletEntity.pool.RecoverItem(this);
        }
        if (objTrans)
        {
            
            objTrans.position += dir * _config.moveSpeed * deltaTime;
        }
        
    }

    public override void YOTONetUpdate()
    {
    }

    public override void YOTOFixedUpdate(float deltaTime)
    {
    }

    public override void YOTOOnHide()
    {
    }
}