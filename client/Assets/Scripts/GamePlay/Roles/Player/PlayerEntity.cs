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
    
    RateHud rateHud;
    AxeEntity axeEntity;
    GunEntity gunEntity;
    private IWeapon currentWeapon;
    private BasicBehavior _basicBehavior;
    #region 生命周期
    
    
    public void Tick(float dt)
    {
        if (ObjTrans == null) return;
        _basicBehavior.Tick(dt);
        
    }
    public void SetData(PlayerManager data)
    {
        PlayerManager=data;
        SetInVision(true);
        SetPrefabBundlePath("Player/PlayerBase");
        InstanceGObj();
    }
    

    protected override void AfterInstanceGObj()
    {
        _basicBehavior = new BasicBehavior(PlayerManager.CameraMgr);
       

        Animator anim = ObjTrans.GetComponentInChildren<Animator>();
        _basicBehavior.RigesterAnimator(anim);
        rateHud = ObjTrans.GetComponentInChildren<RateHud>();
        rateHud.Reset();
        rateHud.Show();
        rateHud.UpdateRate(Hp/MaxHP);
    }

    protected override void BeforeRecover(bool isDelete)
    {
        gunEntity.RecoverObject();
        axeEntity.RecoverObject();
    }

    public void AfterIntoObjectPool()
    {
        RecoverObject();
    }

    #endregion
    #region IUser

    private bool isUsing = false;

    public void OnUsing()
    {
        isUsing = true;
    }

    public void OnStopUsing()
    {
        isUsing = false;
    }

    #endregion

    #region 属性

    private TeamId _team = new TeamId(0);
    private bool _isTargetable = false;
    private bool _isAlive = false;
    private float _maxHP=100;
    private float _hp=100;
    private float _atk=10;
    private float _def=10;
    private int _level=1;
    
    public TeamId Team
    {
        get { return _team; }
    }
    public bool IsTargetable { get { return _isTargetable; } }
    public bool IsAlive { get{ return _isAlive;} }

    public Vector3 Position
    {
        get
        {
            if (ObjTrans != null)
            {
                return ObjTrans.position;
            }
            else
            {
                return Location;
            }
        }
    }

    public Vector3 AimPoint
    {
        get
        {
            return _basicBehavior.AimPointWorld;
        }
    }

    public float MaxHP
    {
        get { return _maxHP; }
    }
    public float Hp { get{ return _hp;} }
    
    public float Atk { get{ return _atk;} }
    public float Def { get{ return _def;} }
    public int Level { get{ return _level;} }

    public bool ApplyDamage(in DamageSpec spec, in HitInfo hit, IProjectile instigator)
    {
        return true;
    }

    #endregion

    
}