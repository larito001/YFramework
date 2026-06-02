using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YOTO;

public class GameStartScene : YSceneBase
{
    private GameObject bloomVolumeGo; // 运行时建的全局 Bloom(让金色泛光动物真的泛起来),离开场景销毁
    private UniversalAdditionalCameraData bloomCamData; // 被开启后处理的相机数据,离场还原 renderPostProcessing
    private GameObject mapInstance; // 当前关卡的场景预制体实例(按 map 配表 scenePath 加载),离场销毁

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
        LoadMapScene();           // 按选中关卡加载场景预制体(地面/装饰),需先于刷怪——动物要贴在地面上
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
        UI.Hide<LeaderboardPanel>(); // 排行榜也是大厅面板:漏关会一直算"显示中",令 CombatInputGate 屏蔽滑屏环视
    }

    /// <summary>
    /// 按当前选中关卡(<see cref="MapSystem"/>)的 map 配表 scenePath 列加载场景预制体并实例化。
    /// 第一章沙漠 = Resources/Map/Chapter1_Desert(由 <c>Tools/TPS/Build Chapter1 Desert Map</c> 生成)。
    /// scenePath 为空(其余关卡暂未做场景)则跳过,仍用原本的空场景。离开对局时 <see cref="OnLeaveScene"/> 销毁。
    /// </summary>
    private void LoadMapScene()
    {
        if (mapInstance != null) { Object.Destroy(mapInstance); mapInstance = null; } // 再次出发时先清掉上一局的

        var maps = Context.Get<MapSystem>();
        var map = maps != null ? maps.Get(maps.SelectedMapId) : null;
        var path = map != null ? map.ScenePath : null;
        if (string.IsNullOrEmpty(path)) return;

        var prefab = Res.Load<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning($"[GameStartScene] 关卡场景预制体未找到(确认已在 Resources/ 下并已生成): {path}");
            return;
        }
        mapInstance = Object.Instantiate(prefab);
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
        EnableGoldenBloom(cam); // 开启 Bloom 后处理,让金色泛光动物的 HDR 自发光真正"泛光"

        // 第一人称手持武器:把出战武器模型挂到相机前下方,换装时自动更换
        var viewModel = cam.GetComponent<FpsWeaponViewModel>();
        if (viewModel == null) viewModel = cam.gameObject.AddComponent<FpsWeaponViewModel>();
        viewModel.Init(Context);
    }

    /// <summary>
    /// 开启相机后处理并建一个全局 Bloom override:场景默认没有后处理/Bloom,金色动物的 HDR 自发光不会泛光,
    /// 这里运行时补上。阈值取 1,只让 HDR(亮度&gt;1)亮部泛光,普通物体基本不糊;暖金色调让光晕偏金。
    /// 离开对局时由 <see cref="OnLeaveScene"/> 销毁。
    /// </summary>
    private void EnableGoldenBloom(Camera cam)
    {
        var camData = cam.GetUniversalAdditionalCameraData();
        if (camData != null)
        {
            camData.renderPostProcessing = true; // 相机开后处理(否则 Bloom 不生效)
            bloomCamData = camData;               // 记下来,离场把它关回(相机可能被别的场景复用)
        }

        if (bloomVolumeGo != null) return; // 已建过(再次出发不重复建)

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.overrideState = true; bloom.threshold.value = 1.0f;
        bloom.intensity.overrideState = true; bloom.intensity.value = 1.2f;
        bloom.scatter.overrideState   = true; bloom.scatter.value   = 0.7f;
        bloom.tint.overrideState      = true; bloom.tint.value      = new Color(1f, 0.9f, 0.6f); // 暖金光晕

        bloomVolumeGo = new GameObject("GoldenBloomVolume");
        var volume = bloomVolumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.profile = profile;
    }

    protected override void OnEnterScene()
    {
        EnterSceneComplete();
    }

    protected override void OnLeaveScene()
    {
        Context.Get<AnimalSystem>().Clear(); // 离开对局:清掉场上动物(它们不在场景 rootObj 下,不会随场景失活)
        if (mapInstance != null) { Object.Destroy(mapInstance); mapInstance = null; } // 销毁本局加载的关卡场景预制体
        if (bloomVolumeGo != null) // 收掉运行时建的全局 Bloom(连同 GO 一起销毁 profile,否则 SO 每局泄漏一份)
        {
            var v = bloomVolumeGo.GetComponent<Volume>();
            if (v != null && v.profile != null) Object.Destroy(v.profile);
            Object.Destroy(bloomVolumeGo);
            bloomVolumeGo = null;
        }
        if (bloomCamData != null) // 还原相机后处理开关(本场景为 Bloom 打开的,离场关回,避免被复用相机的场景误开)
        {
            bloomCamData.renderPostProcessing = false;
            bloomCamData = null;
        }
        LeaveSceneComplete();
    }
}
