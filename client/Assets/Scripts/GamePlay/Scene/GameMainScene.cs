using UnityEngine;
using YOTO;

public class GameMainScene : GotSceneBase
{
    public override GotSceneType SceneType => GotSceneType.GamePlay;

    public override string SceneName => "GameMainScene";

    protected override void OnLoadingEnd()
    {
        base.OnLoadingEnd();
        GetService<UIMgr>().Show(UIEnum.GameMainPanel);
        GetService<UIMgr>().Hide(UIEnum.StartPanel);
    }

    protected override void OnEnterScene()
    {
        GetService<PlayerManager>().GeneratePlayer();
        EnterSceneComplete();
    }

    protected override void OnLeaveScene()
    {
        GetService<PlayerManager>().ClearPlayer();
        LeaveSceneComplete();
    }
}
