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
            Vector3 pos = new Vector3();
            if (EnemiesManager.instance.GetEnemyPos(out pos))
            {
                BaseBulletEntity b = BaseBulletEntity.pool.GetItem(new BulletConfig()
                {
                    name = "Bullet/bullet",
                    moveSpeed = 10,
                    damage = 1,
                    duration = 1,
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
        var orbitCamera = YOTOFramework.cameraMgr.getMainCamera().GetComponent<OrbitCamera>();
        orbitCamera.Init(ObjTrans);
        playerMoveCtrl = ObjTrans.GetComponent<ThirdPlayerMoveCtrl>();
        playerMoveCtrl.playerInputSpace = orbitCamera.transform;
    }
}