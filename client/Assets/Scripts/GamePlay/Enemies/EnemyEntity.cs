using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyEntity : ObjectBase, PoolItem<object>, IVictim
{
    public static DataObjPool<EnemyEntity, object> pool =
        new DataObjPool<EnemyEntity, object>("EnemyEntity", 200);

    public Properties Properties;
    private IGotSeeker seeker;
    private YStateMachine stateMachine;
    protected override void YOTOOnload()
    {
    }

    public override void YOTOStart()
    {
    }

    public override void YOTOUpdate(float deltaTime)
    {
        if (seeker != null)
        {
            seeker.OncePathFinding( EnemiesManager.instance.GetPlayerPos());  
            CheckDistance();
        }
    }

    private void CheckDistance()
    {
        var dis = objTrans.position - EnemiesManager.instance.GetPlayerPos();
        if (dis.magnitude > 100)
        {
            EnemiesManager.instance.RemoveEnemy(this);
        }
    }

    public override void YOTONetUpdate()
    {
        if (seeker != null)
        {
            if (!seeker.GetIsMoving())
            {
                
                
            }
        }
    }

    public override void YOTOFixedUpdate(float deltaTime)
    {
    }

    public override void YOTOOnHide()
    {
    }

    public void SetTarget()
    {
    }

    protected override void AfterInstanceGObj()
    {
        if (!ObjTrans.gameObject.TryGetComponent<TheVictim>(out TheVictim victim))
        {
            victim = ObjTrans.gameObject.AddComponent<TheVictim>();
        }

        seeker = PathFindingFactory.GetSeeker();
        var config = new AStarMidSeekerConfig(objTrans.gameObject);
        config.UseObstacleAvoidance=true;
        config.modifierType = ModifierType.FunnelModifier;
        config.speed = 2f;
        config.constrainInsideGraph = true;
        config.stopDistance = 1;
        seeker.Init(config);
        victim.Victim = this;
    }

    protected override void BeforeRecover(bool isDelete)
    {
        seeker.Remove();
        seeker = null;
    }

    public void AfterIntoObjectPool()
    {
        RecoverObject();
        stateMachine = null;
    }

    public void SetData(object serverData)
    {
        SetInVision(true);
        SetPrefabBundlePath("Enemies/Enemy");
        InstanceGObj();
        Properties = new Properties();
        Properties.HP = 100;
        Properties.OnDead = () => { EnemiesManager.instance.RemoveEnemy(this); };
        stateMachine = new YStateMachine();
    }

    public Properties GetProperties()
    {
        return Properties;
    }

    public void OnHurt(float hurt)
    {
        FlyTextMgr.Instance.AddText(hurt.ToString(), objTrans.position);
        Properties.HP -= hurt;

    }
}