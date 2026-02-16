using UnityEngine;

public interface IYState
{
    string GetStateName();
    void EnterState(YStateMachine machine,object param);
    void UpdateState(YStateMachine machine, float dt);
    void ExitState(YStateMachine machine);
}
public class YStateMachine
{

    // 状态管理
    protected IYState currentState;
    protected IYState previousState;
    protected int stateMachineId = 0;
    private object lastParam;
    private object currentParam;
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
    public void SwitchState(IYState newState,object param)
    {
        if (currentState!=null&&currentState.GetStateName() == newState.GetStateName())
        {
            Debug.LogError(stateMachineId + "重复调用："+newState.GetStateName());
            return;
        }
        
        if (currentState != null)
        {
            previousState = currentState;
            currentState.ExitState(this);
            // Debug.Log(stateMachineId+"移除：" + currentState.GetStateName());
        }
        lastParam=currentParam;
        currentState = newState;
        currentParam=param;
        currentState.EnterState(this,currentParam);
 
         // Debug.Log(stateMachineId + "进入：" + currentState.GetStateName());
        
    }
    
    // 返回上一个状态
    public void ReturnToPreviousState()
    {
        if (previousState != null)
        {
            SwitchState(previousState,lastParam);
        }
    }
    
    // 获取当前状态名称（用于UI显示或调试）
    public string GetCurrentStateName()
    {
        return currentState?.GetStateName() ?? "No State";
    }
}