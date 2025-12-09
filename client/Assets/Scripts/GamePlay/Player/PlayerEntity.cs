using UnityEngine;
using YOTO;

public class PlayerEntity : ObjectBase, PoolItem<object>, IVictim
{
    public static DataObjPool<PlayerEntity, object> pool =
        new DataObjPool<PlayerEntity, object>("PlayerEntity", 4);

    ThirdPlayerMoveCtrl playerMoveCtrl;
    public Properties Properties;
    protected override void YOTOOnload()
    {
    }


    public override void YOTOStart()
    {
    }

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
                b.Fire(ObjTrans.position, pos - ObjTrans.position);
            }
        }
    }

    public Vector3 GetForwardPos(float distance)
    {
        return ObjTrans.position + playerMoveCtrl.velocity.normalized * distance;
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

    public void AfterIntoObjectPool()
    {
        RecoverObject();
    }

    
    public void SetData(object data)
    {
        SetInVision(true);
        SetPrefabBundlePath("Player/PlayerBase");
        InstanceGObj();
        Properties = new Properties();
        Properties.HP = 100;
        Properties.OnDead = () =>
        {
            //todo:玩家死亡
        };
        Properties.Camp = Camp.Player;
        
    }


    protected override void AfterInstanceGObj()
    {
        var orbitCamera = YFramework.cameraMgr.getMainCamera().GetComponent<OrbitCamera>();
        orbitCamera.Init(ObjTrans);
        playerMoveCtrl = ObjTrans.GetComponent<ThirdPlayerMoveCtrl>();
        playerMoveCtrl.playerInputSpace = orbitCamera.transform;
        if (!ObjTrans.gameObject.TryGetComponent<TheVictim>(out TheVictim victim))
        {
            victim = ObjTrans.gameObject.AddComponent<TheVictim>();
        }

        victim.Init(new Vector3(5, 1, 5), this);
    }

    protected override void BeforeRecover(bool isDelete)
    {
        
    }

    public Properties GetProperties()
    {
        return new Properties();
    }

    public void OnHurt(float hurt)
    {
        FlyTextMgr.Instance.AddText(hurt.ToString(), objTrans.position);
        Properties.HP -= hurt;
    }

    public void OnEnter(Collider other)
    {
       
    }

    public void OnExit(Collider other)
    {
        
    }
}