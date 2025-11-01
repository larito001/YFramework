// 文件名: TaskManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json; // 如果不想使用 Newtonsoft，请使用 Unity JsonUtility（示例中用 JsonUtility）

public class TaskManager 
{

    // 所有任务定义（通过资源管理/拖拽到 inspector 或运行时加载）
    public List<TaskDefinition> allTaskDefinitions = new List<TaskDefinition>();

    // 运行时任务实例（player 当前激活或已完成的）
    private Dictionary<string, TaskInstance> instances = new Dictionary<string, TaskInstance>();

    // Registered condition handlers keyed by ObjectiveType or custom keys
    private List<ITaskCondition> conditionHandlers = new List<ITaskCondition>();

    // Events
    public event Action<TaskInstance> OnTaskStarted;
    public event Action<TaskInstance, string, int, int> OnTaskProgress; // instance, objectiveId, current, required
    public event Action<TaskInstance> OnTaskCompleted;
    public event Action<TaskInstance> OnTaskUpdated;

    public void Init()
    {
        //注册任务判断方法
        RegisterConditionHandler(new SimpleKillCondition());
        LoadAll();
    }

    public void Unload()
    {
        
        SaveAll();
        conditionHandlers.Clear();
    }

    #region Task lifecycle
    
    public List<TaskDefinition> GetAllTaskDefinitions() => allTaskDefinitions;
    public TaskDefinition GetDefinition(string id) => allTaskDefinitions.Find(t => t.taskId == id);
    public TaskInstance StartTask(string taskId, bool forceRestart=false)
    {
        var def = GetDefinition(taskId);
        if (def == null) { Debug.LogWarning($"TaskDef {taskId} not found"); return null; }

        if (instances.TryGetValue(taskId, out var inst) && inst.isActive && !forceRestart) {
            return inst;
        }

        var newInst = new TaskInstance(taskId);
        // init objective progress
        foreach (var o in def.objectives) newInst.objectiveProgress[o.id] = 0;

        instances[taskId] = newInst;
        OnTaskStarted?.Invoke(newInst);
        SaveAll(); // lightweight save
        return newInst;
    }

    /// <summary>
    /// 触发
    /// </summary>
    /// <param name="taskId">任务id</param>
    /// <param name="objectiveId">任务内目标id</param>
    /// <param name="sourceId">触发事件来源</param>
    /// <param name="addProgress">增加的进度</param>
    /// <returns></returns>
    public bool TryProgress(string taskId, string objectiveId, string sourceId=null, object[] param =null)
    {
        if (!instances.TryGetValue(taskId, out var inst)) return false;
        if (inst.isCompleted) return false;

        var def = GetDefinition(taskId);
        if (def == null) return false;
        var obj = def.objectives.Find(x => x.id == objectiveId);
        if (obj == null) return false;

        // Try apply via registered handlers first (gives extensibility)
        bool changed = false;
        foreach (var h in conditionHandlers)
        {
            if (h.TryApplyCondition(inst, objectiveId, sourceId, param))
            {
                changed = true;
                break; // 可改为不短路，取决于设计
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

    private void CheckComplete(TaskInstance inst, TaskDefinition def)
    {
        foreach (var o in def.objectives)
        {
            if (inst.GetProgress(o.id) < o.requiredAmount) return;
        }
        // all satisfied
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
            // 简单工厂或通过注册的奖励实现解析 payload
            TaskRewardFactory.ApplyReward(r, inst);
        }
    }
    
    
    
    #endregion

    #region 事件注册
    public void RegisterConditionHandler(ITaskCondition handler)
    {
        if (!conditionHandlers.Contains(handler)) conditionHandlers.Add(handler);
    }

    public void UnregisterConditionHandler(ITaskCondition handler)
    {
        conditionHandlers.Remove(handler);
    }
    #endregion

    #region Save / Load (轻量示例)
    private const string SAVE_KEY = "TASK_MANAGER_SAVE_V1";
    [Serializable]
    class SaveData { public List<TaskInstance> instances = new List<TaskInstance>(); }

    public void SaveAll()
    {
        var sd = new SaveData();
        foreach (var kv in instances) sd.instances.Add(kv.Value);
        string json = JsonUtility.ToJson(sd);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();
    }

    public void LoadAll()
    {
        if (!PlayerPrefs.HasKey(SAVE_KEY)) return;
        string json = PlayerPrefs.GetString(SAVE_KEY);
        try
        {
            var sd = JsonUtility.FromJson<SaveData>(json);
            instances.Clear();
            foreach (var inst in sd.instances) instances[inst.taskId] = inst;
        }
        catch (Exception e)
        {
            Debug.LogWarning("Failed load tasks: " + e.Message);
        }
    }
    #endregion


}
