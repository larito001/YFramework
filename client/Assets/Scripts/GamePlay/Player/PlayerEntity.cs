
using UnityEngine;
using YOTO;

public class PlayerEntity : ObjectBase, PoolItem<object>
{
    public static DataObjPool<PlayerEntity, object> pool =
        new DataObjPool<PlayerEntity, object>("PlayerEntity", 4);


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
            BaseBulletEntity b = BaseBulletEntity.pool.GetItem(new BulletConfig()
            {
                name = "Bullet/bullet",
                moveSpeed = 10,
                damage = 1,
                duration=1,
            });
            b.Fire(ObjTrans.position, ObjTrans.GetComponent<ThirdPlayerMoveCtrl>().forwardAxis);
        }
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
        ParticleEntity.pool.RecoverItem(particle);
    }

  
    public void SetData(object data)
    {
        SetInVision(true);
        SetPrefabBundlePath("Player/PlayerBase");
        InstanceGObj();
    }



    private ParticleEntity particle;
    public Transform mousePos;

    protected override void AfterInstanceGObj()
    {
       var orbitCamera = YOTOFramework.cameraMgr.getMainCamera().GetComponent<OrbitCamera>();
       orbitCamera.Init(ObjTrans);
       ObjTrans.GetComponent<ThirdPlayerMoveCtrl>().playerInputSpace = orbitCamera.transform;
      
    }
    
}