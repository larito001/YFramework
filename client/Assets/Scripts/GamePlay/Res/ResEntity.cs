using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResEntity : ObjectBase, PoolItem<Vector3>
{
    public static DataObjPool<ResEntity, Vector3> pool =
        new DataObjPool<ResEntity, Vector3>("ResEntity", 50);
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
}
