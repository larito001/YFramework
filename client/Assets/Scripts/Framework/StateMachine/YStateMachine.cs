using UnityEngine;

public interface IYState
{
    string GetStateName();
    void EnterState(YStateMachine enemy);
    void UpdateState(YStateMachine enemy, float dt);
    void ExitState(YStateMachine enemy);
}
public class YStateMachine
{

    // 状态管理
    protected IYState currentState;
    protected IYState previousState;

    public void ReSet()
    {
        currentState = null;
        previousState = null;
    }
    public void Update(float dt)
    {
        // 更新当前状态
        currentState?.UpdateState(this,dt);
    }
    
    // 切换状态
    public void SwitchState(IYState newState)
    {
        if (currentState!=null&&currentState.GetStateName() == newState.GetStateName())
        {
            Debug.LogError("重复调用："+newState.GetStateName());
            return;
        }
        
        if (currentState != null)
        {
            previousState = currentState;
            currentState.ExitState(this);
            Debug.LogError("移除：" + currentState.GetStateName());
        }
        
        currentState = newState;
        currentState.EnterState(this);
         Debug.LogError("进入：" + currentState.GetStateName());
        
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
        return currentState?.GetStateName() ?? "No State";
    }
}