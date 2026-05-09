using System.Collections;
using System.Collections.Generic;
using Pathfinding;
using UnityEngine;
using UnityEngine.Events;

public class YAIPath : AIPath
{
    private UnityAction _onTargetReached;

    public void RigesterReached(UnityAction action)
    {
        _onTargetReached += action;
    }

    public void UnRigesterReached(UnityAction action)
    {
        _onTargetReached -= action;
    }
    public override void OnTargetReached()
    {
        base.OnTargetReached();
        _onTargetReached?.Invoke();
    }
}
