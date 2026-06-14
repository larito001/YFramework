using System.Collections.Generic;
using UnityEngine;
using YOTO;

/// <summary>世界可交互物体(宝箱、拾取物等)。由 <see cref="WorldInteractionSystem"/> 统一做靠近检测 + F 触发。</summary>
public interface IInteractable
{
    Vector3 WorldPosition { get; }
    float InteractRange { get; }
    string Prompt { get; }   // 提示文字,如 "按 F 打开"
    bool CanInteract { get; }
    void Interact();
}

/// <summary>
/// 世界交互系统(<see cref="IGameService"/> + <see cref="ITickable"/>)。
/// 每帧找出离玩家最近、且在自身交互范围内的 <see cref="IInteractable"/>,显示提示;
/// 按下 F(<see cref="InputService.OnInteractWorldDown"/>)时触发它。
/// 提示 UI 用 <c>UI/Bag/InteractPrompt</c> 预制体(SIMHEI 字体,BagPrefabBuilder 生成),挂在 Top 层。
/// </summary>
public class WorldInteractionSystem : IGameService, ITickable
{
    private const string PromptPrefab = "UI/Bag/InteractPrompt";

    private GameContext ctx;
    private InputService input;
    private CharacterManager characterMgr;
    private ViewManager viewMgr;
    private ResMgr resMgr;
    private UIMgr uiMgr;

    private readonly List<IInteractable> interactables = new List<IInteractable>();
    private IInteractable current;

    private GameObject promptGo;
    private TMPro.TextMeshProUGUI promptText;
    private bool promptLoading;

    public void Init(GameContext context)
    {
        ctx = context;
        // **先订阅 F 键**,再取其它服务:即便后续 Get 因注册顺序抛异常,F 键订阅也已生效。
        // CharacterManager / ViewManager 在本系统之后注册,一律 Tick 里懒取,Init 不硬取它们。
        input = ctx.Get<InputService>();
        input.OnInteractWorldDown += OnInteractKey;
        Debug.Log("[WorldInteraction] Init: 已订阅 F 键(OnInteractWorldDown)");

        ctx.TryGet(out resMgr);
        ctx.TryGet(out uiMgr);
    }

    public void Shutdown()
    {
        if (input != null) input.OnInteractWorldDown -= OnInteractKey;
        interactables.Clear();
        current = null;
        if (promptGo != null) Object.Destroy(promptGo);
    }

    /// <summary>世界物体进场时注册(ChestView / DropItemView 在 Bind 时调)。</summary>
    public void Register(IInteractable it)
    {
        if (it != null && !interactables.Contains(it)) interactables.Add(it);
    }

    public void Unregister(IInteractable it)
    {
        interactables.Remove(it);
        if (current == it) { current = null; UpdatePrompt(); }
    }

    public void Tick(float dt)
    {
        if (characterMgr == null) ctx.TryGet(out characterMgr); // 懒取(注册顺序晚于本系统)
        var player = characterMgr != null ? characterMgr.Player : null;
        if (player == null) { SetCurrent(null); return; }

        Vector3 p = GetPlayerWorldPos(player);
        IInteractable best = null;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < interactables.Count; i++)
        {
            var it = interactables[i];
            if (it == null || !it.CanInteract) continue;
            float sqr = (it.WorldPosition - p).sqrMagnitude;
            if (sqr <= it.InteractRange * it.InteractRange && sqr < bestSqr)
            {
                bestSqr = sqr;
                best = it;
            }
        }
        SetCurrent(best);
    }

    /// <summary>玩家世界坐标:优先用 view 的 transform(实时,跟随 CC 移动);取不到再退回逻辑 Position。</summary>
    private Vector3 GetPlayerWorldPos(Character player)
    {
        if (viewMgr == null) ctx.TryGet(out viewMgr);
        if (viewMgr != null && viewMgr.TryGetView(player.ID, out var view) && view != null)
            return view.transform.position;
        return player.Position;
    }

    private void SetCurrent(IInteractable it)
    {
        if (current == it) return;
        current = it;
        UpdatePrompt();
    }

    private void OnInteractKey()
    {
        Debug.Log($"[WorldInteraction] F 按下,current={(current == null ? "null" : current.GetType().Name)}");
        if (current != null && current.CanInteract) current.Interact();
    }

    // ---------------- 提示 UI ----------------

    private void UpdatePrompt()
    {
        if (current == null)
        {
            if (promptGo != null) promptGo.SetActive(false);
            return;
        }
        EnsurePrompt();
        if (promptGo == null) return; // 还在异步加载
        promptGo.SetActive(true);
        if (promptText != null) promptText.text = current.Prompt;
    }

    private void EnsurePrompt()
    {
        if (promptGo != null || promptLoading) return;
        var prefab = resMgr.Load<GameObject>(PromptPrefab);
        if (prefab == null) return; // 预制体未生成(跑 Tools/Bag/Build Bag UI Prefabs)
        var parent = uiMgr.GetLayerRoot(UILayerEnum.Top);
        promptGo = Object.Instantiate(prefab, parent);
        promptText = promptGo.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        promptGo.SetActive(false);
    }
}
