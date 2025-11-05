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
}

public class BaseBulletEntity : ObjectBase, PoolItem<BulletConfig>
{
    public static DataObjPool<BaseBulletEntity, BulletConfig> pool =
        new DataObjPool<BaseBulletEntity, BulletConfig>("BaseBulletEntity", 50);

    BulletConfig _config;
    private Vector3 pos;
    private Vector3 dir;
    private bool TryFire = false;

    public void Fire(Vector3 pos, Vector3 dir)
    {
        this.dir = dir;
        this.pos = pos;
        TryFire = true;
        if (objTrans)
        {
            objTrans.position = pos;
            StartFire();
        }
    }

    private void StartFire()
    {
        TryFire = false;
    }

    public void AfterIntoObjectPool()
    {
        var trigger = ObjTrans.GetComponent<BulletTrigger>();
        trigger.Unload();
        SetInVision(false);
        RecoverObject();
    }

    public void SetData(BulletConfig config)
    {
        _config = config;
        SetInVision(true);
        SetPrefabBundlePath(config.name);
        InstanceGObj();
    }

    protected override void AfterInstanceGObj()
    {
        var trigger = ObjTrans.GetComponent<BulletTrigger>();
        trigger.Init(this);
        if (TryFire)
        {
            StartFire();
        }
    }

    public void TriggerEnter()
    {
    }

    public void TriggerExit()
    {
    }

    public void TriggerStay()
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