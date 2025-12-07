using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyEntity : ObjectBase, PoolItem<object>, IVictim
{
    public static DataObjPool<EnemyEntity, object> pool =
        new DataObjPool<EnemyEntity, object>("EnemyEntity", 200);

    public Properties Properties;
    private IGotSeeker seeker;

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
        // var dis = objTrans.position - EnemiesManager.instance.GetPlayerPos();
        // if (dis.magnitude > 30)
        // {
        //     EnemiesManager.instance.RemoveEnemy(this);
        // }
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
        seeker.Init(config);
        victim.Victim = this;
    }

    public void AfterIntoObjectPool()
    {
        RecoverObject();
    }

    public void SetData(object serverData)
    {
        SetInVision(true);
        SetPrefabBundlePath("Enemies/Enemy");
        InstanceGObj();
        Properties = new Properties();
        Properties.HP = 100;
        Properties.OnDead = () => { EnemiesManager.instance.RemoveEnemy(this); };
    }

    public Properties GetProperties()
    {
        return Properties;
    }

    public void OnHurt(float hurt)
    {
        Properties.HP -= hurt;
    }
}