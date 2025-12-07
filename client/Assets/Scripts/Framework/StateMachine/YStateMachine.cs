
public interface IYState
{
    void EnterState(YStateMachine enemy);
    void UpdateState(YStateMachine enemy);
    void ExitState(YStateMachine enemy);
}
public class YStateMachine
{

    // 状态管理
    private IYState currentState;
    private IYState previousState;
    
    void Update()
    {
        // 更新当前状态
        currentState?.UpdateState(this);
    }
    
    // 切换状态
    public void SwitchState(IYState newState)
    {
        if (currentState != null)
        {
            previousState = currentState;
            currentState.ExitState(this);
        }
        
        currentState = newState;
        currentState.EnterState(this);
    }
    
    // 返回上一个状态
    public void ReturnToPreviousState()
    {
        if (previousState != null)
        {
            SwitchState(previousState);
        }
    }
    
    // 获取当前状态名称（用于UI显示或调试）
    public string GetCurrentStateName()
    {
        return currentState?.GetType().Name ?? "No State";
    }
}