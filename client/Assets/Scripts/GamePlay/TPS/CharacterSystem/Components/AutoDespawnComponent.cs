/// <summary>
/// 订阅本 Character 的 <see cref="HealthComponent.OnDied"/>，倒计时到点叫 CharacterManager 走 deferred remove。
/// 把"死亡后清理生命周期"从 HealthComponent 拆出来，让 HealthComponent 只做 HP 数学 + 事件广播。
///
/// 想让某 actor 死亡保留尸体不清理 → 不挂这个组件 / 把 Delay 设 &lt;=0。
/// 多个 AutoDespawnComponent 不会冲突（Manager.RemoveCharacter 是 deferred + 去重的）。
/// </summary>
public class AutoDespawnComponent : ICharacterComponent
{
    /// <summary>死亡后多久叫 CharacterManager 清理（秒）。默认 3s = 倒地动画播完 + 留停顿。</summary>
    public float Delay = 3f;

    private HealthComponent health;
    private CharacterManager characterMgr;
    private float timer;
    private bool dying;

    public override void Attach(Character owner)
    {
        Ctx?.TryGet(out characterMgr);
        health = owner.Get<HealthComponent>();
        if (health != null) health.OnDied += OnDied;
    }

    public override void Detach()
    {
        if (health != null) health.OnDied -= OnDied;
        health = null;
        characterMgr = null;
        dying = false;
        timer = 0f;
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
            characterMgr?.RemoveCharacter(Owner);
        }
    }
}
