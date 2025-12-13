using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResEntity : ObjectBase, PoolItem<Vector3>,IUsable
{
    public static DataObjPool<ResEntity, Vector3> pool =
        new DataObjPool<ResEntity, Vector3>("ResEntity", 50);

    public override string GetModelLayer()
    {
        return "Agent";
    }

    protected override void AfterInstanceGObj()
    {
        
    }

    protected override void BeforeRecover(bool isDelete)
    {
        
    }

    public void AfterIntoObjectPool()
    {
        RecoverObject();
    }

    public void SetData(Vector3 serverData)
    {
        Location = serverData;
        SetInVision(true);
        SetPrefabBundlePath("Res/Res");
        InstanceGObj();
    }

    IUser currentUser = null;

    public void OnUse(IUser  user)
    {
        if (currentUser == null)
        {
            currentUser=user;
            user.OnUsing();
            Timers.inst.Add(4,OnStop);
        }else if (currentUser == user)
        {
            Timers.inst.Remove(OnStop);
            currentUser.OnStopUsing();
            currentUser = null;
        }
     
    }

    public void OnStop(object o )
    {
        currentUser.OnStopUsing();
        currentUser = null;
        pool.RecoverItem(this);
    }
    
}
