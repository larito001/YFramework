using UnityEngine;

/// <summary>
/// 换弹组件：装在 Weapon 上。WeaponComponent 收 R 键事件后写 Owner.ReloadRequest = true，
/// 本组件 Tick 消费 trigger → 启动定时器 → 到时间把 CurrentAmmo 填回 MagCapacity 并清 IsReloading。
///
/// 字段写入（Owner=Weapon 上）：
///   - 本组件是 <c>Owner.IsReloading</c> 和 <c>Owner.CurrentAmmo</c> 的唯一 writer
///   - 消费并清 <c>Owner.ReloadRequest</c>
///
/// 不依赖装备态：未装备的武器也能在后台跑完 reload（玩家"预换弹再切回来"是常见操作）。
/// 切枪打断：要主动打断，从外部把 Owner.IsReloading=false 写回，本组件下一 Tick 看到就跳过 timer。
/// </summary>
public class ReloadComponent : IWeaponComponent
{
    /// <summary>换弹总时长（秒）。应匹配 Animator 里 Reload 动画长度。</summary>
    public float ReloadDuration = 1.5f;

    private float timer;

    public override void Detach()
    {
        if (Owner != null)
        {
            Owner.IsReloading = false;
            // 清 ReloadRequest trigger：Detach 时若为 true（玩家刚按 R 还没轮到本组件 Tick 消费），
            // 下次 attach 会立即消费 stale 请求自动开始换弹
            Owner.ReloadRequest = false;
        }
        timer = 0f;
        base.Detach();
    }

    public override void Tick(float dt)
    {
        if (Owner == null) return;

        // 1. 接收新的换弹请求：弹匣不满 + 未在换弹中
        if (Owner.ReloadRequest)
        {
            Owner.ReloadRequest = false;  // 消费 trigger
            if (Owner.MagCapacity > 0
                && Owner.CurrentAmmo < Owner.MagCapacity
                && !Owner.IsReloading)
            {
                Owner.IsReloading = true;
                timer = ReloadDuration;
            }
        }

        // 2. 换弹中：倒计时（IsReloading 被外部清零 → 跳过 timer，作为打断信号）
        if (Owner.IsReloading)
        {
            timer -= dt;
            if (timer <= 0f)
            {
                Owner.CurrentAmmo = Owner.MagCapacity;
                Owner.IsReloading = false;
                timer = 0f;
#if UNITY_EDITOR
                Debug.Log($"[Reload] {Owner.Name} reloaded: {Owner.CurrentAmmo}/{Owner.MagCapacity}");
#endif
            }
        }
    }
}
