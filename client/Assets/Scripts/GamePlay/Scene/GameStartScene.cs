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
        CloseLobbyUI();           // 进入对局:关闭开始/装备等所有大厅菜单界面,避免遮住 HUD
        EnableCameraLook();       // 滑屏旋转相机 + 开枪抖动 + 瞄准变焦(先于 HUD,让 HUD 能取到瞄准机制)
        UI.Show<GameMainPanel>(); // 进入对局:显示打猎 HUD(瞄准/射击/积分/弹药)
        Context.Get<AnimalSystem>().SpawnWave(); // 在地面随机散布动物
        Context.Get<StoreMgr>().SaveAll(); // 游戏开始后写入进度:把当前进度(已扣体力等)整体落到激活槽,作为开局存档点
    }

    /// <summary>
    /// 关闭所有大厅/菜单界面。出发进入对局时,触发链路上的界面(开始 → 商店/准备 → 装备)
    /// 都还开着,只隐藏开始界面会让装备界面(全屏遮罩)继续盖在 HUD 上,所以这里逐一关闭。
    /// Hide 对未打开的界面是安全的空操作;Loading(RayCast 层)不在此列,仍由 HideLoading 收尾。
    /// </summary>
    private void CloseLobbyUI()
    {
        UI.Hide<StartPanel>();
        UI.Hide<SaveSlotPanel>();
        UI.Hide<EquipPanel>();
        UI.Hide<ShopPanel>();
        UI.Hide<SettingPanel>();
        UI.Hide<CodexPanel>();
        UI.Hide<TaskPanel>();
        UI.Hide<MapSelectPanel>();
        UI.Hide<FinishPanel>();
        UI.Hide<ConfirmPanel>();
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
        if (cam.GetComponent<ScopeAimController>() == null) cam.gameObject.AddComponent<ScopeAimController>(); // 瞄准变焦 + 命中射线

        // 第一人称手持武器:把出战武器模型挂到相机前下方,换装时自动更换
        var viewModel = cam.GetComponent<FpsWeaponViewModel>();
        if (viewModel == null) viewModel = cam.gameObject.AddComponent<FpsWeaponViewModel>();
        viewModel.Init(Context);
    }

    protected override void OnEnterScene()
    {
        EnterSceneComplete();
    }

    protected override void OnLeaveScene()
    {
        Context.Get<AnimalSystem>().Clear(); // 离开对局:清掉场上动物(它们不在场景 rootObj 下,不会随场景失活)
        LeaveSceneComplete();
    }
}
