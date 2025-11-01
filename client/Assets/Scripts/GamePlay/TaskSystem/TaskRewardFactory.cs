// 文件名: TaskRewardFactory.cs
using UnityEngine;

/// <summary>
/// 派发奖励
/// </summary>
public static class TaskRewardFactory
{
    public static void ApplyReward(TaskRewardDefinition def, TaskInstance inst)
    {
        switch (def.rewardId)
        {
            // case 0:
            //     break;
            // case 1:
            //     break;
            default:
                Debug.Log($"Unhandled reward {def.rewardId}");
                break;
        }
    }
}