using System.Collections;
using System.Collections.Generic;
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
}

public class BaseBulletEntity : ObjectBase
{


    protected List<IVictim> victims = new List<IVictim>();
    protected BulletConfig _config;
    protected Vector3 pos;
    protected Vector3 _target;
    protected Vector3 dir;
    protected bool TryFire = false;
    protected float timer = 0;
    protected float stayTimer = 0;
    protected bool isLive = false;
    protected int triggerCount = 1;
    protected IVictim _fireRole;

    public void Fire(IVictim fireRole, Vector3 pos, Vector3 target)
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
        if (triggerCount > 0)
        {
            if (other.TryGetComponent<SceneModelBase>(out SceneModelBase modelBase))
            {
                var victim = modelBase.GetObjectBase() as IVictim;
                if (victim == null) return;

                if (victims.Contains(victim)) return;
                var otherCamp = victim.GetProperties().Camp;
                if (otherCamp != _config.camp)
                {
                    victims.Add(victim);
                }
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
                var victim = modelBase.GetObjectBase() as IVictim;
                if (victim == null) return;
                if (!victims.Contains(victim)) return;
                victims.Remove(victim);
            }
        }
    }

    // public override void YOTOFixedUpdate(float deltaTime)
    // {
    //     if (!isLive) return;
    //     timer += deltaTime;
    //     if (timer >= _config.duration)
    //     {
    //         BaseBulletEntity.pool.RecoverItem(this);
    //         return;
    //     }
    //
    //     if (_config.attackType == AttackType.Remote)
    //     {
    //         if (objTrans)
    //         {
    //             objTrans.position += dir * _config.moveSpeed * deltaTime;
    //         }
    //     }
    //     else if (_config.attackType == AttackType.Throw)
    //     {
    //         // 真实抛体参数
    //         const float gravity = 9.8f;
    //
    //         timer += deltaTime;
    //
    //         // 起点、终点
    //         Vector3 startPos = pos;
    //         Vector3 targetPos = _targetPos;
    //
    //         // 水平分量
    //         Vector3 delta = targetPos - startPos;
    //         Vector3 deltaXZ = new Vector3(delta.x, 0f, delta.z);
    //         float distanceXZ = deltaXZ.magnitude;
    //
    //         // 初始水平速度（speed 即初速度）
    //         float horizontalSpeed = _config.moveSpeed;
    //         Vector3 horizontalDir = deltaXZ.normalized;
    //
    //         // 飞行时间（由水平匀速决定）
    //         float totalTime = distanceXZ / horizontalSpeed;
    //
    //         if (timer >= totalTime)
    //         {
    //             objTrans.position = targetPos;
    //             return;
    //         }
    //
    //         // 竖直初速度（距离越近，totalTime 越小，vy 越小，高度自然越低）
    //         float verticalSpeed =
    //             (delta.y + 0.5f * gravity * totalTime * totalTime) / totalTime;
    //
    //         // 水平位移
    //         Vector3 horizontalOffset =
    //             horizontalDir * horizontalSpeed * timer;
    //
    //         // 竖直位移
    //         float yOffset =
    //             verticalSpeed * timer - 0.5f * gravity * timer * timer;
    //
    //         objTrans.position =
    //             startPos + horizontalOffset + Vector3.up * yOffset;
    //     }
    //
    //     else if (_config.attackType == AttackType.Near)
    //     {
    //     }
    //
    //     stayTimer += deltaTime;
    //     //非延迟触发
    //     if (_config.triggerTimer == 0 && triggerCount > 0)
    //     {
    //         foreach (var theVictim in victims)
    //         {
    //             theVictim.OnHurt(_fireRole, _config.damage);
    //             triggerCount--;
    //             if (triggerCount <= 0)
    //             {
    //                 pool.RecoverItem(this);
    //                 break;
    //             }
    //         }
    //
    //         victims.Clear();
    //         return;
    //     }
    //
    //     //延迟触发
    //     if (stayTimer >= _config.triggerTimer && triggerCount > 0)
    //     {
    //         stayTimer -= _config.triggerTimer;
    //         foreach (var theVictim in victims)
    //         {
    //             theVictim.OnHurt(_fireRole, _config.damage);
    //             triggerCount--;
    //             if (triggerCount <= 0)
    //             {
    //                 break;
    //             }
    //         }
    //
    //         if (triggerCount <= 0)
    //         {
    //             pool.RecoverItem(this);
    //         }
    //     }
    //
    //  
    // }
}