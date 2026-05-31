using UnityEngine;

/// <summary>
/// 世界中的宝箱实例(挂在宝箱模型预制体上,如 ammo_box)。实现 <see cref="IInteractable"/>:
/// 玩家靠近显示提示,按 F 打开 <see cref="ChestPanel"/>。
///
/// **刷新策略**:本实例**首次**打开时 <see cref="ChestSystem.Roll"/> 一次,生成的网格存在 <see cref="rolledGrid"/>,
/// 整局保持(玩家拿走/放入都留在它上面);游戏重开 → 实例重建 → rolledGrid 为空 → 重新 roll。不写盘。
/// 通过 <see cref="GameLoop"/>.Instance.Ctx 取服务(场景 MonoBehaviour → service 的桥)。
/// </summary>
public class ChestEntity : MonoBehaviour, IInteractable
{
    [Tooltip("chest 配表里的宝箱 id")]
    public int chestId = 1;
    [Tooltip("交互范围(米)")]
    public float interactRange = 3f;

    private GridBag rolledGrid; // 首次打开生成,整局保持

    public Vector3 WorldPosition => transform.position;
    public float InteractRange => interactRange;
    public string Prompt => "按 F 打开宝箱";
    public bool CanInteract => true;

    private GameContext Ctx => GameLoop.Instance != null ? GameLoop.Instance.Ctx : null;

    // 在 Start 注册:GameLoop([DefaultExecutionOrder(1000)]) 的 Awake 早于任意 Start,
    // 故此时 Ctx 已就绪(若放 OnEnable 会因 GameLoop.Awake 尚未跑而拿不到 Ctx)。
    private void Start()
    {
        if (Ctx != null && Ctx.TryGet<WorldInteractionSystem>(out var s)) s.Register(this);
    }

    private void OnDestroy()
    {
        if (Ctx != null && Ctx.TryGet<WorldInteractionSystem>(out var s)) s.Unregister(this);
    }

    public void Interact()
    {
        var ctx = Ctx;
        if (ctx == null) return;
        if (!ctx.TryGet<ChestSystem>(out var chestSys)) return;
        if (!ctx.TryGet<UIMgr>(out var ui)) return;

        // 首次打开 roll 一次;之后复用同一网格(整局保持)
        if (rolledGrid == null)
        {
            rolledGrid = chestSys.Roll(chestId, 0);
            if (rolledGrid == null) return;
        }

        chestSys.SetCurrent(rolledGrid, chestId);
        ui.Show<ChestPanel>();
    }
}
