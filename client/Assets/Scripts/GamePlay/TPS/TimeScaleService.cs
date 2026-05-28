using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 受击卡肉分级。具体 (duration, scale) 在 <see cref="TimeScaleService"/> 上配，
/// 攻击端（bullet/melee/hitscan）只决定"用哪一级"，伤害链通过 <see cref="DamageRouter"/> 把 tier 透传到 HealthComponent。
/// 默认 Long——所有 ApplyDamage / DamageRouter 入口的 tier 参数 default 都是 Long。
/// </summary>
public enum HitstopTier
{
    /// <summary>不卡肉。环境伤害 / DOT / 不打断对手节奏的小伤害用。</summary>
    None = 0,
    /// <summary>短卡肉。点射 / 高频武器，避免连发把对手钉死。</summary>
    Short = 1,
    /// <summary>长卡肉。重武器 / 近战 / 默认值，给玩家清晰的命中反馈。</summary>
    Long = 2,
}

/// <summary>
/// 时间缩放服务：统一管理"卡肉 (hit-stop)"、局部慢动作、全局慢动作。
///
/// 设计原则：
///   - **受击 hit-stop 默认走全局缩放**（<see cref="GlobalHitstop"/>，写 Time.timeScale=0）——
///     攻击者动画、子弹、Animator、Physics 全部一起冻一小段，反馈最强。
///     按 actor 局部冻只能让被打的人冻住，俯视角下不易察觉，且攻击者动作会"穿过"冻住的目标。
///   - <see cref="HitstopByTier"/>（伤害链调用入口）默认走 GlobalHitstop。
///   - **局部缩放 API 保留**（<see cref="Hitstop"/> + <see cref="Actor.TimeScale"/>）给"特定角色减速 / 个别 buff"用，
///     调用 Hitstop(actorId, ...) 仍然可用，伤害链不走这条。
///   - 所有 timer 用 <c>Time.unscaledDeltaTime</c> 推进——Time.timeScale=0 时常规 dt=0，自己解不开。
///
/// Tick 顺序：必须在 CharacterManager / WeaponManager / BulletManager **之前**注册。
///
/// 扩展方向：
///   - 攻击者一起卡肉：当前是全局已包含所有人；要"只卡攻击+被攻击"对，改 HitstopByTier 走 per-actor 双发
///   - 子弹时间：GlobalScale=0.2 + 玩家 actor.TimeScale=5 抵消（玩家正常，世界慢）
///   - 区域慢动作：扫描某球内 actor 批量调 Hitstop(id, ...) 用局部缩放
/// </summary>
public class TimeScaleService : IGameService, ITickable
{
    private ActorWorld world;

    /// <summary>(剩余时长, 期间使用的 scale)。timer 用 Time.unscaledDeltaTime 推进，
    /// 不被全局慢动作 / hitstop 影响（否则全局停时 per-actor 也跟着永远不解）。</summary>
    private readonly Dictionary<int, (float remaining, float scale)> hitstop
        = new Dictionary<int, (float, float)>();
    private readonly List<int> tickKeys = new List<int>();   // 复用避免每帧 alloc
    private readonly List<int> toRemove = new List<int>();

    // ── 全局 hitstop 状态 ──
    private float globalHitstopRemaining;     // >0 表示全局卡肉中，timer 用 unscaledDeltaTime 推
    private float globalHitstopRestoreScale = 1f; // 卡肉结束时恢复的 Time.timeScale（进入时记录的"原值"）
    private float globalHitstopActiveScale = 1f;  // 进入时写到 Time.timeScale 的值。恢复时如果 Time.timeScale != active，
                                                  // 说明 hitstop 期间外部（暂停/慢动作系统）改过 scale，尊重外部值不覆盖

    public void Init(GameContext ctx)
    {
        world = ctx.Get<ActorWorld>();
    }

    public void Shutdown()
    {
        // Shutdown 时如果仍在 hitstop，需要安全恢复 Time.timeScale，否则进程级状态会被卡在 0
        // （Unity 的 Time.timeScale 不会随 GameContext 销毁自动重置）。
        if (globalHitstopRemaining > 0f && Mathf.Approximately(Time.timeScale, globalHitstopActiveScale))
            Time.timeScale = globalHitstopRestoreScale;
        globalHitstopRemaining = 0f;
        hitstop.Clear();
        tickKeys.Clear();
        toRemove.Clear();
    }

    /// <summary>全局时间缩放。直接读写 <c>Time.timeScale</c>。0=完全暂停（Update / Animator / Physics 全停）。
    /// 注意：&lt;0 会被夹到 0；想做"倒放"那种效果不能用这个，得做反向播放。</summary>
    public float GlobalScale
    {
        get => Time.timeScale;
        set => Time.timeScale = Mathf.Max(0f, value);
    }

    // ── 卡肉分级配置：tier → (duration, scale) ──
    // 攻击端只挑 tier，不关心具体参数；策划手感调整在这里集中改。
    // scale=0 = 完全冻结。时长是经典动作游戏的全局 hit-stop 量级（短的 2~3 帧，长的 5~8 帧 @60fps）。
    /// <summary>Short tier 的卡肉时长（秒）。高频射击连发用，约 2 帧。</summary>
    public float ShortHitstopDuration = 0.03f;
    /// <summary>Short tier 的卡肉 scale。0=完全冻结。</summary>
    public float ShortHitstopScale = 0f;
    /// <summary>Long tier 的卡肉时长（秒）。近战 / 重武器 / 默认值，约 6 帧。</summary>
    public float LongHitstopDuration = 0.10f;
    /// <summary>Long tier 的卡肉 scale。0=完全冻结。</summary>
    public float LongHitstopScale = 0f;

