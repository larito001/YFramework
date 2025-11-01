
/// <summary>
/// 判断任务是否完成的判断器
/// </summary>
public interface ITaskCondition
{
    // 当玩家发生可能影响任务进度的事件时调用（例如击杀、拾取、对话、触发点）
    // 返回：该事件是否让目标进度变化（true 表示进度改变）
    bool TryApplyCondition(TaskInstance instance, string objectiveId, string sourceId, object [] param);
}
