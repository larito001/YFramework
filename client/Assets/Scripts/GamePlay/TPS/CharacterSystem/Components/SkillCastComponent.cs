using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 技能释放组件（取代旧 MeleeComponent）：把"全身不可打断战斗动作"统一成**技能**（<see cref="SkillDef"/>）执行。
/// owns 整条技能的执行——时间线 + 命中窗伤害 + 位移 + 相机震屏 + 锁定 + 把当前段 clip 交给动画 controller：
///   - **不可打断**：<see cref="Cast"/> 在已有技能播放时直接拒绝；释放途中 Move/Aim/Weapon 全被 <see cref="Character.IsCastingSkill"/> 门控
///   - **逐段推进**：每段播到 clip.length 自然结束（或 HoldDuration 循环够秒）后进下一段，最后一段完 → 回 locomotion
///   - **位移**：按段 ForwardDistance + DistanceProfile 逐帧写 <see cref="Character.WishVelocity"/> 的 x/z（纵向 y 留给 GravityComponent）
///   - **伤害**：段内命中窗 [StartNorm,EndNorm] 每帧球形 OverlapSphere，每个窗对同一目标只扣一次（per-window 去重，支持同段多窗连击）
///
/// 触发：订阅同 Actor 输入组件的 <see cref="InputComponentBase.OnCastSkill"/>（玩家 V 键→0；AI→随机下标）；也可外部直接调 <see cref="Cast"/>。
///
/// Add 顺序：必须在 MoveComponent **之后**（前冲位移覆写 WishVelocity.x/z，Move 在前会被抹掉），Gravity **之前**（Gravity 定 y）；输入组件之后。
/// </summary>
public class SkillCastComponent : ICharacterComponent
{
    /// <summary>本角色可释放的技能（Resources 相对路径，Attach 时 Load 成 <see cref="SkillDef"/>）。下标即 OnCastSkill/Cast 的索引。</summary>
    public List<string> SkillPaths = new List<string>();
    /// <summary>OverlapSphere 物理 layer 过滤。生产期设成仅含敌人层。默认 ~0 全开（调试）。</summary>
    public LayerMask HitLayers = ~0;

    private readonly List<SkillDef> skills = new List<SkillDef>();

    // ── runtime ──
    private SkillDef active;
    private int segIndex;
    private float segElapsed;
    private float segDuration;          // 当前段时长（HoldDuration 或 clip.length）
    private float segPrevDistFrac;      // 上帧已位移占比
    private Vector3 castForward;        // 起技能时锁定的水平前向（释放途中 Aim 被门控，朝向冻结）
    // per-window 去重 + 已震屏标记（StartSegment 清）
    private readonly Dictionary<SkillDef.HitWindow, HashSet<int>> windowHits = new Dictionary<SkillDef.HitWindow, HashSet<int>>();
    private readonly HashSet<SkillDef.HitWindow> shakenWindows = new HashSet<SkillDef.HitWindow>();

    private YOTO.ResMgr resMgr;
    private ActorWorld world;
    private CameraManager cameraMgr;
    private InputComponentBase input;
    private static readonly Collider[] overlapBuf = new Collider[16];

    public override void Attach(Character owner)
    {
        base.Attach(owner);
        Ctx?.TryGet(out resMgr);
        Ctx?.TryGet(out world);
        Ctx?.TryGet(out cameraMgr);

        // 加载技能资产
        skills.Clear();
        if (resMgr != null)
        {
            for (int i = 0; i < SkillPaths.Count; i++)
            {
                var def = string.IsNullOrEmpty(SkillPaths[i]) ? null : resMgr.Load<SkillDef>(SkillPaths[i]);
                if (def == null && !string.IsNullOrEmpty(SkillPaths[i]))
                    Debug.LogWarning($"[SkillCastComponent] SkillDef 加载失败: {SkillPaths[i]}");
                skills.Add(def);
            }
        }

        // 订阅输入组件的释放技能事件（玩家 InputComponent / AI AIInputComponent 都走这条）
        input = owner.Get<InputComponentBase>();
        if (input != null) input.OnCastSkill += Cast;
    }

    public override void Detach()
    {
        if (input != null) input.OnCastSkill -= Cast;
        // 清自己写过的 Owner 字段，避免 writer 离场后 reader 读到死值卡住 Move/Weapon/view
        if (Owner != null)
        {
            Owner.IsCastingSkill = false;
            Owner.SkillClip = null;
            Owner.SkillClipDirty = false;
            Owner.SkillClipFade = 0f;
            Owner.SkillRecoverFade = 0f;
        }
        active = null;
        windowHits.Clear();
        shakenWindows.Clear();
        skills.Clear();
        resMgr = null;
        world = null;
        cameraMgr = null;
        input = null;
        base.Detach();
    }

    /// <summary>释放第 index 个技能。已在释放中（不可打断）/ 死亡 静默忽略；越界 / 资产空 报 warning。</summary>
    public void Cast(int index)
    {
        if (Owner == null || Owner.IsDead) return;
        if (active != null) return;                          // 不可打断
        if (index < 0 || index >= skills.Count)
        {
            Debug.LogWarning($"[SkillCastComponent] Cast 技能下标越界: {index}（共 {skills.Count} 个技能）。Actor {Owner.ID}");
            return;
        }
        var def = skills[index];
        if (def == null || def.Segments == null || def.Segments.Length == 0)
        {
            Debug.LogWarning($"[SkillCastComponent] Cast 技能 [{index}] 资产空 / 无 segment（路径 {(index < SkillPaths.Count ? SkillPaths[index] : "?")}）。Actor {Owner.ID}");
            return;
        }

        active = def;
        Owner.IsCastingSkill = true;
        // 锁定前向（释放途中 Aim 被门控，朝向冻结，这里取一次即可）
        var fwd = Owner.Rotation * Vector3.forward;
        fwd.y = 0f;
        castForward = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
        StartSegment(0);
    }

