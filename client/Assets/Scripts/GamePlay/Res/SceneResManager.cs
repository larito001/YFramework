using System.Collections.Generic;
using UnityEngine;
using YOTO;

public class SceneResManager : IGameService
{
    public RewardBoxDataSO RewardDataSO;
    private readonly List<IUsable> resList = new List<IUsable>();

    private IUIService uiMgr;
    private ResourceHandle<RewardBoxDataSO> rewardDataHandle;

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
        rewardDataHandle = ctx.Get<ResMgr>().LoadHandle<RewardBoxDataSO>("Config/RewardBoxDataSO");
        RewardDataSO = rewardDataHandle?.Asset;
        resList.Clear();
        uiMgr = ctx.Get<UIMgr>();
    }

    public void Shutdown()
    {
        uiMgr = null;
        rewardDataHandle?.Release();
        rewardDataHandle = null;
        RewardDataSO = null;
        resList.Clear();
    }

    public ResBoxEntity CreateResBox(ResBoxInfo info)
    {
        var entity = ResBoxEntity.pool.GetItem(info);
        entity.ConfigureRuntime(uiMgr, this);
        return entity;
    }
}
