using UnityEngine;
using YOTO;

/// <summary>
/// 大厅/基地场景(<see cref="YSceneType.GamePlay"/>):对局结束「返回大厅」会切到这里。
/// 与对局场景 <see cref="GameStartScene"/>(Home)对称——这里收起战斗 HUD/结算、显示主界面 <see cref="StartPanel"/>;
/// 再点「准备→出发」会 SwitchScene 回 Home 重新进对局(同场景切换会被忽略,所以大厅必须是独立场景类型)。
/// </summary>
public class GameMainScene : YSceneBase
{
    public override YSceneType SceneType => YSceneType.GamePlay;

    public override string SceneName => "GameMainScene";

    protected override void OnLoadingEnd()
    {
        base.OnLoadingEnd();
        UI.Hide<GameMainPanel>(); // 收起战斗 HUD
        UI.Hide<FinishPanel>();   // 收起结算
        UI.Show<StartPanel>();    // 显示大厅主界面
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
