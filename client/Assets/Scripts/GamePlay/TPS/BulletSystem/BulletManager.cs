using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 子弹集合 + 每帧 Tick + Spawn/Despawn。
/// view 走 PrefabPool&lt;BulletView&gt; 池化（避免 600 RPM 下 GC 抖动）。
/// 池的 prefab 原型 Init 时程序化建一个 inactive sphere primitive，不依赖外部 Resources prefab，
/// 立刻可用；后续要换真子弹模型，把构造 prototype 那段换成 ResMgr.Load + Instantiate 即可。
///
/// view 不进 ViewManager 字典 —— Bullet 是高频短命且无人反查，pool 自管生命周期。
/// Despawn 在 Tick 期间被调用时不立即移除 —— 缓冲到 Tick 末尾 RemoveImmediate，避免遍历中改 list。
/// </summary>
public class BulletManager : IGameService, ITickable
{
    /// <summary>池预热数量（构造时预 Instantiate）。</summary>
    public int PoolPrewarm = 16;
    /// <summary>池上限。超过后 Release 直接 Destroy。</summary>
    public int PoolMaxSize = 256;

    private ActorWorld world;
    private readonly List<Bullet> bullets = new List<Bullet>();
    private readonly List<Bullet> toRemove = new List<Bullet>();
    private readonly Dictionary<int, BulletView> activeViews = new Dictionary<int, BulletView>();

    private PrefabPool<BulletView> viewPool;
    private GameObject prefabPrototypeGo;

    public void Init(GameContext context)
    {
        world = context.Get<ActorWorld>();

        // 程序化建一个 inactive sphere 当池的 prefab 原型。PrefabPool 用它做 Instantiate 克隆源。
        prefabPrototypeGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        prefabPrototypeGo.SetActive(false);
        prefabPrototypeGo.name = "[BulletPrefabPrototype]";
        prefabPrototypeGo.transform.localScale = Vector3.one * 0.1f;
        var col = prefabPrototypeGo.GetComponent<Collider>();
        // 必须 DestroyImmediate：Object.Destroy 排队到帧末才生效，下面 PrefabPool 构造里立刻
        // Instantiate prewarm 份克隆会全部继承 Collider，导致子弹互射命中、角色蹭到子弹等怪事
        if (col != null) Object.DestroyImmediate(col);
        var prototype = prefabPrototypeGo.AddComponent<BulletView>();

        viewPool = new PrefabPool<BulletView>(prototype, prewarm: PoolPrewarm, maxSize: PoolMaxSize);
    }

    public void Shutdown()
    {
        for (int i = bullets.Count - 1; i >= 0; i--)
            RemoveImmediate(bullets[i]);
        bullets.Clear();
        toRemove.Clear();
        activeViews.Clear();

        viewPool?.Dispose();
        viewPool = null;
        if (prefabPrototypeGo != null)
        {
            Object.Destroy(prefabPrototypeGo);
            prefabPrototypeGo = null;
        }
    }

    public void Tick(float dt)
    {
        for (int i = 0; i < bullets.Count; i++)
            bullets[i].Tick(dt);

        if (toRemove.Count > 0)
        {
            for (int i = 0; i < toRemove.Count; i++)
                RemoveImmediate(toRemove[i]);
            toRemove.Clear();
        }
    }

    /// <summary>直线弹道便捷 API：创建 Bullet + 自动 Add <see cref="BulletMoveComponent"/>。
    /// damage 是攻击者侧配置的伤害"配方"（含基础 / 卡肉 / 暴击 / 元素 / buff），命中时由 SegmentRaycastMoveBase 调 DamageInfo.Build 组装。</summary>
    public Bullet Spawn(Vector3 position, Vector3 velocity, float lifetime, in DamageSpec damage, int ownerActorId, int teamId)
    {
        var b = SpawnBullet(position, lifetime, in damage, ownerActorId, teamId, velocity);
        b.Add(new BulletMoveComponent());
        return b;
    }

    /// <summary>裸 Spawn：注册 Bullet + 创建 view，但**不挂任何移动组件**。
    /// 调用方自己 Add 想要的 IBulletComponent（例如 <see cref="BulletMoveComponent"/> 直线、
    /// 或贝塞尔曲线/制导/抛物线等自定义实现）。
    /// teamId 是发射者阵营，子弹继承用于友军过滤（命中目标时打进 DamageInfo.AttackerTeamId）。
    /// initialVelocity 仅用于 view 初始朝向；后续每帧由移动组件写 Owner.Velocity 决定显示朝向。</summary>
    public Bullet SpawnBullet(Vector3 position, float lifetime, in DamageSpec damage, int ownerActorId, int teamId, Vector3 initialVelocity = default)
    {
        var b = new Bullet
        {
            Position = position,
            Velocity = initialVelocity,
            LifetimeRemaining = lifetime,
            Damage = damage,
            OwnerActorId = ownerActorId,
            TeamId = teamId,
        };

        bullets.Add(b);
        world.Register(b);

        var rot = initialVelocity.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(initialVelocity) : Quaternion.identity;
        var view = viewPool.Get(position, rot);
        view.Bind(b, b.ID);
        activeViews[b.ID] = view;

        return b;
    }

    /// <summary>请求 Despawn（deferred）。Tick 末批量清。同一颗子弹一帧内多次调用会去重，
    /// 避免 RemoveImmediate 重复释放 view / 重复 Dispose。和 CharacterManager.RemoveCharacter 对齐。</summary>
    public void Despawn(Bullet bullet)
    {
        if (bullet == null) return;
        if (toRemove.Contains(bullet)) return;
        toRemove.Add(bullet);
    }

    private void RemoveImmediate(Bullet bullet)
    {
        if (bullet == null) return;
        bullets.Remove(bullet);
        world.Unregister(bullet.ID);
        if (activeViews.TryGetValue(bullet.ID, out var view))
        {
            viewPool.Release(view); // SetActive(false) + OnDespawn 清字段 + 还回 stack
            activeViews.Remove(bullet.ID);
        }
        bullet.Dispose();
    }
}
