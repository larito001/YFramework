using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Character 集合 + 每帧驱动。所有 Character 的逻辑组件统一在这里 Tick。
/// 只管 Actor 列表 + 生命周期；view 加载/销毁委托给全局 ViewManager service。
/// </summary>
public class CharacterManager : IGameService, ITickable
{
    private GameContext ctx;
    private ViewManager viewMgr;
    private ActorWorld world;
    private CharacterFactory factory;
    private readonly List<Character> characters = new List<Character>();
    // Deferred removal queue：组件 Tick 期间调 RemoveCharacter 不会改正在遍历的 characters 列表，
    // 攒到本帧 Tick 末尾统一清。和 BulletManager.toRemove 一个套路。
    private readonly List<Character> toRemove = new List<Character>();

    // 玩家复活：记录当前玩家 + 出生点；玩家死亡 AutoDespawn 到点时不移除而是重生。
    private Character player;
    private Vector3 playerSpawnPos;
    private bool respawnPlayerPending;

    public void Init(GameContext context)
    {
        ctx = context;
        viewMgr = context.Get<ViewManager>();
        world = context.Get<ActorWorld>();
        factory = new CharacterFactory();
        factory.BindViewManager(viewMgr);
    }

    public void Shutdown()
    {
        for (int i = characters.Count - 1; i >= 0; i--)
        {
            var c = characters[i];
            world.Unregister(c.ID);
            viewMgr.RemoveBaseView(c.ID);
            c.Dispose();
        }
        characters.Clear();
    }

    public void Tick(float dt)
    {
        // 局部时间缩放在 Actor.Tick 内部统一处理（每个 actor 用自己的 TimeScale），这里只传游戏帧 dt。
        // TimeScaleService 在本 Manager 之前 Tick 写好 actor.TimeScale。
        for (int i = 0; i < characters.Count; i++)
            characters[i].Tick(dt);

        if (toRemove.Count > 0)
        {
            for (int i = 0; i < toRemove.Count; i++) RemoveImmediate(toRemove[i]);
            toRemove.Clear();
        }

        // 玩家重生：在移除处理之后做（避开 Tick 遍历期间往 characters 加新元素），出生点建新玩家 + 重新跟随相机。
        if (respawnPlayerPending)
        {
            respawnPlayerPending = false;
            SpawnPlayer(playerSpawnPos);
        }
    }

    public void GenneratePlayer(Vector3 position = default)
    {
        playerSpawnPos = position;
        SpawnPlayer(position); // 设 this.player

        // 测试用：在玩家前方铺 3 个 Dummy 站桩靶，便于验证近战/子弹/导弹/射线四种伤害源都能扣血。
        // 不想要就删这三行。
        SpawnDummy(new Vector3(0f, 0f, 10f));
        SpawnDummy(new Vector3(4f, 0f, 12f));
        SpawnDummy(new Vector3(-4f, 0f, 12f));

        // 测试用：spawn 2 个会随机行动（idle/走/跑/攻击/飞扑）的僵尸。
        // 需先用菜单 Tools/TPS/Build Skill & Anim Assets 生成 Resources 下的 ZombieAnimSet + 技能资产。
        SpawnZombie(new Vector3(4f, 0f, 8f));
        SpawnZombie(new Vector3(-4f, 0f, 8f));

        // 测试用：玩家侧后方 spawn 一座塔（TeamId=1 玩家军 + 玩家 ID 作为放置者），验证锁敌 + telegraph + 开火链路
        if (player != null && ctx != null && ctx.TryGet<TowerManager>(out var towerMgr))
        {
            // towerMgr.SpawnTower(new Vector3(-3f, 0f, -2f), teamId: 1, ownerActorId: player.ID);
        }
    }

    /// <summary>spawn（或复活）玩家：建角色 + 挂生命周期钩子 + 注册 ActorWorld + 相机跟随 + 记录为当前玩家。
    /// 复活走"重生"路线（删旧 + 建新）而非原地改死值——复用整条 spawn 链，省去解死亡溶解 / CC disable / 死亡动画的麻烦。</summary>
    private void SpawnPlayer(Vector3 position)
    {
        var c = factory.CreateCharacter(position);
        AttachLifecycleHooks(c);
        characters.Add(c);
        world.Register(c);
        player = c;

        // 让相机跟随玩家。view 由 LoadBaseView 在 factory 内同步创建，这里能拿到。
        if (viewMgr.TryGetView(c.ID, out var view) && view != null
            && ctx != null && ctx.TryGet<CameraManager>(out var cam))
            cam.SetFollow(view.transform);
    }

    /// <summary>生成一个站桩 Dummy 敌人在指定位置。返回 Character 实例供外部进一步配置（订阅 OnDied 等）。</summary>
    public Character SpawnDummy(Vector3 position, float maxHealth = 1000f)
    {
        var c = factory.CreateDummy(position, maxHealth);
        AttachLifecycleHooks(c);
        characters.Add(c);
        world.Register(c);
        return c;
    }

    /// <summary>生成一个会随机行动的僵尸（locomotion + 技能 + 简单 AI）在指定位置。</summary>
    public Character SpawnZombie(Vector3 position, float maxHealth = 200f)
    {
        var c = factory.CreateZombie(position, maxHealth);
        AttachLifecycleHooks(c);
        characters.Add(c);
        world.Register(c);
        return c;
    }

    /// <summary>给新 spawn 的 Character 挂上 Manager 侧的事件订阅：
    /// 当前是 AutoDespawnComponent.OnDespawnReady → 倒计时到点 → RemoveCharacter（deferred）。
    /// 通用组件不持 Manager 引用、走事件订阅是 ARCHITECTURE 约定（详见"跨对象通信"小节）。
    /// 配对的 <see cref="DetachLifecycleHooks"/> 在 RemoveImmediate 里调用。</summary>
    private void AttachLifecycleHooks(Character c)
    {
        var ad = c.Get<AutoDespawnComponent>();
        if (ad != null) ad.OnDespawnReady += OnAutoDespawnReady;
    }

    /// <summary>显式退订对应 AttachLifecycleHooks 的事件。即便 AutoDespawnComponent.Detach
    /// 会清 event = null 兜底，也走这一步——避免依赖发布方的清理协议（中途 Remove 重 Add 时会静默失订阅）。</summary>
    private void DetachLifecycleHooks(Character c)
    {
        var ad = c.Get<AutoDespawnComponent>();
        if (ad != null) ad.OnDespawnReady -= OnAutoDespawnReady;
    }

    private void OnAutoDespawnReady(Actor a)
    {
        if (!(a is Character c)) return;
        RemoveCharacter(c);
        // 玩家：死亡 3s（AutoDespawn.Delay）后不是消失，而是在出生点重生。其余敌人正常移除。
        if (c == player) respawnPlayerPending = true;
    }

    /// <summary>请求移除 Character（deferred）。组件 Tick 中调用安全；实际清理发生在本帧 Tick 末尾。
    /// 已在队列里的请求会被去重，重复调用无副作用。</summary>
    public void RemoveCharacter(Character character)
    {
        if (character == null) return;
        if (toRemove.Contains(character)) return;
        toRemove.Add(character);
    }

    private void RemoveImmediate(Character character)
    {
        if (character == null) return;
        DetachLifecycleHooks(character);
        characters.Remove(character);
        world.Unregister(character.ID);
        viewMgr.RemoveBaseView(character.ID);
        character.Dispose();
    }
}
