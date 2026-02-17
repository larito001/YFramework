using System.Collections;
using System.Collections.Generic;
using Combat;
using UnityEngine;
using UnityEngine.Events;

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
    public AttackType attackType; //攻击类型
    public Camp camp; //阵营
    public float duration; //持续时间
    public bool isTrack; //是否追踪
    public int TrggerCount; //触发次数
    public float triggerTimer; //触发延迟时间
    public UnityAction removeCallback;//触发时的回调
    public bool followByTrans;//是否跟随transform
    public bool canAtkWall;//打到墙是否消失

}

public class BaseBulletEntity : ObjectBase
{


    protected List<IDamageable> victims = new List<IDamageable>();
    protected BulletConfig _config;
    protected Vector3 pos;
    protected Vector3 _target;
    protected Vector3 dir;
    protected bool TryFire = false;
    protected float timer = 0;
    protected float stayTimer = 0;
    protected bool isLive = false;
    protected int triggerCount = 1;
    protected IWeapon _fireRole;
   
    public void Fire(IWeapon fireRole, Vector3 pos, Vector3 target)
    {
        _fireRole = fireRole;
        timer = 0;
        this._target = target;
        this.pos = pos;
        TryFire = true;
        dir = _target - pos;
        dir = dir.normalized;
        if (objTrans)
        {
            StartFire();
        }
    }

    private void StartFire()
    {
        TryFire = false;
        timer = 0;
        stayTimer = 0;
        objTrans.position = pos;
        objTrans.forward = dir;
    }

    public void AfterIntoObjectPool()
    {
        victims.Clear();
        isLive = false;
        timer = 0;
        SetInVision(false);
        RecoverObject();
    }

    public void SetData(BulletConfig config)
    {
        victims.Clear();
        isLive = true;
        timer = 0;
        _config = config;
        SetInVision(true);
        SetPrefabBundlePath(config.name);
        InstanceGObj();
    }


    public override string GetModelLayer()
    {
        return "BulletTrigger";
    }

    protected override void AfterInstanceGObj()
    {
        triggerCount = _config.TrggerCount;
        if (TryFire)
        {
            StartFire();
        }
    }

    protected override void BeforeRecover(bool isDelete)
    {
        _config.removeCallback?.Invoke();
    }

    public override void OnColiderEnter(Collider other)
    {
        base.OnColiderEnter(other);
        if (!isLive) return;
        
        if (_config.canAtkWall&&other.gameObject.layer == LayerMask.NameToLayer("Terrain"))
        {
            DestoryBullet();
            return;
        }
        
        if (triggerCount > 0)
        {
            if (other.TryGetComponent<SceneModelBase>(out SceneModelBase modelBase))
            {
                var victim = modelBase.GetObjectBase() as IDamageable;
                if (victim == null) return;

                if (victims.Contains(victim)) return;
                // var otherCamp = victim.Team;
                // if (otherCamp != _config.Team)
                // {
                //     victims.Add(victim);
                // }
            }
        }
    }

    public override void OnColiderExit(Collider other)
    {
        
        base.OnColiderExit(other);
        if (!isLive) return;
        if (triggerCount > 0)
        {
            if (other.TryGetComponent<SceneModelBase>(out SceneModelBase modelBase))
            {
                var victim = modelBase.GetObjectBase() as IDamageable;
                if (victim == null) return;
                if (!victims.Contains(victim)) return;
                victims.Remove(victim);
            }
        }
        
        
        
    }

    public virtual void DestoryBullet()
    {
        
    }
}