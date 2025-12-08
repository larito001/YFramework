using UnityEngine;
using YOTO;

public class PlayerEntity : ObjectBase, PoolItem<object>
{
    public static DataObjPool<PlayerEntity, object> pool =
        new DataObjPool<PlayerEntity, object>("PlayerEntity", 4);

    ThirdPlayerMoveCtrl playerMoveCtrl;

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
                    triggerTimer = 0f
                });
           
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
    }


    protected override void AfterInstanceGObj()
    {
        var orbitCamera = YFramework.cameraMgr.getMainCamera().GetComponent<OrbitCamera>();
        orbitCamera.Init(ObjTrans);
        playerMoveCtrl = ObjTrans.GetComponent<ThirdPlayerMoveCtrl>();
        playerMoveCtrl.playerInputSpace = orbitCamera.transform;
    }

    protected override void BeforeRecover(bool isDelete)
    {
        
    }
}