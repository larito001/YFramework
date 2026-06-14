/// <summary>
/// 世界掉落物 Actor。纯数据 + 组件容器，按 Actor 约定接入。通用字段（Position）在 <see cref="Actor"/> 基类，
/// 本类只持掉落物专属数据；行为放在 <see cref="DropItemView"/>（IInteractable：靠近 + F 捡回背包）。
///
/// 与宝箱/塔不同，掉落物的模型带 Rigidbody 物理（会落地、可被推），transform 由物理驱动，
/// 故 <see cref="DropItemView"/> 在 LateUpdate **回写** transform.position → <see cref="Actor.Position"/>
/// （类似 CharacterView 用 CC 跑完再回写 Position），让 actor 保持权威坐标。
/// </summary>
public class DropItemActor : Actor
{
    /// <summary>掉落的物品 id（对应 item 配表）。</summary>
    public int ItemId;
    /// <summary>堆叠数量。捡起时背包放不下的剩余会写回这里（留在地上）。</summary>
    public int Count;
    /// <summary>交互范围（米）。</summary>
    public float InteractRange = 2f;
    /// <summary>提示用显示名（取自 item 配表，缺省 "物品"）。</summary>
    public string DisplayName = "物品";
}