    public override void Tick(float dt)
    {
        if (Owner == null) return;

        // 死亡打断技能
        if (Owner.IsDead)
        {
            if (active != null) EndCast();
            return;
        }
        if (active == null) return;

        // 退化段（无 clip 且无 HoldDuration → segDuration<=0）：跳过，不做 n=1 的瞬移/漏命中
        if (segDuration <= 0f) { AdvanceOrEnd(); return; }

        segElapsed += dt;
        float n = Mathf.Clamp01(segElapsed / segDuration);
        var seg = active.Segments[segIndex];

        // 1. 位移：权威写 x/z（杜绝释放期间滑步），y 不动留给 Gravity
        float fracNow = SampleProfile(seg.DistanceProfile, n);
        float dFrac = fracNow - segPrevDistFrac;
        segPrevDistFrac = fracNow;
        float v = (dt > 0f && Mathf.Abs(seg.ForwardDistance) > 1e-5f) ? seg.ForwardDistance * dFrac / dt : 0f;
        Owner.WishVelocity = new Vector3(castForward.x * v, Owner.WishVelocity.y, castForward.z * v);

        // 2. 伤害 + 相机震屏：遍历所有含 n 的命中窗（支持同段多窗），每窗独立去重，窗首次开启震一下
        if (seg.HitWindows != null)
        {
            for (int i = 0; i < seg.HitWindows.Length; i++)
            {
                var w = seg.HitWindows[i];
                if (w == null || n < w.StartNorm || n > w.EndNorm) continue;
                TryShake(w);
                DoHit(w);
            }
        }

        // 3. 段结束 → 下一段 / 结束技能
        if (segElapsed >= segDuration) AdvanceOrEnd();
    }

    private void AdvanceOrEnd()
    {
        if (segIndex + 1 < active.Segments.Length) StartSegment(segIndex + 1);
        else EndCast();
    }

    private void StartSegment(int i)
    {
        segIndex = i;
        segElapsed = 0f;
        segPrevDistFrac = 0f;
        windowHits.Clear();
        shakenWindows.Clear();
        var seg = active.Segments[i];
        segDuration = seg.HoldDuration > 0f ? seg.HoldDuration : (seg.Clip != null ? seg.Clip.length : 0f);
        if (segDuration <= 0f)
            Debug.LogWarning($"[SkillCastComponent] 技能 \"{active.Name}\" 第 {i} 段无 clip 且 HoldDuration<=0，本段被跳过（不会播放/命中/位移）。");
        // 交 clip 给动画 controller（基类 LocomotionAnimController 的技能全身分支消费）
        Owner.SkillClip = seg.Clip;
        Owner.SkillClipFade = seg.Fade;
        Owner.SkillClipDirty = true;
    }

    private void EndCast()
    {
        if (Owner != null)
        {
            Owner.SkillRecoverFade = active != null ? active.RecoverFade : 0f;
            Owner.IsCastingSkill = false;
            // 停下前冲：清水平意图，y 留给 Gravity
            Owner.WishVelocity = new Vector3(0f, Owner.WishVelocity.y, 0f);
        }
        active = null;
        windowHits.Clear();
        shakenWindows.Clear();
    }

    /// <summary>命中窗首次开启那帧触发一次相机震屏（kickback：相机往挥击反向顿一下），与是否打中解耦。</summary>
    private void TryShake(SkillDef.HitWindow w)
    {
        if (active == null || active.ShakeIntensity <= 0f) return;
        if (shakenWindows.Contains(w)) return;
        shakenWindows.Add(w);
        if (cameraMgr?.Shake != null)
            cameraMgr.Shake.Shake(-castForward, active.ShakeDuration, active.ShakeIntensity);
    }

    private void DoHit(SkillDef.HitWindow w)
    {
        if (world == null) return;
        if (!windowHits.TryGetValue(w, out var hit))
        {
            hit = new HashSet<int>();
            windowHits[w] = hit;
        }
        var center = Owner.Position + castForward * w.ForwardOffset + Vector3.up * w.Height;
        int count = Physics.OverlapSphereNonAlloc(center, w.Radius, overlapBuf, HitLayers);
        for (int i = 0; i < count; i++)
        {
            int id = DamageRouter.ResolveActorId(overlapBuf[i], Owner.ID);
            if (id < 0 || hit.Contains(id)) continue;
            hit.Add(id);
            var info = DamageInfo.Build(in w.Damage, Owner.ID, Owner.TeamId);
            DamageRouter.ApplyToActor(world, id, in info);
        }
    }

    /// <summary>位移分布曲线采样：有效曲线（≥2 帧）按曲线，否则线性。</summary>
    private static float SampleProfile(AnimationCurve curve, float n)
        => (curve != null && curve.length >= 2) ? Mathf.Clamp01(curve.Evaluate(n)) : n;
}
