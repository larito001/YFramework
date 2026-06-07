using UnityEngine;

/// <summary>
/// **连招前端**（Character 侧，纯逻辑）：把玩家攻击键（<see cref="InputComponentBase.OnAttack"/>）按当前武器的
/// <see cref="ComboGraph"/> 路由成一连串 <see cref="SkillDef"/>，交 <see cref="SkillCastComponent"/> 执行。
/// 每招的位移 / 命中 / 吸附 / 取消窗仍归 SkillCast；本组件只管"按哪张图、接哪一招、何时接、缓冲输入"。
///
/// **两种模式（按当前武器自动切，看 Character.CurrentComboGraphPath 是否为空）**：
///   - **图模式**（配了 ComboGraph）：站立按键 → Neutral(节点0) 对应出边起手；释放途中进入取消窗
///     （<see cref="SkillCastComponent.CanChainNow"/>）按键 → 当前节点出边接下一招；掉连（收招结束没接上）→ 回 Neutral。
///   - **单招回退**（无图：枪 / 旧近战）：Light=放 WeaponPrimarySkill（&gt;=0 时；枪为 -1 → 忽略，左键开火走 FireHeld）、
///     Heavy=放 WeaponSecondarySkill（-1 回退技能 0）。**完全保留接连招前的行为**，连招是按武器 opt-in 的纯增量。
///
/// **输入缓冲**：按键早于取消窗时先缓存 <see cref="BufferWindow"/> 秒，窗一开立刻消费——连招手感的关键，避免"卡帧点"。
///
/// Add 顺序：必须在输入组件**和** <see cref="SkillCastComponent"/> **之后**（Attach 里 Get 两者）。不写 Owner 意图字段，只调 SkillCast.Cast。
/// </summary>
public class ComboComponent : ICharacterComponent
{
    // 手感 / 时机参数全部由**当前 ComboGraph 资产**提供（策划按武器 / 连招各自配，不在代码写死）。
    // 无图的回退态（枪 / 旧近战单招）用兜底默认值——那条路本就没连招手感可言。
    private float BufferWindow      => graph != null ? graph.BufferWindow      : 0.2f;
    private float ComboGraceWindow  => graph != null ? graph.ComboGraceWindow  : 0.25f;
    private float DirInputThreshold => graph != null ? graph.DirInputThreshold : 0.3f;

    private InputComponentBase input;
    private SkillCastComponent skillCast;
    private YOTO.ResMgr resMgr;

    private ComboGraph graph;
    private string loadedPath;       // 已加载图对应的 path，检测武器切换后重载
    private int currentNode = -1;    // -1 = Neutral（未在连招中 / 已掉连）

    private bool hasBuffer;
    private ComboButton bufferedButton;
    private ComboDir bufferedDir;
    private float bufferTimer;
    private float comboGraceTimer;   // 收招后"续接宽限"剩余时间：>0 期间 currentNode 保留、点击仍按当前节点续接
    private bool wasCasting;          // 上帧是否在释放：检测 casting→idle 跃迁以开启续接宽限

    public override void Attach(Character owner)
    {
        base.Attach(owner);
        Ctx?.TryGet(out resMgr);
        input = owner.Get<InputComponentBase>();
        skillCast = owner.Get<SkillCastComponent>();
        if (input != null) input.OnAttack += OnAttack;
        if (input == null || skillCast == null)
            Debug.LogWarning("[ComboComponent] 缺 InputComponentBase / SkillCastComponent —— 连招不工作。需在两者之后 Add。");
    }

    public override void Detach()
    {
        if (input != null) input.OnAttack -= OnAttack;
        input = null;
        skillCast = null;
        resMgr = null;
        graph = null;
        loadedPath = null;
        currentNode = -1;
        hasBuffer = false;
        comboGraceTimer = 0f;
        wasCasting = false;
        base.Detach();
    }

    /// <summary>攻击键事件：记缓冲 + 抓按下瞬间的方向（取消窗匹配方向边时用）。实际起手/接招在 Tick 里按时机消费。</summary>
    private void OnAttack(ComboButton button)
    {
        bufferedButton = button;
        bufferedDir = ResolveDir();
        hasBuffer = true;
        bufferTimer = BufferWindow;
    }

    public override void Tick(float dt)
    {
        if (Owner == null || skillCast == null) return;
        if (Owner.IsDead) { hasBuffer = false; currentNode = -1; return; }

        SyncGraph();   // 武器换了就重载图

        // 缓冲**始终计时过期**：只认"接近取消窗"的点击（BufferWindow 内）。段前期早早按的键会在窗开前过期被忽视——
        // 避免"玩家已停手、陈旧输入还在续连招"。连段靠 BufferWindow（窗前预输入）+ ComboGraceWindow（窗后宽限）两个短窗覆盖取消点，
        // 而不是把整段缓冲住。连点能连上是因为后续点击不断刷新缓冲；一旦停手，缓冲很快过期、连招随之停。
        if (hasBuffer)
        {
            bufferTimer -= dt;
            if (bufferTimer <= 0f) hasBuffer = false;
        }

        if (graph != null) TickGraph(dt);
        else TickLegacy();
    }

