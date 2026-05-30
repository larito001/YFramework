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

    /// <summary>已加载的可释放技能数量（= SkillPaths 数）。AI 取随机技能下标用，避免在别处手抄数量。</summary>
    public int SkillCount => skills.Count;

    // ── runtime ──
    private SkillDef active;
    private int segIndex;
    private float segElapsed;
    private float segDuration;          // 当前段时长（HoldDuration 或 clip.length）
    private float segPrevDistFrac;      // 上帧已位移占比
    private Vector3 castForward;        // 起技能时锁定的水平前向（释放途中 Aim 被门控，朝向冻结）
    // per-window 命中去重（StartSegment 清）
    private readonly Dictionary<SkillDef.HitWindow, HashSet<int>> windowHits = new Dictionary<SkillDef.HitWindow, HashSet<int>>();
    // VFX：本段已触发的动效（防重复生成）+ 跟随型动效的活跃 handle（到 EndNorm / 段切换 / 结束时 Stop）
    private readonly HashSet<SkillDef.SkillVfx> firedVfx = new HashSet<SkillDef.SkillVfx>();
    private readonly Dictionary<SkillDef.SkillVfx, int> activeAttachedVfx = new Dictionary<SkillDef.SkillVfx, int>();
    // 震屏：本段已触发的震屏（防重复，每项到 StartNorm 触发一次）
    private readonly HashSet<SkillDef.SkillShake> firedShake = new HashSet<SkillDef.SkillShake>();

    private YOTO.ResMgr resMgr;
    private ActorWorld world;
    private CameraManager cameraMgr;
    private VfxManager vfxMgr;
    private InputComponentBase input;
    private static readonly Collider[] overlapBuf = new Collider[16];

    public override void Attach(Character owner)
    {
        base.Attach(owner);
        Ctx?.TryGet(out resMgr);
        Ctx?.TryGet(out world);
        Ctx?.TryGet(out cameraMgr);
        Ctx?.TryGet(out vfxMgr);

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
        StopAllAttachedVfx(); // 离场前回收跟随型动效（vfxMgr 置 null 之前）
        firedVfx.Clear();
        firedShake.Clear();
        windowHits.Clear();
        skills.Clear();
        resMgr = null;
        world = null;
        cameraMgr = null;
        vfxMgr = null;
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

        // 2. 伤害：遍历所有含 n 的命中窗（支持同段多窗），每窗独立去重
        if (seg.HitWindows != null)
        {
            for (int i = 0; i < seg.HitWindows.Length; i++)
            {
                var w = seg.HitWindows[i];
                if (w == null || n < w.StartNorm || n > w.EndNorm) continue;
                DoHit(w);
            }
        }

        // 2b. 动效：到 StartNorm 生成（世界点一次性 / 跟随角色），跟随型到 EndNorm 销毁
        DriveVfx(seg, n);

        // 2c. 相机震屏：到各自 StartNorm 触发一次（与 HitWindow 解耦，独立配置）
        DriveShake(seg, n);

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
        // 进新段：清本段动效/震屏触发记录 + 停掉上一段残留的跟随型动效
        firedVfx.Clear();
        firedShake.Clear();
        StopAllAttachedVfx();
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
        StopAllAttachedVfx(); // 技能结束回收跟随型动效（世界一次性动效自销毁不管）
        firedVfx.Clear();
        firedShake.Clear();
        windowHits.Clear();
    }

    /// <summary>段内相机震屏驱动：到各自 StartNorm 触发一次 kickback 震屏（相机往技能反向顿一下），每项一段内只触发一次。</summary>
    private void DriveShake(SkillDef.SkillSegment seg, float n)
    {
        if (seg.Shake == null || cameraMgr?.Shake == null) return;
        for (int i = 0; i < seg.Shake.Length; i++)
        {
            var s = seg.Shake[i];
            if (s == null || s.Intensity <= 0f) continue;
            if (n < s.StartNorm || firedShake.Contains(s)) continue;
            firedShake.Add(s);
            cameraMgr.Shake.Shake(-castForward, s.Duration, s.Intensity);
        }
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

    /// <summary>段内动效驱动：到 StartNorm 生成（世界点一次性 / 跟随角色），每个动效一段内只生成一次；跟随型到 EndNorm 销毁。</summary>
    private void DriveVfx(SkillDef.SkillSegment seg, float n)
    {
        if (seg.Vfx == null || vfxMgr == null) return;
        for (int i = 0; i < seg.Vfx.Length; i++)
        {
            var v = seg.Vfx[i];
            if (v == null || string.IsNullOrEmpty(v.Path)) continue;

            if (n >= v.StartNorm && !firedVfx.Contains(v))
            {
                firedVfx.Add(v);
                var localRot = Quaternion.Euler(v.RotationEuler);
                if (v.AttachToOwner)
                {
                    int h = vfxMgr.PlayAttached(v.Path, Owner.ID, v.LocalOffset, localRot, v.Scale);
                    if (h != 0) activeAttachedVfx[v] = h;
                }
                else
                {
                    // 世界点：相对释放朝向(castForward)定位/旋转，生成后不跟随
                    var faceRot = castForward.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(castForward) : Owner.Rotation;
                    vfxMgr.Play(v.Path, Owner.Position + faceRot * v.LocalOffset, faceRot * localRot, v.Scale);
                }
            }

            // 跟随型到 EndNorm 销毁
            if (v.AttachToOwner && n > v.EndNorm && activeAttachedVfx.TryGetValue(v, out var hh))
            {
                vfxMgr.Stop(hh);
                activeAttachedVfx.Remove(v);
            }
        }
    }

    /// <summary>停掉并回收所有活跃的跟随型动效（段切换 / 技能结束 / 离场时调）。世界一次性动效不归这里管（自销毁）。</summary>
    private void StopAllAttachedVfx()
    {
        if (vfxMgr != null)
            foreach (var h in activeAttachedVfx.Values) vfxMgr.Stop(h);
        activeAttachedVfx.Clear();
    }

    /// <summary>位移分布曲线采样：有效曲线（≥2 帧）按曲线，否则线性。</summary>
    private static float SampleProfile(AnimationCurve curve, float n)
        => (curve != null && curve.length >= 2) ? Mathf.Clamp01(curve.Evaluate(n)) : n;
}
