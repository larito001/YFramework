using System.Collections.Generic;
using UnityEngine;
using YOTO;

public class SceneResManager : IGameService
{
  
    private readonly List<IUsable> resList = new List<IUsable>();

    private IUIService uiMgr;
   

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

    public void Init(GameContext ctx)
    {
      
        resList.Clear();
        uiMgr = ctx.Get<UIMgr>();
    }

    public void Shutdown()
    {
        uiMgr = null;

        resList.Clear();
    }

    public ResBoxEntity CreateResBox(ResBoxInfo info)
    {
        var entity = ResBoxEntity.pool.GetItem(info);
        entity.ConfigureRuntime(uiMgr, this);
        return entity;
    }
}
