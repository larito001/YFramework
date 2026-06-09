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

    [Header("近战吸附 (Melee Snap / Magnetism)")]
    /// <summary>开启近战吸附：起手锁前向锥内最佳敌人 → 即时转向它 + 前冲主动收敛到其身前站位（双吸附）。false=旧行为（按当前朝向直冲）。</summary>
    public bool SnapEnabled = true;
    /// <summary>捕获半径（米）：起手在此半径内找敌人。建议 ≈ 段最大 ForwardDistance + SnapDesiredGap，保证锁到的都够得着。</summary>
    public float SnapRange = 3.5f;
    /// <summary>捕获半角（度）：只锁前向 ±此角度锥内的敌人，避免吸到背后。70 ≈ God of War 宽容档。</summary>
    public float SnapHalfAngle = 70f;
    /// <summary>站位间隙（米）：吸附前冲到距目标中心这么近就停，停在其胶囊外侧——杜绝怼进身体被 collide-and-slide 顶歪。≈ 双方胶囊半径之和 + 余量。</summary>
    public float SnapDesiredGap = 1.1f;
    /// <summary>释放途中持续转向目标的 slerp 速率（指数收敛，帧率无关）。25~35 ≈ 5 帧追平，有起手转身感不硬切。</summary>
    public float SnapTurnRate = 30f;

    private readonly List<SkillDef> skills = new List<SkillDef>();

    /// <summary>已加载的可释放技能数量（= SkillPaths 数）。AI 取随机技能下标用，避免在别处手抄数量。</summary>
    public int SkillCount => skills.Count;

    /// <summary>是否正在释放技能。连招前端（<see cref="ComboComponent"/>）据此判断"起手 vs 接招 vs 掉连归零"。</summary>
    public bool IsCasting => active != null;
    /// <summary>当前是否可被下一击打断接招（= 已进入取消窗 <see cref="InCancelWindow"/>）。ComboComponent 据此决定接不接下一招。</summary>
    public bool CanChainNow => InCancelWindow();

    // ── runtime ──
    private SkillDef active;
    private int segIndex;
    private float segElapsed;
    private float segDuration;          // 当前段时长（HoldDuration 或 clip.length）
    private float segPrevDistFrac;      // 上帧已位移占比
    private Vector3 castForward;        // 当前水平前向：无吸附=起手锁定不变；有吸附=每帧跟随活目标（Aim 被门控，本组件权威写朝向）
    private int snapTargetId = -1;      // 近战吸附锁定的目标 actor.ID；-1=无（走原直冲）。Cast 时 acquire，EndCast/Detach 清
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
    // 命中 / 吸附各自一份**实例** buffer（不再 static 共享）：杜绝"命中→同帧触发反击 Cast"之类重入把对方的扫描结果踩掉。
    // 代价是每个角色多两个定长数组（几百字节），角色量级下可忽略。
    private readonly Collider[] overlapBuf = new Collider[16];   // DoHit 命中检测用
    private readonly Collider[] snapBuf = new Collider[32];      // AcquireSnapTarget 吸附候选粗筛用（与命中分开）

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
        snapTargetId = -1;
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

    /// <summary>释放第 index 个技能（指向 <see cref="SkillPaths"/>）。越界报 warning；其余门控见 <see cref="Cast(SkillDef)"/>。
    /// AI 走这条（OnCastSkill 订阅）；玩家连招走 <see cref="ComboComponent"/> → <see cref="Cast(SkillDef)"/>。</summary>
    public void Cast(int index)
    {
        if (index < 0 || index >= skills.Count)
        {
            Debug.LogWarning($"[SkillCastComponent] Cast 技能下标越界: {index}（共 {skills.Count} 个技能）。Actor {Owner?.ID}");
            return;
        }
        Cast(skills[index]);
    }

    /// <summary>释放指定 <see cref="SkillDef"/>。已在释放中（不可打断，除非进入取消窗→连招打断）/ 死亡 / 切枪过场中 静默忽略；资产空报 warning。
    /// 连招前端（<see cref="ComboComponent"/>）按招式图直接传 def 调本方法；StartSegment(0) 会清掉上一招残留的命中窗/动效/位移记录。</summary>
    public void Cast(SkillDef def)
    {
        if (Owner == null || Owner.IsDead) return;
        if (Owner.IsSwapping) return;                        // 切枪过场中不能放技能（与"技能中不能切枪"对称）
        if (Owner.IsDodging) return;                         // 闪避中不能放技能（两类全身动作互斥；与"闪避门控 IsBusy"对称）
        if (active != null && !InCancelWindow()) return;     // 不可打断——除非已进入取消窗（命中后摇可被下一击打断 = 连招）
        if (def == null || def.Segments == null || def.Segments.Length == 0)
        {
            Debug.LogWarning($"[SkillCastComponent] Cast 技能资产空 / 无 segment。Actor {Owner.ID}");
            return;
        }

        active = def;
        Owner.IsCastingSkill = true;
        // 起手前向：先取当前朝向（无吸附时即为最终前向）
        var fwd = Owner.Rotation * Vector3.forward;
        fwd.y = 0f;
        castForward = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
        // 近战吸附：在前向锥内挑目标，命中则即时转向它（首帧硬转，GoW soft-target），castForward 改指目标
        if (SnapEnabled) AcquireSnapTarget();
        StartSegment(0);
    }

    /// <summary>近战吸附目标捕获：在 castForward 前向 ±SnapHalfAngle 锥内、SnapRange 半径内挑最佳敌人
    /// （角度优先、距离次之——先吸你正对着的，商业动作游戏通用打分）。命中则锁 snapTargetId 并**首帧即时转向**
    /// （GoW "按下攻击即转向"）。找不到 → snapTargetId=-1，本次回退原"按朝向直冲"。
    /// 筛选复用 AIInputComponent / TowerTargetingComponent 同款：非自己 + 非中立 + 阵营不同 + 有 HealthComponent + 未死。
    ///
    /// **候选来源**：先用 <see cref="Physics.OverlapSphereNonAlloc"/> 在 SnapRange 内按 HitLayers 粗筛（交给物理空间分区），
    /// 候选数从"全场 actor"降到"球内 collider"，再跑原有锥角/阵营/血量过滤 + 打分。
    /// 同一 actor 多 collider 会被打分多次但取同一最优，无害；snapBuf 满（>32 collider 在范围内）时多余候选被丢，近战吸附可接受。</summary>
    private void AcquireSnapTarget()
    {
        snapTargetId = -1;
        if (world == null) return;

        float cosHalf = Mathf.Cos(SnapHalfAngle * Mathf.Deg2Rad);
        float rangeSqr = SnapRange * SnapRange;
        float bestScore = float.MaxValue;
        Actor best = null;
        var selfPos = Owner.Position;
        int selfTeam = Owner.TeamId;

        int count = Physics.OverlapSphereNonAlloc(selfPos, SnapRange, snapBuf, HitLayers);
        for (int i = 0; i < count; i++)
        {
            int id = DamageRouter.ResolveActorId(snapBuf[i], Owner.ID);   // 反查 actor + 过滤自身
            if (id < 0 || !world.TryGet(id, out var a) || a == null || a == Owner) continue;
            if (a.TeamId == 0 || a.TeamId == selfTeam) continue;     // 友军 / 中立不吸
            if (a.IsDead || a.Get<HealthComponent>() == null) continue;

            var to = a.Position - selfPos; to.y = 0f;                // 水平距离
            float sqr = to.x * to.x + to.z * to.z;
            if (sqr > rangeSqr || sqr < 1e-6f) continue;             // 超出捕获半径（水平）
            var dir = to / Mathf.Sqrt(sqr);
            float dot = castForward.x * dir.x + castForward.z * dir.z;
            if (dot < cosHalf) continue;                             // 锥外（含背后）不吸

            // 打分：角度差(1-dot) 占大头，距离作次权——保证"吸正对着的"而非"吸最近的"
            float score = (1f - dot) * 2f + Mathf.Sqrt(sqr) * 0.1f;
            if (score < bestScore) { bestScore = score; best = a; }
        }

        if (best == null) return;
        snapTargetId = best.ID;
        var d = best.Position - selfPos; d.y = 0f;
        if (d.sqrMagnitude > 1e-4f)
        {
            castForward = d.normalized;
            Owner.Rotation = Quaternion.LookRotation(castForward, Vector3.up); // 首帧即时转向（释放途中 Aim 被门控）
        }
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

        // 1. 位移 + 吸附（权威写 x/z 杜绝滑步，y 留给 Gravity）：
        //    本段 TrackTarget 开 且 有吸附目标 → 朝其"身前站位点"主动收敛（位移吸附/suction）+ 持续转向（转向吸附）；
        //    本段 TrackTarget 关 / 无目标 → 回退原"沿 castForward 走 authored ForwardDistance"（本段不跟人）。
        float fracNow = SampleProfile(seg.DistanceProfile, n);
        Vector3 planar;
        if (seg.TrackTarget && snapTargetId >= 0 && world != null && world.TryGet(snapTargetId, out var snapT) && !snapT.IsDead)
        {
            var toT = snapT.Position - Owner.Position; toT.y = 0f;
            float dist = toT.magnitude;
            Vector3 dirT = dist > 1e-4f ? toT / dist : castForward;

            // 转向吸附：每帧快 slerp 跟随活目标（castForward 同步，hitbox/VFX 自动贴着目标）
            castForward = dirT;
            if (dt > 0f)
            {
                var rot = Quaternion.LookRotation(dirT, Vector3.up);
                Owner.Rotation = Quaternion.Slerp(Owner.Rotation, rot, 1f - Mathf.Exp(-SnapTurnRate * dt));
            }

            if (seg.ForwardDistance > 1e-5f)
            {
                // 前冲招：位移吸附——朝"身前站位点"主动收敛，reach 被 authored ForwardDistance 限幅（=最大吸附距离，防跨场拉拽）。
                float gap = Mathf.Max(0f, dist - SnapDesiredGap);
                float reach = Mathf.Min(gap, seg.ForwardDistance);
                // 用 DistanceProfile 把"剩余距离"铺到"剩余段时间"：覆盖剩余 profile 占比 → 目标移动也平滑收敛，段末必达
                float profRemain = 1f - segPrevDistFrac;
                float frac = profRemain > 1e-4f ? Mathf.Clamp01((fracNow - segPrevDistFrac) / profRemain) : 1f;
                float vmag = dt > 0f ? reach * frac / dt : 0f;
                planar = dirT * vmag;
            }
            else
            {
                // 原地（=0）/ 后退（<0）招：不做距离吸附，按 authored 位移沿 castForward（已转向目标，负值=远离目标后退）。
                // 仍享转向吸附（上面已转向目标），只是位移不收敛——撤步/原地攻击不会被吸附清零。
                float dFrac = fracNow - segPrevDistFrac;
                float vmag = (dt > 0f && Mathf.Abs(seg.ForwardDistance) > 1e-5f) ? seg.ForwardDistance * dFrac / dt : 0f;
                planar = castForward * vmag;
            }
        }
        else
        {
            float dFrac = fracNow - segPrevDistFrac;
            float vmag = (dt > 0f && Mathf.Abs(seg.ForwardDistance) > 1e-5f) ? seg.ForwardDistance * dFrac / dt : 0f;
            planar = castForward * vmag;
        }
        segPrevDistFrac = fracNow;
        Owner.WishVelocity = new Vector3(planar.x, Owner.WishVelocity.y, planar.z);

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

        // 3. 段结束 / 移动取消：
        //    无后续输入 → 播完整段（完整后摇，不提前结束）。
        //    仅当本段开了 MoveCancelable：进入取消窗后玩家有移动意图 → 提前 EndCast 脱离收招、恢复移动。
        //    连招的"下一击"取消走 Cast（见 InCancelWindow），与移动取消解耦——所以连招招式可 MoveCancelable=false，
        //    避免"一边走一边连招"时被走位掐断（移动取消默认关，按需 per-skill 开）。
        if (segElapsed >= segDuration) { AdvanceOrEnd(); return; }
        float cancelN = seg.CancelFromNorm > 0f ? Mathf.Clamp01(seg.CancelFromNorm) : 1f;
        if (seg.MoveCancelable && cancelN < 1f && n >= cancelN && input != null && input.MoveWorld.sqrMagnitude > 0.01f)
            EndCast();
    }

    private void AdvanceOrEnd()
    {
        if (segIndex + 1 < active.Segments.Length) StartSegment(segIndex + 1);
        else EndCast();
    }

    /// <summary>当前技能是否已进入"取消窗"（活动段 n >= CancelFromNorm 且该段开了取消窗）。
    /// <see cref="Cast"/> 据此决定能否在释放途中被下一击打断（连招）。无 active / 该段未开窗（CancelFromNorm>=1 或 <=0）→ false。</summary>
    private bool InCancelWindow()
    {
        if (active == null || segDuration <= 0f) return false;
        var seg = active.Segments[segIndex];
        float cancelN = seg.CancelFromNorm > 0f ? Mathf.Clamp01(seg.CancelFromNorm) : 1f;
        if (cancelN >= 1f) return false;
        return Mathf.Clamp01(segElapsed / segDuration) >= cancelN;
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
        snapTargetId = -1;
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
