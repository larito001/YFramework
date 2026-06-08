using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 万物之始
///
/// [DefaultExecutionOrder(1000)] 强制 LateUpdate 在所有其他 MonoBehaviour 之后跑：
/// CameraManager.LateTick 读 transform.position 需要看到本帧 view 已写完的最新位置，
/// 不然跑动时相机读上一帧位置 + CC.Move 物理抖动 → 相机抖（详见 CameraManager 类注释）。
/// </summary>
[DefaultExecutionOrder(1000)]
public sealed class GameLoop : MonoBehaviour
{
    public static GameLoop Instance { get; private set; }

    public GameContext Ctx { get; private set; }

    // 全局时间缩放源。gameplay 时钟 = unscaledDeltaTime × GlobalScale，不走 Unity Time.timeScale。
    // BuildContext 后缓存一次；取不到则退化为 scale=1（正常速度）。
    private TimeScaleService timeScale;

    [Header("Boot")] [SerializeField] private bool autoStart = true;
    [Header("测试模式")] public bool isTest = false;
    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        // 将帧率限制为60FPS
        Application.targetFrameRate = 60;

        // 早期催出 DOTween 单例。延迟到首次 tween 时 lazy-init 会在场景卸载阶段创建
        // [DOTween] GameObject，触发 Unity "Some objects were not cleaned up" 警告
        DOTween.Init();

        Ctx = GameBootstrapper.BuildContext(); // 组装所有系统
        Ctx.InitAll();
        Ctx.TryGet(out timeScale); // 缓存全局缩放源，驱动 gameplay 时钟
        if (autoStart)
        {
            GameBootstrapper.RunStartup(Ctx);
        }
    }

    /// <summary>本帧 gameplay dt：未缩放真实 dt × 全局缩放因子。
    /// 不读 Time.deltaTime（那会被 Time.timeScale 影响，而我们保持 timeScale=1）。</summary>
    private float GameDeltaTime => Time.unscaledDeltaTime * (timeScale != null ? timeScale.GlobalScale : 1f);

    private void Update() => Ctx.Tick(GameDeltaTime);
    // FixedTick 当前无任何 IFixedTickable 消费者；且 Unity 物理步长本就跟 Time.timeScale 走，
    // 不在此处缩放（保持真实 fixedDeltaTime）。将来若加自管物理模拟再按需折算 GlobalScale。
    private void FixedUpdate() => Ctx.FixedTick(Time.fixedDeltaTime);
    private void LateUpdate() => Ctx.LateTick(GameDeltaTime);
    
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Ctx?.ShutdownAll();
            // 杀光残余 tween + 清单例。配合 Awake 的 Init，DOTween 不再被动 lazy-init
            DOTween.KillAll();
            DOTween.Clear(true);
            Instance = null;
        }
    }
}
