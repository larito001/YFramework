using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResEntity : ObjectBase, PoolItem<CircleItemMarker>,IUsable
{
    public static DataObjPool<ResEntity, CircleItemMarker> pool =
        new DataObjPool<ResEntity, CircleItemMarker>("ResEntity", 50);
    public int itemId =-1;
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

    public void SetData(CircleItemMarker serverData)
    {
        Location = serverData.transform.position;
        itemId=serverData.itemId;
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
        
        BagPlugin.Instance.AddItem(itemId,1);
        currentUser.OnStopUsing();
        currentUser = null;
        pool.RecoverItem(this);
    }
    
}
