using UnityEngine;

/// <summary>
/// 万物之始。Unity 入口：创建 GameContext，触发各 Mgr 装配。
/// 不再做 Tick / FixedTick / LateTick 转发——各 Mgr 自己用 Unity 原生
/// Awake / Update / FixedUpdate / LateUpdate / OnDestroy。
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
        Application.targetFrameRate = 60;

        Ctx = new GameContext();
        GameBootstrapper.BuildContext(this, Ctx);

        if (autoStart)
        {
            GameBootstrapper.RunStartup(Ctx);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
