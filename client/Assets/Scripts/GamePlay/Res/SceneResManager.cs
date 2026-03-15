using System.Collections.Generic;
using UnityEngine;

public class SceneResManager : IGameService
{
    public RewardBoxDataSO RewardDataSO;
    List<IUsable> resList = new List<IUsable>();

    BagSystem bagSystem;

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
        RewardDataSO = Resources.Load<RewardBoxDataSO>("Config/RewardBoxDataSO");
        resList.Clear();
        ResBoxEntity.Configure(ctx.Get<UIMgr>(), this);
    }

    public void Shutdown()
    {
    }
}
