using System.Collections.Generic;
using UnityEngine;
using YOTO;

/// <summary>
/// 通用特效(VFX)服务：按 Resources 路径**池化**播放粒子/特效 prefab。技能(<see cref="SkillCastComponent"/>)、
/// 命中反馈等都调它，不再各自 Instantiate/Destroy。两种播法：
///   - <see cref="Play"/>：世界点**一次性**——生成在世界坐标、不跟随，按 prefab 粒子时长自动回池（fire-and-forget）。
///   - <see cref="PlayAttached"/>：**跟随角色**——挂到 actor 的 view transform 下跟着走，返回 handle；
///     调用方到时机用 <see cref="Stop"/> 回收（如技能段 EndNorm）。适合光环/充能等持续特效。
///
/// 每个 path 懒建一个 <see cref="PrefabPool{T}"/>（T=Transform，任意 prefab 都有）；加载失败的 path 缓存 null 不再重试。
/// 回收：世界一次性走 Tick 倒计时，跟随型走 Stop——和 BulletManager 的 spawn/despawn 一个套路。
/// 注册在 ViewManager / ResMgr 之后（Init 里 Get 它们）。
/// </summary>
public class VfxManager : IGameService, ITickable
{
    /// <summary>非粒子 prefab（取不到 ParticleSystem 时长）的兜底自动回收时长（秒）。</summary>
    public float DefaultLifetime = 2f;

    private ResMgr resMgr;
    private ViewManager viewMgr;

    private class PoolEntry
    {
        public PrefabPool<Transform> pool;
        public Vector3 baseScale;
    }
    private readonly Dictionary<string, PoolEntry> pools = new Dictionary<string, PoolEntry>();

    private class Active
    {
        public int handle;
        public string path;
        public Transform tr;
        public float remaining;   // >0 倒计时自动回收（世界一次性）；<0 = 仅 Stop 回收（跟随型）
    }
    private readonly List<Active> actives = new List<Active>();
    private int nextHandle = 1;

    public void Init(GameContext ctx)
    {
        resMgr = ctx.Get<ResMgr>();
        viewMgr = ctx.Get<ViewManager>();
    }

    public void Shutdown()
    {
        for (int i = 0; i < actives.Count; i++) ReleaseToPool(actives[i]);
        actives.Clear();
        foreach (var kv in pools)
        {
            if (kv.Value != null)
            {
                kv.Value.pool?.Dispose();
                resMgr?.Release<GameObject>(kv.Key);
            }
        }
        pools.Clear();
        resMgr = null;
        viewMgr = null;
    }

    /// <summary>世界点一次性特效（不跟随）。到粒子时长后自动回池。返回 handle（想提前停可 <see cref="Stop"/>，一般不用）。</summary>
    public int Play(string path, Vector3 worldPos, Quaternion worldRot, float scale = 1f)
    {
        var entry = GetPool(path);
        if (entry == null) return 0;
        var tr = entry.pool.Get(worldPos, worldRot, null);
        ApplyScale(tr, entry.baseScale, scale);
        float dur = PlayParticles(tr);
        var a = new Active { handle = nextHandle++, path = path, tr = tr, remaining = dur > 0f ? dur : DefaultLifetime };
        actives.Add(a);
        return a.handle;
    }

    /// <summary>跟随角色的特效：挂到 actor 的 view 下，localOffset/localRot 相对角色（x=右 y=上 z=前）。
    /// 返回 handle，由调用方到时机 <see cref="Stop"/> 回收。actor 没 view 则不播（返回 0）。</summary>
    public int PlayAttached(string path, int actorId, Vector3 localOffset, Quaternion localRot, float scale = 1f)
    {
        if (viewMgr == null || !viewMgr.TryGetView(actorId, out var view) || view == null) return 0;
        var entry = GetPool(path);
        if (entry == null) return 0;
        var tr = entry.pool.Get(Vector3.zero, Quaternion.identity, view.transform);
        tr.localPosition = localOffset;
        tr.localRotation = localRot;
        ApplyScale(tr, entry.baseScale, scale);
        PlayParticles(tr);
        var a = new Active { handle = nextHandle++, path = path, tr = tr, remaining = -1f }; // 仅 Stop 回收
        actives.Add(a);
        return a.handle;
    }

    /// <summary>提前停止并回收一个特效（跟随型用；世界一次性也可）。handle&lt;=0 或已回收则忽略。</summary>
    public void Stop(int handle)
    {
        if (handle <= 0) return;
        for (int i = 0; i < actives.Count; i++)
        {
            if (actives[i].handle == handle)
            {
                ReleaseToPool(actives[i]);
                actives.RemoveAt(i);
                return;
            }
        }
    }

    public void Tick(float dt)
    {
        for (int i = actives.Count - 1; i >= 0; i--)
        {
            var a = actives[i];
            if (a.remaining < 0f) continue; // 跟随型不自动回收
            a.remaining -= dt;
            if (a.remaining <= 0f)
            {
                ReleaseToPool(a);
                actives.RemoveAt(i);
            }
        }
    }

    private PoolEntry GetPool(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (pools.TryGetValue(path, out var entry)) return entry; // 命中（含缓存的 null 失败）
        if (resMgr == null) return null;
        var prefab = resMgr.Load<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning($"[VfxManager] 特效 prefab 加载失败: {path}");
            pools[path] = null; // 缓存失败，避免每帧重 Load
            return null;
        }
        entry = new PoolEntry
        {
            pool = new PrefabPool<Transform>(prefab.transform, prewarm: 2, maxSize: 32),
            baseScale = prefab.transform.localScale,
        };
        pools[path] = entry;
        return entry;
    }

    private static void ApplyScale(Transform tr, Vector3 baseScale, float scale)
    {
        tr.localScale = scale > 0f ? baseScale * scale : baseScale;
    }

    /// <summary>重启实例下所有粒子系统，返回最长时长(duration + startLifetime)用于自动回收。无粒子返回 0。</summary>
    private static float PlayParticles(Transform tr)
    {
        var systems = tr.GetComponentsInChildren<ParticleSystem>(true);
        float max = 0f;
        for (int i = 0; i < systems.Length; i++)
        {
            var ps = systems[i];
            ps.Clear(true);
            ps.Play(true);
            var main = ps.main;
            float d = main.duration + main.startLifetime.constantMax;
            if (d > max) max = d;
        }
        return max;
    }

    private void ReleaseToPool(Active a)
    {
        if (a == null || a.tr == null) return;
        if (pools.TryGetValue(a.path, out var entry) && entry != null)
            entry.pool.Release(a.tr);
    }
}
