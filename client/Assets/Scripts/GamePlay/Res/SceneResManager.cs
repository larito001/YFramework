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
        var tasnforms = root.GetComponentsInChildren<Transform>();
        for (var i = 0; i < tasnforms.Length; i++)
        { 
            var res =ResEntity.pool.GetItem(tasnforms[i].position);
            resList.Add(res);
        }
    }

    public void GnerateResAt(Vector3 pos)
    {
    }
}