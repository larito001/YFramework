using System;
using System.Collections.Generic;

/// <summary>
/// 按 UI 状态闸门战斗输入：只要除主界面(<see cref="UIEnum.GameMainPanel"/>)以外的任意 UI 面板
/// (背包 / 宝箱 / 设置 / 结算 / 开始 / 存档槽 等)处于显示状态，就把
/// <see cref="InputService.CombatEnabled"/> 置为 false，屏蔽移动 / 开火 / 瞄准 / 技能 / 选武器等
/// 战斗按键；所有此类面板关闭后自动恢复。
///
/// 不受影响的键：B(开关背包) / E(交互) / F(世界交互)——它们走 <see cref="InputService"/> 里
/// CombatEnabled 之外的分支，所以背包等面板打开时玩家仍能用 B 把它关掉。
///
/// 实现为每帧轮询：<see cref="UIMgr"/> 目前没有面板开/关事件，而 UI 数量极少
/// (一次 Tick 几次字典查询)，开销可忽略，换来对“所有非主界面 UI”天然生效、新增面板零改动。
/// </summary>
public class CombatInputGate : IGameService, ITickable
{
    private InputService input;
    private UIMgr uiMgr;

    // 预算出“会屏蔽战斗输入”的面板集合：全部 UIEnum 去掉占位 None 和常驻 HUD 主界面。
    // 静态缓存，避免每帧 Enum.GetValues 装箱分配。
    private static readonly UIEnum[] BlockingPanels = BuildBlockingPanels();

    public void Init(GameContext ctx)
    {
        input = ctx.Get<InputService>();
        uiMgr = ctx.Get<UIMgr>();
    }

    public void Shutdown()
    {
        // 还原闸门，避免下次进入游戏残留 disabled 状态。
        if (input != null) input.CombatEnabled = true;
        input = null;
        uiMgr = null;
    }

    public void Tick(float dt)
    {
        if (input == null || uiMgr == null) return;
        input.CombatEnabled = !IsAnyBlockingUIShown();
    }

    private bool IsAnyBlockingUIShown()
    {
        for (int i = 0; i < BlockingPanels.Length; i++)
        {
            if (uiMgr.IsShown(BlockingPanels[i])) return true;
        }
        return false;
    }

    private static UIEnum[] BuildBlockingPanels()
    {
        var values = (UIEnum[])Enum.GetValues(typeof(UIEnum));
        var list = new List<UIEnum>(values.Length);
        foreach (var v in values)
        {
            // None 是占位；GameMainPanel 是常驻 HUD，不算“打开了 UI”。
            if (v == UIEnum.None || v == UIEnum.GameMainPanel) continue;
            list.Add(v);
        }
        return list.ToArray();
    }
}
