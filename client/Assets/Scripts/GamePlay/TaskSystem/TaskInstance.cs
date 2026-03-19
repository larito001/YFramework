using System;
using System.Collections.Generic;

[Serializable]
public class TaskInstance
{
    [Serializable]
    public struct ObjectiveProgressEntry
    {
        public string objectiveId;
        public int value;
    }

    public string taskId;
    public bool isActive;
    public bool isCompleted;
    public long startedAtTicks;
    public List<ObjectiveProgressEntry> objectiveProgressEntries = new List<ObjectiveProgressEntry>();

    [NonSerialized]
    private Dictionary<string, int> objectiveProgress;

    public DateTime startedAt => startedAtTicks > 0
        ? new DateTime(startedAtTicks, DateTimeKind.Utc)
        : DateTime.UtcNow;

    public TaskInstance()
    {
        EnsureProgressMap();
    }

    public TaskInstance(string id)
    {
        taskId = id;
        isActive = true;
        isCompleted = false;
        startedAtTicks = DateTime.UtcNow.Ticks;
        EnsureProgressMap();
    }

    public int GetProgress(string objectiveId)
    {
        EnsureProgressMap();
        return objectiveProgress.TryGetValue(objectiveId, out var value) ? value : 0;
    }

    public void SetProgress(string objectiveId, int value)
    {
        EnsureProgressMap();
        objectiveProgress[objectiveId] = value;
        SyncEntries();
    }

    public void EnsureObjective(string objectiveId)
    {
        EnsureProgressMap();
        if (objectiveProgress.ContainsKey(objectiveId))
        {
            return;
        }

        objectiveProgress[objectiveId] = 0;
        SyncEntries();
    }

    public void OnBeforeSave()
    {
        SyncEntries();
    }

    public void OnAfterLoad()
    {
        objectiveProgress = null;
        EnsureProgressMap();
    }

    private void EnsureProgressMap()
    {
        if (objectiveProgress != null)
        {
            return;
        }

        objectiveProgress = new Dictionary<string, int>();
        for (int i = 0; i < objectiveProgressEntries.Count; i++)
        {
            var entry = objectiveProgressEntries[i];
            if (string.IsNullOrWhiteSpace(entry.objectiveId))
            {
                continue;
            }

            objectiveProgress[entry.objectiveId] = entry.value;
        }
    }

    private void SyncEntries()
    {
        objectiveProgressEntries.Clear();
        foreach (var kv in objectiveProgress)
        {
            objectiveProgressEntries.Add(new ObjectiveProgressEntry
            {
                objectiveId = kv.Key,
                value = kv.Value
            });
        }
    }
}
