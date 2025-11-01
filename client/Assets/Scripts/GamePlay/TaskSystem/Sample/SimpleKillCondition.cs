// 文件名: SimpleKillCondition.cs
// 示例：一个简单的条件处理器，当怪物死亡时调用 TryApplyCondition 来推进相关任务
using UnityEngine;
using YOTO;

public class SimpleKillCondition : ITaskCondition
{

    // sourceId: 传入被击杀对象的 id（比如怪物类型 id）
    public bool TryApplyCondition(TaskInstance instance, string objectiveId, string sourceId,object[] param)
    {
        var def =YOTOFramework.taskMgr.GetDefinition(instance.taskId);
        var obj = def.objectives.Find(x => x.id == objectiveId);
        if (obj == null) return false;

        // 只处理 Kill 类型或 custom targetId 匹配
        if (obj.type != ObjectiveType.Kill) return false;
        if (!string.IsNullOrEmpty(obj.targetId) && obj.targetId != sourceId) return false;

        int cur = instance.GetProgress(objectiveId);

        if (param.Length > 0)
        {
            //取参数0
            int next = Mathf.Clamp(cur + (int)param[0], 0, obj.requiredAmount);
            if (next == cur) return false;
            instance.SetProgress(objectiveId, next);
        }

        return true;
    }
}