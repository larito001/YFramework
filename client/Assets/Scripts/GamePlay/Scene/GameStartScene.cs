
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameStartScene : YSceneBase
{
    public override YSceneType SceneType
    {
        get { return YSceneType.Home; }
    }

    public override string SceneName
    {
        get { return "GameStartScene"; }
    }

    protected override void OnLoadingEnd()
    {
        base.OnLoadingEnd();
        UI.Hide<StartPanel>();      
        var manager = Context.Get<CharacterManager>();
        manager.GenneratePlayer();
        var bag = Context.Get<BagSystem>();
        bag.AddItem(1001, 5);   // 5个小药水
        bag.AddItem(2001, 1);   // 铁剑
        UI.Show<BagPanel>();
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
