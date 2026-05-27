using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 子弹集合 + 每帧 Tick + Spawn/Despawn。
/// view 走 PrefabPool<BulletView> 池化（避免 600 RPM 下 GC 抖动）。
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
        prefabPrototypeGo.name = "[BulletPrefabPrototype]";
        prefabPrototypeGo.transform.localScale = Vector3.one * 0.1f;
        var col = prefabPrototypeGo.GetComponent<Collider>();
        if (col != null) Object.Destroy(col); // 子弹自己不参与物理碰撞
        prefabPrototypeGo.SetActive(false);
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

    public Bullet Spawn(Vector3 position, Vector3 velocity, float lifetime, float damage, int ownerCharacterId)
    {
        var b = new Bullet
        {
            Position = position,
            Velocity = velocity,
            LifetimeRemaining = lifetime,
            Damage = damage,
            OwnerCharacterId = ownerCharacterId,
        };
        b.Add(new BulletMoveComponent());

        bullets.Add(b);
        world.Register(b);

        var rot = velocity.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(velocity) : Quaternion.identity;
        var view = viewPool.Get(position, rot);
        view.Bind(b, b.ID);
        activeViews[b.ID] = view;

        return b;
    }

    public void Despawn(Bullet bullet)
    {
        if (bullet == null) return;
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
