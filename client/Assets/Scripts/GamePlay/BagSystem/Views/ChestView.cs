using UnityEngine;

/// <summary>
/// 世界宝箱 view（挂在宝箱模型预制体上，如 ammo_box；prefab 没挂时由 <see cref="ViewManager.LoadBaseView"/>
/// 的 addIfMissing 运行时 AddComponent）。对照 <see cref="TowerView"/>：被动同步 <see cref="ChestActor"/>
/// 的 Position / Rotation 到 transform（宝箱基本不动，等同摆位）。
///
/// 同时实现 <see cref="IInteractable"/>，复用 <see cref="WorldInteractionSystem"/> 的「靠近检测 + F 触发」框架：
/// 玩家靠近显示提示，按 F 打开 <see cref="ChestPanel"/>。注册/注销时机与原 ChestEntity 一致
/// （Bind 时注册，OnDestroy 时注销；此刻 GameLoop.Awake 已跑、Ctx 就绪）。
/// </summary>
public class ChestView : BaseView, IInteractable
{
    private ChestActor chest;

    public Vector3 WorldPosition => transform.position;
    public float InteractRange => chest != null ? chest.InteractRange : 3.5f;
    public string Prompt => "按 F 打开宝箱";
    public bool CanInteract => chest != null;

    private GameContext Ctx => GameLoop.Instance != null ? GameLoop.Instance.Ctx : null;

    public override void Bind(Actor actor, int id)
    {
        chest = actor as ChestActor;
        ID = id;
        if (chest != null)
        {
            chest.Position = transform.position;
            chest.Rotation = transform.rotation;
        }
        // 注册到世界交互系统（靠近 + F）。LoadBaseView 在游戏运行中调用，此时 Ctx 已就绪。
        if (Ctx != null && Ctx.TryGet<WorldInteractionSystem>(out var s)) s.Register(this);
    }

    private void OnDestroy()
    {
        if (Ctx != null && Ctx.TryGet<WorldInteractionSystem>(out var s)) s.Unregister(this);
        chest = null;
        ID = -1;
    }

    private void LateUpdate()
    {
        if (chest == null) return;
        // 被动同步：actor 是权威，view 跟随（宝箱不动，等同保持摆位）。
        transform.position = chest.Position;
        transform.rotation = chest.Rotation;
    }

    public void Interact()
    {
        if (chest == null) return;
        var ctx = Ctx;
        if (ctx == null) return;
        if (!ctx.TryGet<ChestSystem>(out var chestSys)) return;
        if (!ctx.TryGet<UIMgr>(out var ui)) return;

        // 首次打开 roll 一次，存到 actor 上；之后复用同一网格（整局保持）。
        if (chest.RolledGrid == null)
        {
            chest.RolledGrid = chestSys.Roll(chest.ChestId, 0);
            if (chest.RolledGrid == null) return;
        }

        chestSys.SetCurrent(chest.RolledGrid, chest.ChestId);
        ui.Show<ChestPanel>();
    }
}
