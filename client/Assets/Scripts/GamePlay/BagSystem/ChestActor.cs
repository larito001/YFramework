/// <summary>
/// 世界宝箱 Actor。纯数据 + 组件容器，按 Actor 约定接入：和 <see cref="Tower"/> 一样是「基本不动的可交互物」，
/// 通用字段（Position / Rotation）在 <see cref="Actor"/> 基类，本类只持宝箱专属数据。
///
/// 行为放在 <see cref="ChestView"/>（IInteractable：靠近显示提示、按 F 打开 <see cref="ChestPanel"/>）。
///
/// **刷新策略**：本宝箱**首次**打开时 <see cref="ChestSystem.Roll"/> 一次，生成的网格存 <see cref="RolledGrid"/>，
/// 整局保持（玩家拿走/放入都留在它上面）；游戏重开 → actor 重建 → RolledGrid 为空 → 重新 roll。不写盘。
/// （原 ChestEntity.rolledGrid 的语义平移到这里，由 actor 持有而非 MonoBehaviour。）
/// </summary>
public class ChestActor : Actor
{
    /// <summary>chest 配表里的宝箱 id。</summary>
    public int ChestId = 1;

    /// <summary>交互范围（米）。<see cref="ChestView"/> 暴露给 <see cref="WorldInteractionSystem"/> 做靠近检测。</summary>
    public float InteractRange = 3.5f;

    /// <summary>首次打开生成的宝箱网格，整局保持（null=尚未打开过）。<see cref="ChestView.Interact"/> 写/读。</summary>
    public GridBag RolledGrid;
}