    /// <summary>按 tier 触发卡肉。None 静默返回；Short/Long 走 <see cref="GlobalHitstop"/>。
    /// 攻击端（HealthComponent / 各 FireEffect）统一走这个入口。
    /// actorId 当前没用（全局卡肉不区分对象），保留参数方便后续做 log / 调试 / 个别 actor 免疫扩展。</summary>
    public void HitstopByTier(int actorId, HitstopTier tier)
    {
        switch (tier)
        {
            case HitstopTier.None: return;
            case HitstopTier.Short: GlobalHitstop(ShortHitstopDuration, ShortHitstopScale); break;
            case HitstopTier.Long:  GlobalHitstop(LongHitstopDuration,  LongHitstopScale);  break;
        }
    }

    /// <summary>全局 hit-stop：Time.timeScale=scale 持续 duration 秒后恢复。
    /// 期间 Animator/Particle/Physics/所有 ITickable.Tick(dt) 拿到的 dt 都是 0（除 service 本身用 unscaledDt）。
    /// 重叠触发：取较长剩余时长（连击不被新一发短卡肉缩短），scale 用最新一次的。
    /// 注意：进入卡肉时记录当时的 Time.timeScale，到点恢复（兼容全局慢动作场景）。</summary>
    public void GlobalHitstop(float duration, float scale)
    {
        if (duration <= 0f) return;
        scale = Mathf.Max(0f, scale);

        // 首次进入卡肉时锁定恢复值。后续重叠触发不刷新（避免把已经设成 scale=0 的当前值当成"原值"记下来）
        if (globalHitstopRemaining <= 0f)
            globalHitstopRestoreScale = Time.timeScale;

        globalHitstopRemaining = Mathf.Max(globalHitstopRemaining, duration);
        globalHitstopActiveScale = scale;
        Time.timeScale = scale;
    }

    /// <summary>对指定 actor 发起一次卡肉。<paramref name="duration"/> 时长内
    /// <see cref="Character.TimeScale"/> 被设成 <paramref name="scale"/>，到点恢复 1。
    /// 重复触发时：剩余时长取较长的一边（连击不被新一发短卡肉缩短），scale 用最新一次（最强一次主导）。
    /// scale=0 表示完全冻结；典型卡肉 0.04~0.10。duration 一般 0.04~0.12s。</summary>
    public void Hitstop(int actorId, float duration, float scale)
    {
        if (actorId < 0 || duration <= 0f) return;
        scale = Mathf.Max(0f, scale);

        float newRemaining = duration;
        if (hitstop.TryGetValue(actorId, out var existing) && existing.remaining > duration)
            newRemaining = existing.remaining;

        hitstop[actorId] = (newRemaining, scale);
        WriteActorScale(actorId, scale);
    }

    /// <summary>强制结束某 actor 的卡肉，立刻把 TimeScale 恢复 1。一般不用调，timer 自己会到点。
    /// 适合"复活 / 状态切换"主动清理。</summary>
    public void ClearHitstop(int actorId)
    {
        if (!hitstop.Remove(actorId)) return;
        WriteActorScale(actorId, 1f);
    }

    public void Tick(float dt)
    {
        // 用 unscaledDeltaTime 推所有 timer：全局 hitstop 把 Time.timeScale=0 时，传入的 dt 也是 0，
        // 如果用 dt 推，timer 永远不到点，自己解不开。
        float realDt = Time.unscaledDeltaTime;

        // 1. 全局 hitstop 倒计时
        if (globalHitstopRemaining > 0f)
        {
            globalHitstopRemaining -= realDt;
            if (globalHitstopRemaining <= 0f)
            {
                globalHitstopRemaining = 0f;
                // 仅当 Time.timeScale 仍是我们设的 active 值时才恢复——hitstop 期间外部（暂停/慢动作系统）
                // 改过 scale 的话尊重外部值，避免拿"卡肉前"的旧快照覆盖外部决定。
                if (Mathf.Approximately(Time.timeScale, globalHitstopActiveScale))
                    Time.timeScale = globalHitstopRestoreScale;
            }
        }

        // 2. per-actor hitstop 倒计时
        if (hitstop.Count == 0) return;

        tickKeys.Clear();
        foreach (var k in hitstop.Keys) tickKeys.Add(k);

        toRemove.Clear();
        for (int i = 0; i < tickKeys.Count; i++)
        {
            var id = tickKeys[i];
            var st = hitstop[id];
            st.remaining -= realDt;
            if (st.remaining <= 0f) toRemove.Add(id);
            else hitstop[id] = st;
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            var id = toRemove[i];
            hitstop.Remove(id);
            WriteActorScale(id, 1f);
        }
    }

    private void WriteActorScale(int actorId, float scale)
    {
        // TimeScale 在 Actor 基类上 → 任何 Actor 子类（Character/Weapon/Bullet/...）都能被缩放。
        // Actor.Tick 内部消费这个字段；View 端按需自己读（CharacterView 已经处理）。
        if (world == null || !world.TryGet(actorId, out var actor)) return;
        if (actor != null) actor.TimeScale = scale;
    }
}
