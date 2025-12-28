using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneResManager : LogicPluginBase
{
    public static SceneResManager Instance;
    
    public SceneResManager()
    {
        Instance = this;
    }
    
    List<ResEntity> resList = new List<ResEntity>();

    public void Init()
    {
        var root = GameObject.Find("ResRoot");
        var tasnforms = root.GetComponentsInChildren<CircleItemMarker>();
        resList.Clear();
        for (var i = 0; i < tasnforms.Length; i++)
        { 
            var res =ResEntity.pool.GetItem(tasnforms[i]);
            resList.Add(res);
        }
    }

    public bool GetNearestRes(Vector3 pos, float range, out ResEntity res)
    {
        for (var i = 0; i < resList.Count; i++)
        {
            if (Vector3.Distance(resList[i].ObjTrans.position, pos) < range)
            {
                res = resList[i];
                return true;
            }
        }
        res = null;
        return false;

    }

    public void RemoveRes(ResEntity resEntity)
    {
        resList.Remove(resEntity);
        ResEntity.pool.RecoverItem(resEntity);
    
    }
}