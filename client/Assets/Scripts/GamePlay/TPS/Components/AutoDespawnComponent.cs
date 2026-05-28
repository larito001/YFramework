using System;

/// <summary>
/// 订阅本 Actor 的 <see cref="HealthComponent.OnDied"/>，倒计时到点触发 <see cref="OnDespawnReady"/> 事件，
/// 由订阅方（一般是装 Actor 的 Manager）决定怎么清理。把"死亡后清理生命周期"从 HealthComponent 拆出来，
/// 让 HealthComponent 只做 HP 数学 + 事件广播。
///
/// **通用组件**：直接继承 IActorComponent，Owner=Actor。组件不知道哪个 Manager 拥有自己——
/// Factory / Manager 在装组件后**订阅 OnDespawnReady** 来接入清理。如：
/// <code>
///   var ad = new AutoDespawnComponent { Delay = 3f };
///   ad.OnDespawnReady += a =&gt; characterMgr.RemoveCharacter(a as Character);
///   character.Add(ad);
/// </code>
///
/// 想让某 actor 死亡保留尸体不清理 → 不挂这个组件 / 把 Delay 设 &lt;=0。
/// </summary>
public class AutoDespawnComponent : IActorComponent
{
    /// <summary>死亡后多久触发 OnDespawnReady（秒）。默认 3s = 倒地动画播完 + 留停顿。&lt;=0 表示不启动。</summary>
    public float Delay = 3f;

    /// <summary>倒计时到点的事件。Factory / Manager 订阅此事件，回调里把 Owner 还给对应 Manager 清理。
    /// Detach 时本事件会被清空（避免 GC 拖延导致旧订阅者被回调）。</summary>
    public event Action<Actor> OnDespawnReady;

    private HealthComponent health;
    private float timer;
    private bool dying;

    public override void Attach(Actor owner)
    {
        base.Attach(owner);
        if (owner == null) return;
        health = owner.Get<HealthComponent>();
        if (health != null) health.OnDied += OnDied;
    }

    public override void Detach()
    {
        if (health != null) health.OnDied -= OnDied;
        health = null;
        dying = false;
        timer = 0f;
        OnDespawnReady = null;  // 清订阅，防 GC 拖延回调
        base.Detach();
    }

    private void OnDied(int attackerId)
    {
        if (Delay <= 0f) return;
        dying = true;
        timer = Delay;
    }

    public override void Tick(float dt)
    {
        if (!dying || Owner == null) return;
        timer -= dt;
        if (timer <= 0f)
        {
            dying = false;
            timer = 0f;
            OnDespawnReady?.Invoke(Owner);
        }
    }
}
