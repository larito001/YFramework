using System;
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
    public RewardBoxDataSO RewardDataSO;
    List<IUsable> resList = new List<IUsable>();

    public override void ReStartGame(Action callBack = null)
    {
        RewardDataSO = Resources.Load<RewardBoxDataSO>("Config/RewardBoxDataSO");
        // var root = GameObject.Find("ResRoot");
        // var tasnforms = root.GetComponentsInChildren<CircleItemMarker>();
        // foreach (var resEntity in resList)
        // {
        //      ResEntity.pool.RecoverItem(resEntity);
        // }
        resList.Clear();
        // ResBoxInfo boxInfo = new ResBoxInfo();
        // boxInfo.id = 101;
        // boxInfo.pos = GameStarter.PlayerOrgPos.position;
        // var box = ResBoxEntity.pool.GetItem(boxInfo);
        // resList.Add(box);
        // for (var i = 0; i < tasnforms.Length; i++)
        // { 
        //     var res =ResEntity.pool.GetItem(tasnforms[i]);
        //     
        // }
        base.ReStartGame(callBack);
    }


    public bool GetNearestRes(Vector3 pos, float range, out IUsable res)
    {
        for (var i = 0; i < resList.Count; i++)
        {
            if (Vector3.Distance(resList[i].GetPosition(), pos) < range)
            {
                res = resList[i];
                return true;
            }
        }

        res = null;
        return false;
    }

    // public void RemoveRes(ResEntity resEntity)
    // {
    //     resList.Remove(resEntity);
    //     ResEntity.pool.RecoverItem(resEntity);
    // }
}