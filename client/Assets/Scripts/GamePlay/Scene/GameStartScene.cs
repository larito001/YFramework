
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
        bag.AddItem(2001);  // 铁剑 1×3
        bag.AddItem(2002);  // 铁甲 2×2
        bag.AddItem(1002);  // 大药水 1×2
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
