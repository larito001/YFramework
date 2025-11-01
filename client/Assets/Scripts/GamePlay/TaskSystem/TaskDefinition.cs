// 文件名: TaskDefinition.cs
using System;
using System.Collections.Generic;
using UnityEngine;
// 文件名: TaskDefs.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public enum ObjectiveType { Kill, Collect, Talk, Visit, Custom }

[Serializable]
public class TaskObjective
{
    public string id; // 唯一标识（在任务内唯一，便于进度追踪）
    public ObjectiveType type;
    [TextArea] public string description;
    public int requiredAmount = 10;//目标进度
    // 可扩展字段（例如 target id/item id/位置 等）
    public string targetId; // 比如怪物ID/物品ID/NPC ID
}
[Serializable]
public class TaskRewardDefinition
{
    public int rewardId; 
    public int amount;
}

[CreateAssetMenu(menuName = "Tasks/TaskDefinition")]
public class TaskDefinition : ScriptableObject
{
    public string taskId;
    public string title;
    [TextArea] public string description;
    public List<TaskObjective> objectives = new List<TaskObjective>();
    // rewards/metadata
    public List<TaskRewardDefinition> rewards = new List<TaskRewardDefinition>();
    public bool repeatable = false;
}