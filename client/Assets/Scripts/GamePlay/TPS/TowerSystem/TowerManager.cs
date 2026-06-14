using UnityEngine;

/// <summary>
/// Tower 集合 + 每帧驱动。复用 <see cref="ActorManager{T}"/> 的 List + Tick + ActorWorld 注册 + 延迟移除骨架，
/// 本类只管 spawn 配方 + AutoDespawn 生命周期钩子。
///
/// **Tick 顺序**：必须在 WeaponManager 之前——TowerWeaponComponent.Tick 写 currentWeapon.FireIntent 等，
/// FireComponent 在 WeaponManager.Tick 里消费。注册顺序：
///   TimeScaleService → CharacterManager → TowerManager → WeaponManager → BulletManager。
/// </summary>
public class TowerManager : ActorManager<Tower>, ITickable
{
    private TowerFactory factory;

    protected override void OnInit()
    {
        factory = new TowerFactory();
        factory.BindViewManager(ViewMgr);
    }

    /// <summary>塔有 Targeting / Weapon 等组件需每帧驱动 → 实现 ITickable，转调基类 TickActors。</summary>
    public void Tick(float dt) => TickActors(dt);

    /// <summary>外部 API：在指定位置 spawn 一座塔。
    /// teamId：默认 1=玩家军；ownerActorId：放置者 Actor.ID（玩家放置传玩家 ID，关卡预设传 -1=无主），用于击杀归属 / 摧毁通知。</summary>
    public Tower SpawnTower(Vector3 position, int teamId = 1, int ownerActorId = -1, float maxHealth = 500f)
    {
        var t = factory.CreateTower(position, teamId, ownerActorId, maxHealth);
        AttachLifecycleHooks(t);
        Track(t);
        return t;
    }

    /// <summary>给新 spawn 的 Tower 挂 AutoDespawnComponent.OnDespawnReady → 倒计时到点 → Remove（deferred）。</summary>
    private void AttachLifecycleHooks(Tower t)
    {
        var ad = t.Get<AutoDespawnComponent>();
        if (ad != null) ad.OnDespawnReady += OnAutoDespawnReady;
    }

    /// <summary>移除前显式退订（即便 AutoDespawnComponent.Detach 会兜底，也走这步，避免依赖发布方清理协议）。</summary>
    protected override void OnRemoving(Tower t)
    {
        var ad = t.Get<AutoDespawnComponent>();
        if (ad != null) ad.OnDespawnReady -= OnAutoDespawnReady;
    }

    private void OnAutoDespawnReady(Actor a)
    {
        if (a is Tower t) Remove(t);
    }
}