    // ── 图模式 ──
    private void TickGraph(float dt)
    {
        bool casting = skillCast.IsCasting;

        // 续接宽限：技能刚由"释放"跃迁到"空闲"时开窗；窗内保留 currentNode，超时才真正掉连归零。
        // 这样点击落在取消窗刚过 / 段末结束边界帧时，仍按"当前节点续接下一招"，不会被误判成从头起手。
        if (wasCasting && !casting && currentNode > 0) comboGraceTimer = ComboGraceWindow;
        if (!casting && currentNode > 0)
        {
            comboGraceTimer -= dt;
            if (comboGraceTimer <= 0f) currentNode = -1;
        }
        wasCasting = casting;

        if (!hasBuffer) return;

        if (casting)
        {
            // 释放途中：仅取消窗内可接（从当前节点找出边）
            if (skillCast.CanChainNow && currentNode >= 0)
            {
                int next = FindLink(currentNode, bufferedButton, bufferedDir);
                if (next >= 0) PlayNode(next);
            }
            // 窗未开 / 无匹配边 → 保留缓冲，等窗开（缓冲在释放途中不过期）
        }
        else
        {
            // 空闲 / 宽限期：优先从当前节点续接（宽限内），否则从 Neutral 起手。
            // 续接用 currentNode>0 判定（宽限未超时它才 >0）；超时已被上面归零 → 直接走起手。
            int target = currentNode > 0 ? FindLink(currentNode, bufferedButton, bufferedDir) : -1;
            if (target < 0) target = FindLink(0, bufferedButton, bufferedDir);
            if (target >= 0) PlayNode(target);
        }
    }

    private void PlayNode(int node)
    {
        var nodes = graph.Nodes;
        if (nodes == null || node < 0 || node >= nodes.Length) return;
        var def = nodes[node].Skill;
        if (def == null) return;
#if UNITY_EDITOR
        Debug.Log($"[Combo] node[{node}] {nodes[node].Name}  (prev={currentNode}, {(skillCast.IsCasting ? "chain/cancel-window" : "start/grace")})");
#endif
        skillCast.Cast(def);
        currentNode = node;
        hasBuffer = false;   // 消费缓冲
    }

    /// <summary>在 Nodes[from].Links 里找第一条匹配 (button + dir) 的边，返回 TargetNode；无匹配返回 -1。
    /// 方向匹配：边为 Any 总匹配；Forward/Back 要求当前输入方向一致（更特化的方向边应排在 Any 边前面）。</summary>
    private int FindLink(int from, ComboButton button, ComboDir dir)
    {
        var nodes = graph.Nodes;
        if (nodes == null || from < 0 || from >= nodes.Length) return -1;
        var links = nodes[from].Links;
        if (links == null) return -1;
        for (int i = 0; i < links.Length; i++)
        {
            var lk = links[i];
            if (lk.Button != button) continue;
            if (lk.Direction != ComboDir.Any && lk.Direction != dir) continue;
            return lk.TargetNode;
        }
        return -1;
    }

    // ── 单招回退（无图：枪 / 旧近战）——保留接连招前的行为 ──
    private void TickLegacy()
    {
        if (!hasBuffer) return;
        if (skillCast.IsCasting) return;       // 单招不连：正在放就不再起（旧"不可打断"语义）
        if (bufferedButton == ComboButton.Light)
        {
            if (Owner.WeaponPrimarySkill >= 0) skillCast.Cast(Owner.WeaponPrimarySkill);
            // 枪：左键不是技能（开火走 FireHeld），丢弃缓冲
        }
        else if (bufferedButton == ComboButton.Heavy)
        {
            skillCast.Cast(Owner.WeaponSecondarySkill >= 0 ? Owner.WeaponSecondarySkill : 0);
        }
        hasBuffer = false;
    }

    // ── 图加载（跟随武器切换）──
    private void SyncGraph()
    {
        string path = Owner.CurrentComboGraphPath;
        if (path == loadedPath) return;        // 没变
        loadedPath = path;
        currentNode = -1;
        graph = (!string.IsNullOrEmpty(path) && resMgr != null) ? resMgr.Load<ComboGraph>(path) : null;
        if (!string.IsNullOrEmpty(path) && graph == null)
            Debug.LogWarning($"[ComboComponent] ComboGraph 加载失败: {path}（回退单招）。");
    }

    /// <summary>把当前移动意图判成相对角色朝向的方向类别（做 Forward/Back 分支边匹配用）。无明显方向 → Any。</summary>
    private ComboDir ResolveDir()
    {
        if (input == null || Owner == null) return ComboDir.Any;
        var mv = input.MoveWorld; mv.y = 0f;
        if (mv.sqrMagnitude < DirInputThreshold * DirInputThreshold) return ComboDir.Any;
        var fwd = Owner.Rotation * Vector3.forward; fwd.y = 0f;
        var fwdN = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
        float dot = Vector3.Dot(mv.normalized, fwdN);
        if (dot > 0.3f) return ComboDir.Forward;
        if (dot < -0.3f) return ComboDir.Back;
        return ComboDir.Any;
    }
}
