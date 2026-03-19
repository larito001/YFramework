
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameStartScene : GotSceneBase
{
    public override GotSceneType SceneType
    {
        get { return GotSceneType.Home; }
    }

    public override string SceneName
    {
        get { return "GameStartScene"; }
    }

    protected override void OnLoadingEnd()
    {
        base.OnLoadingEnd();
        UI.Hide<StartPanel>();
    }

    protected override void OnEnterScene()
    {  
        EnterSceneComplete();
    }
    protected override void OnLeaveScene()
    {
        LeaveSceneComplete();
    }
}
