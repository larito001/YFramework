using UnityEngine;

/// <summary>
/// 相机服务：组合 Rig / Shake / FreeLook / Aim 四个子模块。
/// Rig 负责主相机与 CinemachineBrain；Shake 负责震屏；FreeLook 与 Aim 负责两套 Cinemachine 虚拟相机。
/// 场景交互（点击/拖拽/悬停）见 <see cref="SceneInteractionService"/>。
/// </summary>
public class CameraManager : IGameService, ITickable
{
    public CameraRig Rig { get; }
    public CameraShake Shake { get; }
    public FreeLookCam FreeLook { get; }
    public AimCam Aim { get; }

    public bool UseVirtualCamera { get; set; }
    public Camera MainCamera => Rig.MainCamera;

    private SceneReferenceService sceneReferenceService;

    public CameraManager()
    {
        Rig = new CameraRig();
        Shake = new CameraShake();
        FreeLook = new FreeLookCam();
        Aim = new AimCam();
    }

    public void Init(GameContext ctx)
    {
        sceneReferenceService = ctx.Get<SceneReferenceService>();

        if (!Rig.Resolve(sceneReferenceService))
        {
            Debug.LogError($"{SceneRefKeys.MainCamera} was not found or does not have a Camera component.");
            return;
        }

        if (UseVirtualCamera)
        {
            Rig.EnableVirtualPipeline();
        }

        Shake.Bind(Rig.Transform);

        FreeLook.Resolve(sceneReferenceService);
        Aim.Resolve(sceneReferenceService);
    }

    public void Tick(float dt)
    {
        Shake.Tick(dt);
    }

    public void Shutdown()
    {
        Aim.Reset();
        FreeLook.Reset();
        Shake.Unbind();
        Rig.Reset();
        sceneReferenceService = null;
    }
}
