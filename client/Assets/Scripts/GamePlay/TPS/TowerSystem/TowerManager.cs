using System.Collections.Generic;

/// <summary>
/// Tower 集合 + 每帧驱动。结构对照 CharacterManager：维护 List + Tick + 延迟 Remove + AutoDespawn 事件订阅。
///
/// **Tick 顺序**：必须在 WeaponManager 之前——TowerWeaponComponent.Tick 写 currentWeapon.FireIntent / FireOrigin /
/// FireDirection / FireTarget，FireComponent 在 WeaponManager.Tick 里消费。GameLoop / GameBootstrapper 注册顺序：
///   TimeScaleService → CharacterManager → TowerManager → WeaponManager → BulletManager。
/// </summary>
public class TowerManager : IGameService, ITickable
{
    private GameContext ctx;
    private ViewManager viewMgr;
    private ActorWorld world;
    private TowerFactory factory;
    private readonly List<Tower> towers = new List<Tower>();
    // Deferred removal：组件 Tick 期间调 RemoveTower 不会改正在遍历的 towers 列表，攒到本帧 Tick 末统一清。
    private readonly List<Tower> toRemove = new List<Tower>();

    public void Init(GameContext context)
    {
        ctx = context;
        viewMgr = context.Get<ViewManager>();
        world = context.Get<ActorWorld>();
        factory = new TowerFactory();
        factory.BindViewManager(viewMgr);
    }

    public void Shutdown()
    {
        for (int i = towers.Count - 1; i >= 0; i--)
        {
            var t = towers[i];
            world.Unregister(t.ID);
            viewMgr.RemoveBaseView(t.ID);
            t.Dispose();
        }
        towers.Clear();
    }

    public void Tick(float dt)
    {
        for (int i = 0; i < towers.Count; i++)
            towers[i].Tick(dt);

        if (toRemove.Count > 0)
        {
            for (int i = 0; i < toRemove.Count; i++) RemoveImmediate(toRemove[i]);
            toRemove.Clear();
        }
    }

    /// <summary>外部 API：在指定位置 spawn 一座塔（teamId 默认 1=玩家军）。返回 Tower 实例供外部进一步配置。</summary>
    public Tower SpawnTower(UnityEngine.Vector3 position, int teamId = 1, float maxHealth = 500f)
    {
        var t = factory.CreateTower(position, teamId, maxHealth);
        AttachLifecycleHooks(t);
        towers.Add(t);
        world.Register(t);
        return t;
    }

    /// <summary>请求移除 Tower（deferred）。组件 Tick 中调用安全；实际清理发生在本帧 Tick 末尾。
    /// 已在队列里的请求会被去重，重复调用无副作用。</summary>
    public void RemoveTower(Tower tower)
    {
        if (tower == null) return;
        if (toRemove.Contains(tower)) return;
        toRemove.Add(tower);
    }

    /// <summary>给新 spawn 的 Tower 挂上 Manager 侧的事件订阅（同 CharacterManager 套路）：
    /// AutoDespawnComponent.OnDespawnReady → 倒计时到点 → RemoveTower（deferred）。</summary>
    private void AttachLifecycleHooks(Tower t)
    {
        var ad = t.Get<AutoDespawnComponent>();
        if (ad != null) ad.OnDespawnReady += OnAutoDespawnReady;
    }

    private void DetachLifecycleHooks(Tower t)
    {
        var ad = t.Get<AutoDespawnComponent>();
        if (ad != null) ad.OnDespawnReady -= OnAutoDespawnReady;
    }

    private void OnAutoDespawnReady(Actor a)
    {
        if (a is Tower t) RemoveTower(t);
    }

    private void RemoveImmediate(Tower tower)
    {
        if (tower == null) return;
        DetachLifecycleHooks(tower);
        towers.Remove(tower);
        world.Unregister(tower.ID);
        viewMgr.RemoveBaseView(tower.ID);
        tower.Dispose();
    }
}
