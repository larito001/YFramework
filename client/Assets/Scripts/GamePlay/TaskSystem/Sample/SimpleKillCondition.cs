using UnityEngine;

public class SimpleKillCondition : ITaskCondition
{
    private readonly TaskManager taskManager;

    public SimpleKillCondition(TaskManager taskManager)
    {
        this.taskManager = taskManager;
    }

    public bool TryApplyCondition(TaskInstance instance, string objectiveId, string sourceId, object[] param)
    {
        var def = taskManager.GetDefinition(instance.taskId);
        var obj = def.objectives.Find(x => x.id == objectiveId);
        if (obj == null) return false;

        if (obj.type != ObjectiveType.Kill) return false;
        if (!string.IsNullOrEmpty(obj.targetId) && obj.targetId != sourceId) return false;

        int cur = instance.GetProgress(objectiveId);

        if (param.Length > 0)
        {
            int next = Mathf.Clamp(cur + (int)param[0], 0, obj.requiredAmount);
            if (next == cur) return false;
            instance.SetProgress(objectiveId, next);
        }

        return true;
    }
}
