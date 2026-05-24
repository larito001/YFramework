using UnityEngine;
using YOTO;

public class GameMainScene : YSceneBase
{
    public override YSceneType SceneType => YSceneType.GamePlay;

    public override string SceneName => "GameMainScene";

    protected override void OnLoadingEnd()
    {
        base.OnLoadingEnd();
        // UI.Show<GameMainPanel>();
        // UI.Hide<StartPanel>();
        
        Res.LoadAsync<GameObject>();
        
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
