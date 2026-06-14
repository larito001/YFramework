using UnityEngine;

/// <summary>
/// 世界掉落物 view（由 <see cref="DropItemSystem"/> 在运行时拼好的模型 + 物理 GameObject 上 AddComponent，
/// 经 <see cref="ViewManager.RegisterView"/> 注册）。实现 <see cref="IInteractable"/>：靠近显示提示，按 F 捡回背包。
/// 背包放得下则请求 <see cref="DropItemSystem"/> 移除本掉落物；放不下则把剩余数量留在地上（更新 <see cref="DropItemActor.Count"/>）。
///
/// transform 由 Rigidbody 物理驱动，LateUpdate 把 transform.position 回写到 <see cref="DropItemActor.Position"/>，
/// 让 actor 保持权威坐标（对照 CharacterView 用 CC 跑完回写 Position）。
/// </summary>
public class DropItemView : BaseView, IInteractable
{
    private DropItemActor drop;

    public Vector3 WorldPosition => transform.position;
    public float InteractRange => drop != null ? drop.InteractRange : 2f;
    public string Prompt => drop == null ? string.Empty
        : (drop.Count > 1 ? $"按 F 捡起 {drop.DisplayName} x{drop.Count}" : $"按 F 捡起 {drop.DisplayName}");
    public bool CanInteract => drop != null;

    private GameContext Ctx => GameLoop.Instance != null ? GameLoop.Instance.Ctx : null;

    public override void Bind(Actor actor, int id)
    {
        drop = actor as DropItemActor;
        ID = id;
        if (drop != null) drop.Position = transform.position;
        if (Ctx != null && Ctx.TryGet<WorldInteractionSystem>(out var s)) s.Register(this);
    }

    private void OnDestroy()
    {
        if (Ctx != null && Ctx.TryGet<WorldInteractionSystem>(out var s)) s.Unregister(this);
        drop = null;
        ID = -1;
    }

    private void LateUpdate()
    {
        // 物理驱动 transform，回写到 actor（actor 保持权威坐标，供其它系统读）。
        if (drop != null) drop.Position = transform.position;
    }

    public void Interact()
    {
        if (drop == null) return;
        var ctx = Ctx;
        if (ctx == null) return;
        if (!ctx.TryGet<BagSystem>(out var bag)) return;

        int leftover = bag.AddItem(drop.ItemId, drop.Count); // 返回放不下的剩余（0 = 全进背包）
        if (leftover <= 0)
        {
            // 全捡走：请求掉落物系统移除本 actor（统一注销 ActorWorld + 销毁 view）。
            if (ctx.TryGet<DropItemSystem>(out var sys)) sys.RemoveDropItem(drop);
            return;
        }

        // 背包满：放不下的留在地上，更新数量（下次靠近提示随之变化）。
        drop.Count = leftover;
        Debug.Log($"[DropItem] 背包放不下 {drop.DisplayName}，剩余 x{drop.Count} 留在地上");
    }
}
