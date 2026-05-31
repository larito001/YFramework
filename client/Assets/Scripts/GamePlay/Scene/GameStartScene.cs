using UnityEngine;
using YOTO;

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
        UI.Show<GameMainPanel>(); // 进入对局:显示打猎 HUD(瞄准/射击/积分/弹药)
        EnableCameraLook();       // 滑屏旋转相机
        Context.Get<AnimalSystem>().SpawnWave(); // 在地面随机散布动物(暂用箱子占位)
    }

    /// <summary>给主相机挂上滑屏环视控制(已挂则跳过)。无主相机时仅告警。</summary>
    private void EnableCameraLook()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[GameStartScene] 场景内没有 MainCamera,滑屏旋转相机未启用。");
            return;
        }
        if (cam.GetComponent<CameraSwipeLook>() == null) cam.gameObject.AddComponent<CameraSwipeLook>();
        if (cam.GetComponent<ShootCameraShake>() == null) cam.gameObject.AddComponent<ShootCameraShake>(); // 开枪抖动
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
