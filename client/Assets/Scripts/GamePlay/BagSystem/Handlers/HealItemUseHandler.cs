using UnityEngine;

/// <summary>
/// 治疗类消耗品的使用逻辑示例(<see cref="IItemUseHandler"/>):使用时给当前玩家回血。
/// 演示「效果与配表解耦、按物品 id 注册」的用法。
///
/// 玩家在 handler 内**延迟查找**(每次使用时通过 CharacterManager.Player 取),
/// 注册时机不要求玩家已 spawn;玩家死亡/重生切换实例也能命中最新玩家。
///
/// 回血量当前由构造参数传入(不同药水注册不同数值)。若想完全数据驱动,
/// 在 item.xlsx 加一列(如 useParam:int)重跑打表工具后改读 <c>context.Item</c> 上对应字段即可。
/// </summary>
public class HealItemUseHandler : IItemUseHandler
{
    private readonly GameContext ctx;
    private readonly float healAmount;

    public HealItemUseHandler(GameContext ctx, float healAmount)
    {
        this.ctx = ctx;
        this.healAmount = healAmount;
    }

    public bool OnUse(in ItemUseContext context)
    {
        if (ctx == null || !ctx.TryGet<CharacterManager>(out var charMgr)) return false;

        var player = charMgr.Player;
        if (player == null || player.IsDead) return false;

        var health = player.Get<HealthComponent>();
        if (health == null) return false;

        // 已满血则视为使用失败,不消耗物品(成熟背包的常见手感)
        if (player.CurHealth >= player.MaxHealth) return false;

        health.Heal(healAmount);
        Debug.Log($"[HealItemUseHandler] 使用 {context.Item.Name} 回血 {healAmount}，当前 HP {player.CurHealth:F0}/{player.MaxHealth:F0}");
        return true;
    }
}
