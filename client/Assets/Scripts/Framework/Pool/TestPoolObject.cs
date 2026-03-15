using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestPoolObject : ObjectBase,PoolItem<Vector3>
{
   public static  DataObjPool<TestPoolObject,Vector3> pool=new DataObjPool<TestPoolObject, Vector3>("poolName", 20);
    public void AfterIntoObjectPool()
    {
        
    }

    public void SetData(Vector3 serverData)
    {
        SetInVision(true);
        SetPrefabBundlePath("Assets/PolygonApocalypse/Prefabs/Characters/SM_Chr_Teen_Male_01.prefab");
    }



    protected override void AfterInstanceGObj()
    {
        
    }

    protected override void BeforeRecover(bool isDelete)
    {
        
    }
}
