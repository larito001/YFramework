using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RunPhaseMachine : YStateMachine,IGameService,ITickable
{
    public void Init(GameContext ctx)
    {
        ReSet();
    }

    public void Shutdown()
    {
        ReSet();
    }

    public void Tick(float dt)
    {
        Update(dt);
    }
}
