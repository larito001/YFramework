using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 子弹集合 + 每帧 Tick + Spawn/Despawn。
/// View 优先走 ViewManager 加载 BulletPrefabPath；失败时 fallback 程序化建 Sphere primitive
/// （方便项目里还没准备子弹 prefab 时立刻可测）。
///
/// Despawn 在 Tick 期间被调用时不立即移除 —— 缓冲到 Tick 末尾 RemoveImmediate，避免遍历中改 list。
/// </summary>
public class BulletManager : IGameService, ITickable
{
    /// <summary>子弹模型 prefab 路径（Resources 相对）。不存在时走 fallback primitive。</summary>
    public string BulletPrefabPath = "Bullet/Sphere";

    private ViewManager viewMgr;
    private ActorWorld world;
    private readonly List<Bullet> bullets = new List<Bullet>();
    private readonly List<Bullet> toRemove = new List<Bullet>();
    /// fallback 创建的 GameObject 自己管销毁（没走 ViewManager 注册）。
    private readonly Dictionary<int, GameObject> fallbackViews = new Dictionary<int, GameObject>();

    public void Init(GameContext context)
    {
        viewMgr = context.Get<ViewManager>();
        world = context.Get<ActorWorld>();
    }

    public void Shutdown()
    {
        for (int i = bullets.Count - 1; i >= 0; i--)
            RemoveImmediate(bullets[i]);
        bullets.Clear();
        toRemove.Clear();
        fallbackViews.Clear();
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

        var view = viewMgr.LoadBaseView<BulletView>(BulletPrefabPath, b, addIfMissing: true);
        if (view == null)
        {
            // fallback：程序化建 sphere，方便没准备 prefab 时也能立刻看到子弹
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"Bullet_{b.ID}";
            go.transform.localScale = Vector3.one * 0.1f;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col); // 子弹自己不参与物理碰撞
            var v = go.AddComponent<BulletView>();
            v.Bind(b, b.ID);
            fallbackViews[b.ID] = go;
        }

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
        viewMgr.RemoveBaseView(bullet.ID);
        if (fallbackViews.TryGetValue(bullet.ID, out var go))
        {
            if (go != null) Object.Destroy(go);
            fallbackViews.Remove(bullet.ID);
        }
        bullet.Dispose();
    }
}
