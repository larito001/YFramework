using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 万物之始
/// </summary>
public sealed class GameLoop : MonoBehaviour
{
    public static GameLoop Instance { get; private set; }

    public GameContext Ctx { get; private set; }

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
        if (autoStart)
        {
            GameBootstrapper.RunStartup(Ctx);
        }
    }

    private void Update() => Ctx.Tick(Time.deltaTime);
    private void FixedUpdate() => Ctx.FixedTick(Time.fixedDeltaTime);
    private void LateUpdate() => Ctx.LateTick(Time.deltaTime);
    
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
