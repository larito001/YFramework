// 文件名: TaskInstance.cs
using System;
using System.Collections.Generic;

[Serializable]
public class TaskInstance
{
    public string taskId;
    public bool isActive;
    public bool isCompleted;
    public Dictionary<string, int> objectiveProgress = new Dictionary<string,int>(); // objectiveId -> currentCount
    public DateTime startedAt;

    public TaskInstance(string id)
    {
        taskId = id;
        isActive = true;
        isCompleted = false;
        startedAt = DateTime.UtcNow;
    }

    public int GetProgress(string objectiveId)
    {
        if (objectiveProgress.TryGetValue(objectiveId, out var v)) return v;
        return 0;
    }

    public void SetProgress(string objectiveId, int val)
    {
        objectiveProgress[objectiveId] = val;
    }
}