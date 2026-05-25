using System;
using System.Collections.Generic;
using UnityEngine;

public class TaskManager : MonoBehaviour
{
    public List<TaskDefinition> allTaskDefinitions = new List<TaskDefinition>();

    private readonly Dictionary<string, TaskInstance> instances = new Dictionary<string, TaskInstance>();
    private readonly List<ITaskCondition> conditionHandlers = new List<ITaskCondition>();

    public event Action<TaskInstance> OnTaskStarted;
    public event Action<TaskInstance, string, int, int> OnTaskProgress;
    public event Action<TaskInstance> OnTaskCompleted;
    public event Action<TaskInstance> OnTaskUpdated;

    public void Unload()
    {
        SaveAll();
        conditionHandlers.Clear();
    }

    public List<TaskDefinition> GetAllTaskDefinitions() => allTaskDefinitions;
    public TaskDefinition GetDefinition(string id) => allTaskDefinitions.Find(t => t.taskId == id);

    public TaskInstance StartTask(string taskId, bool forceRestart = false)
    {
        var def = GetDefinition(taskId);
        if (def == null)
        {
            Debug.LogWarning($"TaskDef {taskId} not found");
            return null;
        }

        if (instances.TryGetValue(taskId, out var inst) && inst.isActive && !forceRestart)
        {
            return inst;
        }

        var newInst = new TaskInstance(taskId);
        foreach (var o in def.objectives)
        {
            newInst.EnsureObjective(o.id);
        }

        instances[taskId] = newInst;
        OnTaskStarted?.Invoke(newInst);
        SaveAll();
        return newInst;
    }

    public bool TryProgress(string taskId, string objectiveId, string sourceId = null, object[] param = null)
    {
        if (!instances.TryGetValue(taskId, out var inst) || inst.isCompleted)
        {
            return false;
        }

        var def = GetDefinition(taskId);
        if (def == null)
        {
            return false;
        }

        var obj = def.objectives.Find(x => x.id == objectiveId);
        if (obj == null)
        {
            return false;
        }

        bool changed = false;
        foreach (var handler in conditionHandlers)
        {
            if (handler.TryApplyCondition(inst, objectiveId, sourceId, param))
            {
                changed = true;
                break;
            }
        }

        if (changed)
        {
            OnTaskUpdated?.Invoke(inst);
            OnTaskProgress?.Invoke(inst, objectiveId, inst.GetProgress(objectiveId), obj.requiredAmount);
            CheckComplete(inst, def);
            SaveAll();
        }

        return changed;
    }

    public void RegisterConditionHandler(ITaskCondition handler)
    {
        if (!conditionHandlers.Contains(handler))
        {
            conditionHandlers.Add(handler);
        }
    }

    public void UnregisterConditionHandler(ITaskCondition handler)
    {
        conditionHandlers.Remove(handler);
    }

    public void SaveAll()
    {
        var sd = new SaveData();
        foreach (var kv in instances)
        {
            kv.Value.OnBeforeSave();
            sd.instances.Add(kv.Value);
        }

        string json = JsonUtility.ToJson(sd);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();
    }

    public void LoadAll()
    {
        if (!PlayerPrefs.HasKey(SAVE_KEY))
        {
            return;
        }

        string json = PlayerPrefs.GetString(SAVE_KEY);
        try
        {
            var sd = JsonUtility.FromJson<SaveData>(json);
            instances.Clear();
            foreach (var inst in sd.instances)
            {
                inst.OnAfterLoad();
                instances[inst.taskId] = inst;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("Failed load tasks: " + e.Message);
        }
    }

    private void Awake()
    {
        RegisterConditionHandler(new SimpleKillCondition(this));
        LoadAll();
    }

    private void OnDestroy()
    {
        SaveAll();
        conditionHandlers.Clear();
    }

    private void CheckComplete(TaskInstance inst, TaskDefinition def)
    {
        foreach (var o in def.objectives)
        {
            if (inst.GetProgress(o.id) < o.requiredAmount)
            {
                return;
            }
        }

        inst.isCompleted = true;
        inst.isActive = false;
        OnTaskCompleted?.Invoke(inst);
        ApplyRewards(def, inst);
        SaveAll();
    }

    private void ApplyRewards(TaskDefinition def, TaskInstance inst)
    {
        foreach (var r in def.rewards)
        {
            TaskRewardFactory.ApplyReward(r, inst);
        }
    }

    private const string SAVE_KEY = "TASK_MANAGER_SAVE_V1";

    [Serializable]
    private class SaveData
    {
        public List<TaskInstance> instances = new List<TaskInstance>();
    }
}
