using UnityEngine;
using YOTO;

public class PlayerEntity : ObjectBase, PoolItem<object>, IVictim
{
    public static DataObjPool<PlayerEntity, object> pool =
        new DataObjPool<PlayerEntity, object>("PlayerEntity", 4);

    ThirdPlayerMoveCtrl playerMoveCtrl;
    public Properties properties;
    public override void YOTOUpdate(float deltaTime)
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (objTrans == null) return;
            Vector3 pos = new Vector3();
            if (EnemiesManager.instance.GetEnemyPos(objTrans.position,out pos))
            {
                BaseBulletEntity b = BaseBulletEntity.pool.GetItem(new BulletConfig()
                {
                    name = "Bullet/bullet",
                    moveSpeed =20,
                    attackType = AttackType.Remote,
                    damage = 50,
                    TrggerCount = 1,
                    duration = 10,
                    triggerTimer = 0f,
                    camp = Camp.Player
                });
                pos.y += Random.Range(0.5f, 2);
                b.Fire(this,ObjTrans.position, pos - ObjTrans.position);
            }
        }
    }

    public Vector3 GetForwardPos(float distance)
    {
        return ObjTrans.position + playerMoveCtrl.velocity.normalized * distance;
    }
    
    public void AfterIntoObjectPool()
    {
        RecoverObject();
    }

    
    public void SetData(object data)
    {
        SetInVision(true);
        SetPrefabBundlePath("Player/PlayerBase");
        InstanceGObj();
        properties = new Properties();
        properties.HP = 100;
        properties.OnDead = () =>
        {
            //todo:玩家死亡
       
            PlayerManager.Instance.PlayerDie();
      
        };
        properties.Camp = Camp.Player;

        
    }


    public override string GetModelLayer()
    {
        return "Agent";
    }

    protected override void AfterInstanceGObj()
    {
        properties.State = RoleState.Alive;
        var orbitCamera = YFramework.cameraMgr.getMainCamera().GetComponent<OrbitCamera>();
        orbitCamera.Init(ObjTrans);
        playerMoveCtrl = ObjTrans.GetComponent<ThirdPlayerMoveCtrl>();
        playerMoveCtrl.playerInputSpace = orbitCamera.transform;
    }

    protected override void BeforeRecover(bool isDelete)
    {
        properties.State = RoleState.Dead;
    }

    public Transform GetTransform()
    {
        return ObjTrans;
    }

    public int GetId()
    {
        return _entityID;
    }

    public Properties GetProperties()
    {
        return properties;
    }

    public void OnHurt(IVictim fireRole, float hurt)
    {

        if (properties == null || properties.State == RoleState.Dead || objTrans == null) return;
        
        FlyTextMgr.Instance.AddText(hurt.ToString(), objTrans.position,FlyTextType.PlayerHurt);
        properties.HP -= hurt;
        fireRole.OnHurtSomeone();
    }
    

    public Vector3 GetPosition()
    {
        if (objTrans != null)
        {
            return ObjTrans.position;
        }

        return Location;
    }

    public void OnHurtSomeone()
    {
        
    }
}