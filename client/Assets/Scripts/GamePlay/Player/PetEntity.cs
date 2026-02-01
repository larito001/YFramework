using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Pathfinding.Examples;
using UnityEngine;

public class PetEntity : ObjectBase, IVictim
{
    #region 属性

    public Properties properties;
    public IGotSeeker seeker;
    public bool NeedRound = false;
    public Vector3 OrgPos = Vector3.zero;
    private IVictim _lockTarget = null;

    #endregion


    #region 生命周期

    public void PetInit()
    {
        SetInVision( true);
        SetPrefabBundlePath("Enemies/Pet"); 
        InstanceGObj();
    }
    public override void YOTOUpdate(float deltaTime)
    {
    }

    public override string GetModelLayer()
    {
        return "Agent";
    }

    public override void YOTOFixedUpdate(float deltaTime)
    {
        base.YOTOFixedUpdate(deltaTime);
        TryExchangeTarget(deltaTime);
    }

    protected override void AfterInstanceGObj()
    {


        properties = new Properties();
        properties.HP = 9999;
        properties.OnDead = () => { properties.State = RoleState.Dead; };
        properties.Camp = Camp.Player;
        properties.State = RoleState.Alive;
        
        seeker = PathFindingFactory.GetSeeker();
        var config = new AStarHighSeekerConfig(objTrans.gameObject);
        config.UseObstacleAvoidance = true;
        config.modifierType = ModifierType.FunnelModifier;
        config.speed = 8;
        config.stopDistance = 1;
        config.isUpdate = true;
        config.enableGravity = true;
        config.OnPathComplete = OnPathCompleteCallback;
        var anim = objTrans.GetComponentInChildren<MineBotAnimation>();
        config.radio = 0.5f * ChangeScale(anim.transform);
        seeker.Init(config);
        anim.ai = seeker.GetSeeker();
        NeedRound = true;
        OrgPos = Location;

        ChangeAllChildrenColor(objTrans);
    }

    protected override void BeforeRecover(bool isDelete)
    {
        seeker.Remove();
        seeker = null;
    }

 
    



    void ChangeAllChildrenColor(Transform parent)
    {
        // 遍历当前父对象下的所有子对象
        foreach (Transform child in parent)
        {
            // 获取子对象的MeshRenderer组件
            MeshRenderer renderer = child.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material.color = Color.green;
            }

            // 递归处理子对象的子对象（孙对象）
            if (child.childCount > 0)
            {
                ChangeAllChildrenColor(child);
            }
        }
    }

    private float ChangeScale(Transform trans)
    {
        float scale = 1.5f;
        trans.localScale = Vector3.one * scale;

        return scale;
    }

    #endregion

    #region get

    public Transform GetTransform()
    {
        return objTrans;
    }

    public int GetId()
    {
        return _entityID;
    }

    public IVictim GetTarget()
    {
        return _lockTarget;
    }



    public Properties GetProperties()
    {
        return properties;
    }

    public Vector3 GetPosition()
    {
        if (objTrans != null)
        {
            return objTrans.position;
        }

        return Location;
    }
    List<Vector3> atkSlot = new List<Vector3>();

    public List<Vector3> GetAtkSlot()
    {
        atkSlot.Clear();
        if (ObjTrans != null)
        {
            atkSlot.Add(ObjTrans.position);
        }

        return atkSlot;
    }

    public Vector3 GetForward()
    {
        if (objTrans != null)
        {
            return objTrans.forward;
        }

        return Vector3.down;
    }

    #endregion

    #region 状态转换

    private float CDtimer = 0;
    private float attackCD = 1f;
    private bool isCdEnd = false;
    private List<EnemyEntity> enemyList =new();

    private IVictim GetNearestEnemyPos()
    {
        return enemyList.OrderBy(x => Vector3.Distance(x.GetPosition(), ObjTrans.position)).FirstOrDefault();
    }
    /// <summary>
    /// 尝试索敌
    /// </summary>
    public void TryExchangeTarget(float dt)
    {
        if (objTrans == null) return;

        if (CDtimer > 0)
        {
            CDtimer -= dt;

        }
        else
        {
            isCdEnd = true;
        }
        //todo:获取索敌对象
        
        if (EnemiesManager.instance.GetEnemyIsInRange(GetPosition(), 10, enemyList))
        {
            _lockTarget = GetNearestEnemyPos();
        }
        if(PlayerManager.Instance.playerEntity!=null)
        seeker.OncePathFinding(PlayerManager.Instance.playerEntity.GetPosition());

        if (_lockTarget != null)
        {
            if (isCdEnd)
            {
                isCdEnd = false;
                CDtimer = attackCD;

                GenerateBullet(_lockTarget);
                _lockTarget = null;
            }

        }
    }
        private void GenerateBullet(IVictim victim)
    {
        if (ObjTrans == null) return;
        
        Vector3 startOffset = new Vector3(0, 0, 0);
        Vector3 pos = victim.GetPosition();
        BaseBulletEntity b = null;
        //寒冰蛋
        b = NormalBulletEntity.pool.GetItem(new BulletConfig()
        {
            name = "Bullet/bullet",
            moveSpeed = 30,
            damage = 20,
            duration = 10,
            TrggerCount = 1,
            triggerTimer = 0,
            attackType = AttackType.Remote,
            camp = Camp.Player,
            canAtkWall = true
        });
        pos += new Vector3(0, 1.5f, 0);
        startOffset.y += 1.2f;
        startOffset.z += 1f;

        //todo:再加z轴方向

        b.Fire(this, ObjTrans.position + ObjTrans.rotation * startOffset, pos);
    }

    public void OnHurt(IVictim fireRole, float hurt)
    {
        if (objTrans==null|| properties == null || properties.State == RoleState.Dead) return;
        FlyTextMgr.Instance.AddText(hurt.ToString(), objTrans.position,FlyTextType.Quick);
        properties.HP -= hurt;
        fireRole.OnHurtSomeone();
    }


    /// <summary>
    /// 射出的子弹被销毁后调用
    /// </summary>
    public void OnBulletFinish()
    {
    }

    private void OnPathCompleteCallback()
    {
    }


    /// <summary>
    /// 子弹打到对方
    /// </summary>
    public void OnHurtSomeone()
    {
    }

    public void OnSlowDown(float rate)
    {
        
    }

    #endregion
}