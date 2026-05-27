using UnityEngine;

/// <summary>
/// 相机服务：组合 Rig / Shake / FreeLook / Aim 四个子模块。
/// Rig 负责主相机与 CinemachineBrain；Shake 负责震屏；FreeLook 与 Aim 负责两套 Cinemachine 虚拟相机。
/// </summary>
public class CameraManager : IGameService, ITickable
{
    public CameraRig Rig { get; }
    public CameraShake Shake { get; }
    public FreeLookCam FreeLook { get; }
    public AimCam Aim { get; }

    public bool UseVirtualCamera { get; set; }
    public Camera MainCamera => Rig.MainCamera;

    /// <summary>跟随目标（俯视角玩家）。SetFollow 设进来。Cinemachine 启用时不生效，由虚拟相机接管。</summary>
    public Transform FollowTarget { get; private set; }
    /// <summary>相机相对玩家的偏移，默认顶视稍倾后角（60° 俯角）。</summary>
    public Vector3 FollowOffset = new Vector3(0f, 12f, -7f);
    /// <summary>跟随的指数平滑系数，值越大越紧跟。</summary>
    public float FollowSmoothing = 12f;

    /// <summary>相机 forward 投到水平面，作为 WASD 前向参考。相机几近垂直俯视时退化到世界 +Z。</summary>
    public Vector3 PlanarForward
    {
        get
        {
            var t = Rig != null ? Rig.Transform : null;
            if (t == null) return Vector3.forward;
            var f = t.forward; f.y = 0f;
            return f.sqrMagnitude < 1e-4f ? Vector3.forward : f.normalized;
        }
    }

    /// <summary>相机 right 投到水平面，作为 WASD 右向参考。</summary>
    public Vector3 PlanarRight
    {
        get
        {
            var t = Rig != null ? Rig.Transform : null;
            if (t == null) return Vector3.right;
            var r = t.right; r.y = 0f;
            return r.sqrMagnitude < 1e-4f ? Vector3.right : r.normalized;
        }
    }

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
        UpdateFollow(dt);
    }

    /// <summary>把玩家 transform 设为跟随目标，并把相机瞬移到位避免第一帧拉扯。</summary>
    public void SetFollow(Transform target)
    {
        FollowTarget = target;
        var t = Rig != null ? Rig.Transform : null;
        if (target != null && t != null)
        {
            t.position = target.position + FollowOffset;
            t.LookAt(target.position, Vector3.up);
        }
    }

    private void UpdateFollow(float dt)
    {
        if (UseVirtualCamera) return; // Cinemachine 接管时不动主相机
        if (FollowTarget == null) return;
        var t = Rig != null ? Rig.Transform : null;
        if (t == null) return;

        var desired = FollowTarget.position + FollowOffset;
        var k = 1f - Mathf.Exp(-FollowSmoothing * dt); // 指数平滑，与帧率无关
        t.position = Vector3.Lerp(t.position, desired, k);
        t.LookAt(FollowTarget.position, Vector3.up);
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
