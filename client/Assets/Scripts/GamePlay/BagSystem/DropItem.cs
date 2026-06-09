using UnityEngine;

/// <summary>
/// 世界中的掉落物(丢弃物品时由 <see cref="DropItemSystem"/> 生成,挂在掉落模型上)。
/// 实现 <see cref="IInteractable"/>:玩家靠近显示提示,按 F 捡起 → 放回背包。
/// 背包放得下则销毁本体;放不下则把剩余数量留在地上(更新 <see cref="count"/>)。
///
/// 复用宝箱同一套世界交互框架(<see cref="WorldInteractionSystem"/> 做靠近检测 + F 触发),
/// 注册/注销时机与 <see cref="ChestEntity"/> 一致。
/// </summary>
public class DropItem : MonoBehaviour, IInteractable
{
    private int itemId;
    private int count;
    private int rotation;        // 丢弃时的朝向(预留:捡回时尽量保持原朝向)
    private float interactRange = 2f;
    private string displayName = "物品";

    public Vector3 WorldPosition => transform.position;
    public float InteractRange => interactRange;
    public string Prompt => count > 1 ? $"按 F 捡起 {displayName} x{count}" : $"按 F 捡起 {displayName}";
    public bool CanInteract => true;

    private GameContext Ctx => GameLoop.Instance != null ? GameLoop.Instance.Ctx : null;
    private bool registered;

    /// <summary>生成后立即初始化掉落物数据。</summary>
    public void Setup(int itemId, int count, int rotation, float interactRange = 2f)
    {
        this.itemId = itemId;
        this.count = Mathf.Max(1, count);
        this.rotation = rotation;
        this.interactRange = interactRange;

        var ctx = Ctx;
        if (ctx != null && ctx.TryGet<BagSystem>(out var bag))
        {
            var cfg = bag.GetItem(itemId);
            if (cfg != null && !string.IsNullOrEmpty(cfg.Name)) displayName = cfg.Name;
        }
        TryRegister(); // Setup 常在 Instantiate 后、Start 前调,这里先注册(registered 去重)
    }

    // ChestEntity 同理:Start 时 GameLoop.Awake 已跑、Ctx 就绪。Setup 与 Start 都尝试,registered 防重复。
    private void Start() => TryRegister();

    private void TryRegister()
    {
        if (registered) return;
        var ctx = Ctx;
        if (ctx != null && ctx.TryGet<WorldInteractionSystem>(out var s)) { s.Register(this); registered = true; }
    }

    private void OnDestroy()
    {
        var ctx = Ctx;
        if (registered && ctx != null && ctx.TryGet<WorldInteractionSystem>(out var s)) s.Unregister(this);
    }

    public void Interact()
    {
        var ctx = Ctx;
        if (ctx == null) return;
        if (!ctx.TryGet<BagSystem>(out var bag)) return;

        int leftover = bag.AddItem(itemId, count); // 返回放不下的剩余(0 = 全进了背包)
        if (leftover <= 0) { Destroy(gameObject); return; }

        // 背包满:放不下的留在地上,更新数量(下次靠近提示随之变化)。
        count = leftover;
        Debug.Log($"[DropItem] 背包放不下 {displayName},剩余 x{count} 留在地上");
    }
}
