using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 局部时间缩放圈的**纯逻辑**驱动服务。<see cref="TimeScaleZone"/> 在 OnEnable/OnDisable 把自己注册/注销进来；
/// 本服务每帧遍历 <see cref="ActorWorld"/> 里所有 Actor，按"位置是否落在某个圈内"算出区域缩放，写到
/// <see cref="Actor.ZoneScale"/>，再由 <see cref="Actor.Tick"/> 把它乘进 dt。
///
/// **不依赖任何 Unity 物理**：圈不再挂 Collider、不靠 OnTrigger 识别——角色和子弹一视同仁，都只做
/// 标量距离平方比较。原因：子弹没有 Collider、靠代码挪 transform，根本不产生触发事件；而触发器又会被
/// 弹道 raycast 命中导致子弹在圈表面消失。纯逻辑判断把这两类坑一次性绕开，行为也更可预测。
///
/// Tick 顺序：必须在 CharacterManager / BulletManager **之前**（在 GameBootstrapper 里紧跟 TimeScaleService 注册），
/// 保证本帧先写好 ZoneScale，随后各 Actor.Tick 读到的就是当帧值。
/// </summary>
public class TimeScaleZoneService : IGameService, ITickable
{
    private ActorWorld world;
    private readonly List<TimeScaleZone> zones = new List<TimeScaleZone>();
    private readonly List<Actor> actorBuf = new List<Actor>(); // 复用避免每帧 alloc
    private bool appliedAny;                                    // 上一帧是否写过非 1 的 ZoneScale（用于圈清空时一次性复位）

    public void Init(GameContext ctx) => world = ctx.Get<ActorWorld>();

    public void Shutdown()
    {
        zones.Clear();
        actorBuf.Clear();
        appliedAny = false;
    }

    public void Register(TimeScaleZone zone)
    {
        if (zone == null || zones.Contains(zone)) return;
        zones.Add(zone);
    }

    public void Unregister(TimeScaleZone zone)
    {
        if (zone == null) return;
        zones.Remove(zone);
    }

    public void Tick(float dt)
    {
        if (world == null) return;

        // 没有任何圈：仅在"上一帧还残留缩放"时做一次复位扫描，之后零开销跳过。
        if (zones.Count == 0)
        {
            if (!appliedAny) return;
            appliedAny = false;
            actorBuf.Clear();
            world.AppendAll(actorBuf);
            for (int i = 0; i < actorBuf.Count; i++)
                if (actorBuf[i] != null) actorBuf[i].ZoneScale = 1f;
            return;
        }

        appliedAny = true;
        actorBuf.Clear();
        world.AppendAll(actorBuf);
        for (int i = 0; i < actorBuf.Count; i++)
        {
            var a = actorBuf[i];
            if (a != null) a.ZoneScale = SampleScale(a.Position);
        }
    }

    /// <summary>查询世界坐标点所处的区域缩放：被多个圈覆盖时取最小（最强减速）；不在任何圈内返回 1。
    /// 纯标量距离平方比较，无物理查询、无 alloc。</summary>
    public float SampleScale(Vector3 worldPos)
    {
        float scale = 1f;
        for (int i = 0; i < zones.Count; i++)
        {
            var z = zones[i];
            if (z == null) continue;                 // 圈被 Destroy 但还没 Unregister 的边界帧
            float r = z.Radius;                        // 根 transform 不缩放 → 世界半径 = Radius
            if ((worldPos - z.transform.position).sqrMagnitude <= r * r)
                scale = Mathf.Min(scale, z.InsideScale);
        }
        return scale;
    }
}
