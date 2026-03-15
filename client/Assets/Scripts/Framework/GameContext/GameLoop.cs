using System;
using NoSLoofah.BuffSystem;
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
    [Header("buffCollection")] public BuffCollection buffCollection;
    [Header("BuffTagCollector")] public BuffTagData buffData;
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
        Ctx = GameBootstrapper.BuildContext(); // 组装所有系统
        Ctx.InitAll();

        if (autoStart)
        {
            GameLoop.Instance.Ctx.Get<UIMgr>().Show(UIEnum.StartPanel);
       
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
            Instance = null;
        }
    }
}