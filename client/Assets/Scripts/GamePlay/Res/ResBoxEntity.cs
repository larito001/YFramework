using System.Collections.Generic;
using UnityEngine;

public struct ResBoxInfo
{
    public int id;
    public Vector3 pos;
}

public class ResBoxEntity : ObjectBase, PoolItem<ResBoxInfo>, IUsable
{
    public static DataObjPool<ResBoxEntity, ResBoxInfo> pool =
        new DataObjPool<ResBoxEntity, ResBoxInfo>("ResBoxEntity", 50);

    private readonly List<Vector2Int> rewardList = new List<Vector2Int>();
    private IUIService uiMgr;
    private SceneResManager sceneResManager;
    private ResBoxInfo? pendingInfo;
    public int boxId;

    public void ConfigureRuntime(IUIService manager, SceneResManager managerOwner)
    {
        uiMgr = manager;
        sceneResManager = managerOwner;
        TryBuildRewards();
    }

    public void OnUse(IUser user)
    {
        uiMgr?.Show<SearchPanel>(rewardList);
    }

    public Vector3 GetPosition()
    {
        return ObjTrans != null ? ObjTrans.position : Location;
    }

    public void UnUse(IUser user)
    {
    }

    protected override void AfterInstanceGObj()
    {
    }

    protected override void BeforeRecover(bool isDelete)
    {
    }

    public void AfterIntoObjectPool()
    {
    }

    public void SetData(ResBoxInfo serverData)
    {
        pendingInfo = serverData;
        boxId = serverData.id;
        Location = serverData.pos;
        SetInVision(true);
        SetPrefabBundlePath("Res/ResBox");
        InstanceGObj();
        TryBuildRewards();
    }

    private void TryBuildRewards()
    {
        if (!pendingInfo.HasValue || sceneResManager?.RewardDataSO == null)
        {
            return;
        }

        rewardList.Clear();
        boxId = pendingInfo.Value.id;
        var data = sceneResManager.RewardDataSO.rewardDatas;
        foreach (var rewardBoxData in data)
        {
            if (boxId != rewardBoxData.RewardId)
            {
                continue;
            }

            var number = Random.Range(rewardBoxData.MinNumber, rewardBoxData.MaxNumber);
            List<int> rewardIds = new List<int>();
            List<int> weights = new List<int>();
            int totalWeight = 0;

            foreach (var vector2Int in rewardBoxData.Rewards)
            {
                rewardIds.Add(vector2Int.x);
                weights.Add(vector2Int.y);
                totalWeight += vector2Int.y;
            }

            for (int i = 0; i < number; i++)
            {
                int randomValue = Random.Range(0, totalWeight);
                int currentWeight = 0;

                for (int j = 0; j < weights.Count; j++)
                {
                    currentWeight += weights[j];
                    if (randomValue < currentWeight)
                    {
                        rewardList.Add(new Vector2Int(rewardIds[j], 1));
                        break;
                    }
                }
            }
        }

        pendingInfo = null;
    }
}
