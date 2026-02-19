using System.Collections.Generic;
using Combat;
using Unity.VisualScripting;
using UnityEngine;
using YOTO;

public class PlayerEntity : ObjectBase, PoolItem<PlayerManager>, IDamageable, IUser,IThreatTarget,ITickable
{
    
    public PlayerManager PlayerManager { get; private set; }
    public static DataObjPool<PlayerEntity, PlayerManager> pool =
        new DataObjPool<PlayerEntity, PlayerManager>("PlayerEntity", 4);

    // ThirdPlayerMoveCtrl playerMoveCtrl;
    public Properties properties;
    RateHud rateHud;
    AxeEntity axeEntity;
    GunEntity gunEntity;
    private IWeapon currentWeapon;
    private IUsable _usableItemInRange;
    private BasicBehavior _basicBehavior;
    #region 生命周期
    // public PetEntity pet;
    public void Tick(float dt)
    {
        if (ObjTrans == null) return;
        _basicBehavior.Tick(dt);
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (SceneResManager.Instance.GetNearestRes(ObjTrans.position, 3, out var res))
            {
                _usableItemInRange = res;
                res.OnUse(this);
            }
        }

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.S) ||
            Input.GetKeyDown(KeyCode.D) || Input.GetMouseButton(0))
        {
            if (_usableItemInRange != null)
            {
                _usableItemInRange.UnUse(this);
                _usableItemInRange = null;
            }
        }

        // if (Input.GetKeyDown(KeyCode.Q))
        // {
        //     currentWeapon.OnUnUse();
        //     var renderer = ObjTrans.GetComponentInChildren<PlayerRenderer>();
        //     if (currentWeapon is AxeEntity)
        //     {
        //         currentWeapon = gunEntity;
        //         renderer.UseAK();
        //     }
        //     else
        //     {
        //         currentWeapon = axeEntity;
        //         renderer.UseNife();
        //     }
        //
        //     currentWeapon.OnUse();
        // }

        if (Input.GetKeyDown(KeyCode.E))
        {
            // if (BagPlugin.Instance.GetItemNum(20002) > 0)
            // {
            //     BagPlugin.Instance.RemoveItem(20002, 1);
            //     properties.HP += 25;
            //     GameLoop.Instance.Ctx.Get<FlyTextMgr>().AddText("+" + 25, ObjTrans.position, FlyTextType.AddHP);
            //     rateHud.UpdateRate(properties.HP / properties.MaxHP);
            // }
        }
    }
    public void SetData(PlayerManager data)
    {
        PlayerManager=data;
        properties = new Properties();
        properties.HP = 100;
        properties.MaxHP = 100;
        properties.OnDead = () =>
        {
            //todo:玩家死亡
            axeEntity.OnDie();
            gunEntity.OnDie();
            // PlayerManager.Instance.PlayerDie();
        };
        properties.Camp = Camp.Player;
        SetInVision(true);
        SetPrefabBundlePath("Player/PlayerBase");
        InstanceGObj();
    }

    // PlayerRenderer renderer;

    protected override void AfterInstanceGObj()
    {
        properties.State = RoleState.Alive;
        _basicBehavior = new BasicBehavior(PlayerManager.CameraMgr);
       

        Animator anim = ObjTrans.GetComponentInChildren<Animator>();
        _basicBehavior.RigesterAnimator(anim);
        // var orbitCamera = YFramework.cameraMgr.getMainCamera().GetComponent<OrbitCamera>();
        // orbitCamera.Init(ObjTrans);
        // playerMoveCtrl = ObjTrans.GetComponent<ThirdPlayerMoveCtrl>();
        // playerMoveCtrl.playerInputSpace = orbitCamera.transform;
        rateHud = ObjTrans.GetComponentInChildren<RateHud>();
        rateHud.Reset();
        rateHud.Show();
        rateHud.UpdateRate(properties.HP / properties.MaxHP);

        // axeEntity = new AxeEntity();
        // // axeEntity.InitAex(objTrans.GetComponentInChildren<PlayerRenderer>().transform);
        // axeEntity.Location = new Vector3(0.25f, 0.5f, 0);
        // gunEntity = new GunEntity();
        // gunEntity.InitGun(objTrans);
        // gunEntity.Location = new Vector3(0.25f, 0.5f, 0);
        // currentWeapon = axeEntity;
        // renderer = ObjTrans.GetComponentInChildren<PlayerRenderer>();
        // renderer.UseNife();

        // pet = new PetEntity();
        // pet.Location = ObjTrans.position;
        // pet.PetInit();
    }

    protected override void BeforeRecover(bool isDelete)
    {
        properties.State = RoleState.Dead;
        gunEntity.RecoverObject();
        axeEntity.RecoverObject();
        // pet.RecoverObject();
        // pet = null;
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
        // if (objTrans != null)
        // {
        //     return renderer.transform.forward;
        // }

        return Vector3.down;
    }

    public void OnHurtSomeone()
    {
    }

    public void OnSlowDown(float rate)
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
        isUsing = true;
        // playerMoveCtrl.SetCanMove(!isUsing);
    }

    public void OnStopUsing()
    {
        isUsing = false;
        // playerMoveCtrl.SetCanMove(!isUsing);
    }

    #endregion

    #region 触发

    public override string GetModelLayer()
    {
        return "Agent";
    }

    public override void OnColiderEnter(Collider other)
    {
        // base.OnColiderEnter(other);
        // if (YUtils.GetIUsableFromCollider(other, out var usable))
        // {
        //     _usableItemInRange = usable;
        // }
    }

    public override void OnColiderExit(Collider other)
    {
        base.OnColiderExit(other);
        // if (YUtils.GetIUsableFromCollider(other, out var usable))
        // {
        //     if (_usableItemInRange == usable)
        //     {
        //         _usableItemInRange = null;
        //     }
        // }
    }

    #endregion


    public void OnMouseClick(Vector3 hitPoint, float dt)
    {
   
    }

    public void OnMouseUp()
    {
        if (ObjTrans != null)
        {
            // var renderer = ObjTrans.GetComponentInChildren<PlayerRenderer>();
            // renderer.SetAtacking(false);
        }
    }

    public TeamId Team { get; }
    public bool IsTargetable { get; }
    public bool IsAlive { get; }
    public Vector3 Position { get; }
    public Vector3 AimPoint { get; }
    public float ThreatRadius { get; }

    public bool ApplyDamage(in DamageSpec spec, in HitInfo hit, IProjectile instigator)
    {
        return true;
    }
    
}