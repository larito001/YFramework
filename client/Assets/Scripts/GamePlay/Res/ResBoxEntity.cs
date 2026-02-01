using System.Collections.Generic;
using UnityEngine;
using YOTO;

public struct ResBoxInfo
{
    public int id;
    public Vector3 pos;
}

public class ResBoxEntity : ObjectBase, PoolItem<ResBoxInfo>, IUsable
{
    public static DataObjPool<ResBoxEntity, ResBoxInfo> pool =
        new DataObjPool<ResBoxEntity, ResBoxInfo>("ResBoxEntity", 50);
    List<Vector2Int> rewardList = new List<Vector2Int>();
    public int boxId;
    public void OnUse(IUser user)
    {
        YFramework.uIMgr.Show(UIEnum.SearchPanel, rewardList);
    }

    public Vector3 GetPosition()
    {
        if(ObjTrans!=null)
        return ObjTrans.position;
        else
        {
            return Location;
        }
    }

    public void UnUse(IUser user)
    {
      
    }

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
 
    }

    public void SetData(ResBoxInfo serverData)
    {
        boxId=serverData.id;
        Location = serverData.pos;
        SetInVision(true);
        SetPrefabBundlePath("Res/ResBox");
        InstanceGObj();
        var data = SceneResManager.Instance.RewardDataSO.rewardDatas;
        
        foreach (var rewardBoxData in data)
        {
            if (boxId == rewardBoxData.RewardId)
            {
                var number = Random.Range(rewardBoxData.MinNumber, rewardBoxData.MaxNumber);
    
                // 1. 将奖励和权重分离到两个列表中
                List<int> rewardIds = new List<int>();
                List<int> weights = new List<int>();
                int totalWeight = 0;
    
                foreach (var vector2Int in rewardBoxData.Rewards)
                {
                    // x是奖励ID，y是权重
                    rewardIds.Add(vector2Int.x);
                    weights.Add(vector2Int.y);
                    totalWeight += vector2Int.y;
                }
    
                // 2. 根据总权重和数量，随机选择奖励
                List<Vector2Int> selectedRewards = new List<Vector2Int>();
    
                for (int i = 0; i < number; i++)
                {
                    // 生成一个随机数
                    int randomValue = Random.Range(0, totalWeight);
                    int currentWeight = 0;
        
                    // 根据权重随机选择
                    for (int j = 0; j < weights.Count; j++)
                    {
                        currentWeight += weights[j];
                        if (randomValue < currentWeight)
                        {
                            selectedRewards.Add(new Vector2Int(rewardIds[j],1));
                            break;
                        }
                    }
                }
    
                // 3. 将选中的奖励添加到奖励列表
                rewardList.AddRange(selectedRewards);
            }
            
            // rewardList.Add(); 
        }
 
    }
}
