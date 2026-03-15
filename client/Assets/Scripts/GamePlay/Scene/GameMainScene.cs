using UnityEngine;
using YOTO;

public class GameMainScene : GotSceneBase
{
    public override GotSceneType SceneType => GotSceneType.GamePlay;

    public override string SceneName => "GameMainScene";

    protected override void OnLoadingEnd()
    {
        base.OnLoadingEnd();
        GameLoop.Instance.Ctx.Get<UIMgr>().Show(UIEnum.GameMainPanel);
        GameLoop.Instance.Ctx.Get<UIMgr>().Hide(UIEnum.StartPanel);
    }

    protected override void OnEnterScene()
    {
        var ctx = GameLoop.Instance.Ctx;
        ctx.Get<PlayerManager>().GeneratePlayer();
        EnterSceneComplete();
    }

    protected override void OnLeaveScene()
    {
        var ctx = GameLoop.Instance.Ctx;
        ctx.Get<PlayerManager>().ClearPlayer();
        LeaveSceneComplete();
    }
}
