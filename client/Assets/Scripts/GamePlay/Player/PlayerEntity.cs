using UnityEngine;
using YOTO;

public class PlayerEntity : ObjectBase, PoolItem<object>, IVictim, IUser
{
    public static DataObjPool<PlayerEntity, object> pool =
        new DataObjPool<PlayerEntity, object>("PlayerEntity", 4);

    ThirdPlayerMoveCtrl playerMoveCtrl;
    public Properties properties;

    #region 生命周期

    public override void YOTOUpdate(float deltaTime)
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (_usableItemInRange != null)
            {
                _usableItemInRange.OnUse(this);
            }
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (objTrans == null) return;
            Vector3 pos = new Vector3();
            if (EnemiesManager.instance.GetEnemyPos(objTrans.position, out pos))
            {
                BaseBulletEntity b = BaseBulletEntity.pool.GetItem(new BulletConfig()
                {
                    name = "Bullet/bullet",
                    moveSpeed = 80,
                    attackType = AttackType.Remote,
                    damage = 5,
                    TrggerCount = 1,
                    duration = 10,
                    triggerTimer = 0f,
                    camp = Camp.Player
                });
                pos.y += Random.Range(0.5f, 2);
                b.Fire(this, ObjTrans.position, pos - ObjTrans.position);
            }
        }
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

    public void AfterIntoObjectPool()
    {
        RecoverObject();
    }

    #endregion

    #region IVictim

    public Properties GetProperties()
    {
        return properties;
    }

    public void OnHurt(IVictim fireRole, float hurt)
    {
        if (properties == null || properties.State == RoleState.Dead || objTrans == null) return;

        FlyTextMgr.Instance.AddText(hurt.ToString(), objTrans.position, FlyTextType.PlayerHurt);
        properties.HP -= hurt;
        fireRole.OnHurtSomeone();
    }

    public void OnHurtSomeone()
    {
    }

    public Vector3 GetPosition()
    {
        if (objTrans != null)
        {
            return ObjTrans.position;
        }

        return Location;
    }

    #endregion

    #region IUser

    private bool isUsing = false;
    public void OnUsing()
    {
        isUsing =true;
        playerMoveCtrl.SetCanMove(!isUsing);
    }

    public void OnStopUsing()
    {
        isUsing = false;
        playerMoveCtrl.SetCanMove(!isUsing);
    }

    #endregion

    #region 触发

    private IUsable _usableItemInRange;

    public override string GetModelLayer()
    {
        return "Agent";
    }

    public override void OnColiderEnter(Collider other)
    {
        base.OnColiderEnter(other);
        if (YUtils.GetIUsableFromCollider(other, out var usable))
        {
            _usableItemInRange = usable;
        }
    }

    public override void OnColiderExit(Collider other)
    {
        base.OnColiderExit(other);
        if (YUtils.GetIUsableFromCollider(other, out var usable))
        {
            if (_usableItemInRange == usable)
            {
                _usableItemInRange = null;
            }
        }
    }

    #endregion
}