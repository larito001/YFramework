using Animancer;
using UnityEngine;
using YOTO;

/// <summary>
/// 角色的 view：被动从 Character 读数据驱动 CC / Transform / Animancer。
/// Character 不知道 view 存在，view 通过 Bind 拿到 Character 引用，只读意图、回写物理状态。
/// 在 LateUpdate 跑：保证 GameLoop.Update 里所有组件 Tick 写完意图后再消费。
///
/// **动画方案**：用 Animancer 直接 Play AnimationClip，**不再依赖 Unity Animator state machine / BlendTree /
/// AnimatorOverrideController**。Animator 组件仍在（Animancer 内部用它做 Playables graph），但 runtimeAnimatorController 清空。
/// 武器对应的 clip 集走 <see cref="WeaponAnimSet"/> ScriptableObject——切武器时 ResMgr.Load + 重建 Locomotion。
///
/// 武器模型挂载不在这里 —— 由 Weapon Actor + WeaponView 各自处理，WeaponView 通过 ViewManager
/// 反查本 CharacterView 的 socket 子物体来 reparent。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class CharacterView : BaseView
{
    public CharacterController Controller { get; private set; }
    public Animator Anim { get; private set; }
    public AnimancerComponent Animancer { get; private set; }

    /// <summary>瞄准 mixer ParameterX/Y 的 SmoothDamp 时间（秒）。MoveComponent 转弯方向瞬切（用户要求），
    /// mixer Parameter 直接喂 AnimMoveX/Y 会导致大幅切方向时 9 child 权重瞬变 → 视觉硬切无 blend。
    /// damp 让 mixer 输入平滑过渡，相邻 child 间自然 blend。0.1 = 6 帧追平，足够柔和不拖沓。</summary>
    public float AnimMoveDampTime = 0.1f;

    /// <summary>受击闪烁颜色。要求材质 shader 暴露 _FlashColor / _FlashAmount（如 Custom/CharacterHitFlash）。</summary>
    public Color HitFlashColor = Color.white;
    /// <summary>受击闪烁时长（秒）。从 1 线性衰减回 0；过短不易察觉，过长拖尾感强。</summary>
    public float HitFlashDuration = 0.12f;
    /// <summary>飘字相对脚下 Position.y 的偏移（米）。1.8 ≈ 头顶上方一点点，俯视角下不会被身体挡。</summary>
    public float FlyTextHeight = 1.8f;

    /// <summary>死亡溶解总时长（秒）。和 AutoDespawnComponent.Delay 对齐，让"完全消失"刚好赶上 Despawn。
    /// 越大效果越缓；shader 端用 _DissolveAmount 在这段时间内从 0 线性推到 1。</summary>
    public float DissolveDuration = 3f;
    public Color DissolveEdgeColor = new Color(1f, 0.45f, 0.05f, 1f);
    public float DissolveEdgeWidth = 0.08f;
    public float DissolveEdgeEmission = 3f;

    private Character character;
    private HealthComponent subscribedHealth;
    private FlyTextMgr flyTextMgr;
    private ResMgr resMgr;

    // ── Animancer 状态 ──
    /// <summary>当前持有武器的 AnimSet。WeaponAnimDirty trigger 时 ResMgr.Load 加载。null = 无武器 / 没加载到。</summary>
    private WeaponAnimSet currentAnimSet;
    /// <summary>Layer 0 全身 locomotion mixer（4 child：Idle/Walk/Run/Sprint），按 AnimSpeedRatio 平滑 blend。</summary>
    private LinearMixerState locomotionMixer;
    /// <summary>Layer 0 全身 aim locomotion mixer（CartesianMixerState 9 child：idle 中心 + 8 方向 strafe），按 (AnimMoveX, AnimMoveY) 2D blend——替代旧 BlendTree 2D。</summary>
    private CartesianMixerState aimLocomotionMixer;
    /// <summary>当前 Layer 0 在播的 mixer 引用（基类型，可为 1D LinearMixerState 或 2D CartesianMixerState）——防止每帧 Play 重置 mixer 状态。</summary>
    private AnimancerState currentLayer0Mixer;
    /// <summary>当前激活的一次性 state（Shoot/Reload/Equip/Holster/Melee/Die）。未播完时 Combat layer 保持，Locomotion 不打断。</summary>
    private AnimancerState activeOneShotState;
    /// <summary>true = activeOneShotState 在 Layer 0 全身覆盖（Melee / Die）—— UpdateLocomotion 暂停 mixer，避免半路打断全身动画。
    /// 状态播完后 UpdateLocomotion 自动 fade 回 Locomotion mixer。</summary>
    private bool layer0FullBodyActive;

    // Aim mixer ParameterX/Y 平滑用——SmoothDamp 让玩家大幅切方向时 mixer 输入渐变，避免 9 child 权重瞬变硬切
    private float smoothedAnimMoveX;
    private float smoothedAnimMoveY;
    private float smoothMoveXVel;
    private float smoothMoveYVel;
    /// <summary>true = AnimSet 配了 UpperBodyMask，启用 Layer 1 上下身分离；false = 单层模式，Combat 覆盖 Locomotion。</summary>
    private bool useUpperBodyLayer;

    private Renderer[] flashRenderers;
    private MaterialPropertyBlock flashMpb;
    private float flashTimer;
    private float dissolveTimer;
    private bool dissolving;
    private static readonly int HashFlashAmount = Shader.PropertyToID("_FlashAmount");
    private static readonly int HashFlashColor  = Shader.PropertyToID("_FlashColor");
    private static readonly int HashDissolveAmount       = Shader.PropertyToID("_DissolveAmount");
    private static readonly int HashDissolveEdgeColor    = Shader.PropertyToID("_DissolveEdgeColor");
    private static readonly int HashDissolveEdgeWidth    = Shader.PropertyToID("_DissolveEdgeWidth");
    private static readonly int HashDissolveEdgeEmission = Shader.PropertyToID("_DissolveEdgeEmission");

    private void Awake()
    {
        Controller = GetComponent<CharacterController>();
        if (Controller == null)
            Controller = gameObject.AddComponent<CharacterController>();

        // Animancer 接管动画驱动：自动 AddComponent，Animator 在子物体时手动指引用。
        // Animancer 内部用 Animator 做 Playables graph 输出，runtimeAnimatorController 清空让 Animancer 完全接管。
        Animancer = GetComponent<AnimancerComponent>();
        if (Animancer == null) Animancer = gameObject.AddComponent<AnimancerComponent>();

        Anim = Animancer.Animator;
        if (Anim == null)
        {
            // AnimancerComponent SerializeField 默认找同 GameObject 上的 Animator；找不到时尝试子物体
            Anim = GetComponentInChildren<Animator>();
            if (Anim != null) Animancer.Animator = Anim;
        }
        if (Anim == null)
        {
            Debug.LogWarning($"[CharacterView] {name} 找不到 Animator——Animancer 无法工作");
        }
        else
        {
            // **保留 runtimeAnimatorController 作为兜底**：WeaponAnimSet 未加载时 Animator Controller 跑 default Idle；
            // Animancer.Play 调用后 Playables graph 输出会覆盖 Animator Controller 的输出（Animancer 接管）。
            // 美工建好 AnimSet 之后想完全去掉 Controller 依赖，把 prefab 上 Animator Controller 字段清空即可。
#if UNITY_EDITOR
            var hasController = Anim.runtimeAnimatorController != null ? "Yes" : "No";
            Debug.Log($"[CharacterView] {name} Animancer 接管 Animator={Anim.name}，Controller={hasController}，等待 WeaponAnimSet 加载");
#endif
        }

        flashRenderers = GetComponentsInChildren<Renderer>(true);
    }

    public override void Bind(Actor actor, int id)
    {
        character = actor as Character;
        ID = id;
        if (character == null) return;

        character.Position = transform.position;
        character.Rotation = transform.rotation;
        character.IsGrounded = Controller.isGrounded;

        var ctx = GameLoop.Instance != null ? GameLoop.Instance.Ctx : null;
        if (ctx != null)
        {
            ctx.TryGet(out flyTextMgr);
            ctx.TryGet(out resMgr);
        }

        subscribedHealth = character.Get<HealthComponent>();
        if (subscribedHealth != null)
        {
            subscribedHealth.OnDamaged += OnDamaged;
            subscribedHealth.OnDied += OnDeathDissolve;
        }
    }

    private void OnDestroy()
    {
        if (subscribedHealth != null)
        {
            subscribedHealth.OnDamaged -= OnDamaged;
            subscribedHealth.OnDied -= OnDeathDissolve;
            subscribedHealth = null;
        }
        flyTextMgr = null;
        resMgr = null;
        currentAnimSet = null;
        locomotionMixer = null;
        aimLocomotionMixer = null;
        currentLayer0Mixer = null;
        activeOneShotState = null;
        layer0FullBodyActive = false;
        useUpperBodyLayer = false;
        smoothedAnimMoveX = 0f;
        smoothedAnimMoveY = 0f;
        smoothMoveXVel = 0f;
        smoothMoveYVel = 0f;
        character = null;
        ID = -1;
    }

    private void OnDamaged(DamageInfo info)
    {
        flashTimer = HitFlashDuration;
        if (flyTextMgr != null && character != null)
        {
            // 飘字读 FinalAmount（含暴击 / 抗性后），不读 Amount（基础值）。HealthComponent 已经回填 FinalAmount。
            flyTextMgr.AddText($"-{Mathf.RoundToInt(info.FinalAmount)}",
                character.Position + Vector3.up * FlyTextHeight, FlyTextType.Quick);
        }
    }

    private void OnDeathDissolve(int attackerId)
    {
        dissolving = true;
        dissolveTimer = 0f;
    }

    /// <summary>WeaponAnimDirty 触发时调：换 WeaponAnimSet 资源 + 重建 mixer + 配 layer mask。null path = 清当前 set。</summary>
    private void LoadWeaponAnimSet(string path)
    {
        currentAnimSet = null;
        locomotionMixer = null;
        aimLocomotionMixer = null;
        currentLayer0Mixer = null;
        activeOneShotState = null;
        layer0FullBodyActive = false;
        useUpperBodyLayer = false;

        if (string.IsNullOrEmpty(path))
        {
#if UNITY_EDITOR
            Debug.Log($"[CharacterView] {name} WeaponAnimSet 清空（path=null）—— Animancer fallback 到 Animator Controller default state");
#endif
            return;
        }
        if (resMgr == null)
        {
            Debug.LogWarning($"[CharacterView] {name} ResMgr 未拿到 —— 无法 Load WeaponAnimSet");
            return;
        }
        var set = resMgr.Load<WeaponAnimSet>(path);
        if (set == null)
        {
            Debug.LogWarning($"[CharacterView] {name} WeaponAnimSet 加载失败: {path}（资源不存在 / 不在 Resources 下 / 类型不匹配）");
            return;
        }

        currentAnimSet = set;
        BuildLocomotionMixers();
        SetupUpperBodyLayer();
#if UNITY_EDITOR
        Debug.Log($"[CharacterView] {name} WeaponAnimSet 加载: {path}, mask={(set.UpperBodyMask != null ? "Yes" : "No")}, locoMixer={(locomotionMixer != null ? "Built" : "Missing clips")}");
#endif
    }

    /// <summary>从 currentAnimSet 构造两个 LinearMixerState（替代旧"阈值切 clip"）：
    /// locomotionMixer = 4 child (Idle/Walk/Run/Sprint) 按 IdleThreshold/WalkThreshold/RunThreshold/SprintThreshold 平滑 blend；
    /// aimLocomotionMixer = 2 child (AimIdle/AimWalk) 同 thresholds。
    /// clip 缺一个就跳过 mixer 构造——上半身 mixer 至少要 Idle + Walk 两个 clip 才有意义。</summary>
    private void BuildLocomotionMixers()
    {
        if (currentAnimSet == null) return;

        // 主 locomotion mixer
        if (currentAnimSet.Idle != null && currentAnimSet.Walk != null)
        {
            locomotionMixer = new LinearMixerState();
            // null clip 用 Idle 兜底（避免 mixer child null 报错）
            var idle = currentAnimSet.Idle;
            var walk = currentAnimSet.Walk;
            var run = currentAnimSet.Run != null ? currentAnimSet.Run : walk;
            var sprint = currentAnimSet.Sprint != null ? currentAnimSet.Sprint : run;
            locomotionMixer.AddRange(idle, walk, run, sprint);
            locomotionMixer.SetThresholds(
                currentAnimSet.IdleThreshold,
                currentAnimSet.WalkThreshold,
                currentAnimSet.RunThreshold,
                currentAnimSet.SprintThreshold);

            // idle child（index 0）不参与 SynchronizeChildren——idle 没移动节奏不该被拉伸
            var idleChild = locomotionMixer.GetChild(0);
            if (idleChild != null) locomotionMixer.DontSynchronize(idleChild);
        }

        // Aim locomotion mixer：CartesianMixerState 9 child（idle 中心 + 8 方向 strafe），按 (AnimMoveX, AnimMoveY) 2D blend
        // 替代旧 Animator BlendTree 2D。null 方向 clip 用 AimWalk/AimWalkFwd/AimWalkBwd 兜底
        if (currentAnimSet.AimIdle != null)
        {
            var fallback = currentAnimSet.AimWalk != null ? currentAnimSet.AimWalk : currentAnimSet.AimIdle;
            var fwd = currentAnimSet.AimWalkFwd != null ? currentAnimSet.AimWalkFwd : fallback;
            var bwd = currentAnimSet.AimWalkBwd != null ? currentAnimSet.AimWalkBwd : fallback;
            var right = currentAnimSet.AimStrafeRight != null ? currentAnimSet.AimStrafeRight : fallback;
            var left = currentAnimSet.AimStrafeLeft != null ? currentAnimSet.AimStrafeLeft : fallback;
            var fr = currentAnimSet.AimStrafeFR != null ? currentAnimSet.AimStrafeFR : fwd;
            var fl = currentAnimSet.AimStrafeFL != null ? currentAnimSet.AimStrafeFL : fwd;
            var br = currentAnimSet.AimStrafeBR != null ? currentAnimSet.AimStrafeBR : bwd;
            var bl = currentAnimSet.AimStrafeBL != null ? currentAnimSet.AimStrafeBL : bwd;

            aimLocomotionMixer = new CartesianMixerState();
            aimLocomotionMixer.AddRange(currentAnimSet.AimIdle, fwd, fr, right, br, bwd, bl, left, fl);
            const float d = 0.7071f;  // sqrt(2)/2，对角线单位向量分量
            aimLocomotionMixer.SetThresholds(
                new Vector2(0f, 0f),    // AimIdle 中心
                new Vector2(0f, 1f),    // 前
                new Vector2(d, d),      // 前右
                new Vector2(1f, 0f),    // 右
                new Vector2(d, -d),     // 后右
                new Vector2(0f, -1f),   // 后
                new Vector2(-d, -d),    // 后左
                new Vector2(-1f, 0f),   // 左
                new Vector2(-d, d));    // 前左

            // idle child（index 0）不参与 SynchronizeChildren：idle 没移动节奏，强制同步会被拉伸踩拍变形
            // strafe child 之间保持同步——保证 fade 时步幅一致（main fbx walk 速度 vs Diagonals fbx run 速度差距）
            var idleChild = aimLocomotionMixer.GetChild(0);
            if (idleChild != null) aimLocomotionMixer.DontSynchronize(idleChild);
        }
    }

    /// <summary>启用 Layer 1 上半身分离：UpperBodyMask 配了就建 Layer 1 + SetMask；没配 useUpperBodyLayer=false，
    /// Combat 走 Layer 0（跟 Locomotion 同层，会覆盖——单层模式）。</summary>
    private void SetupUpperBodyLayer()
    {
        useUpperBodyLayer = currentAnimSet != null && currentAnimSet.UpperBodyMask != null;
        if (useUpperBodyLayer)
        {
            // Layers 索引器自动扩容，访问 [1] 即建第二层
            var layer = Animancer.Layers[1];
            layer.Mask = currentAnimSet.UpperBodyMask;
            layer.SetWeight(1f);
        }
        else if (Animancer.Layers.Count > 1)
        {
            // 之前有 mask 现在无，淡出 Layer 1
            Animancer.Layers[1].StartFade(0f, currentAnimSet != null ? currentAnimSet.DefaultFade : 0.1f);
        }
    }

    private void WriteShaderState(float flashAmount, float dissolveAmount)
    {
        if (flashRenderers == null || flashRenderers.Length == 0) return;
        if (flashMpb == null) flashMpb = new MaterialPropertyBlock();
        flashMpb.SetFloat(HashFlashAmount, flashAmount);
        flashMpb.SetColor(HashFlashColor, HitFlashColor);
        flashMpb.SetFloat(HashDissolveAmount, dissolveAmount);
        flashMpb.SetColor(HashDissolveEdgeColor, DissolveEdgeColor);
        flashMpb.SetFloat(HashDissolveEdgeWidth, DissolveEdgeWidth);
        flashMpb.SetFloat(HashDissolveEdgeEmission, DissolveEdgeEmission);
        for (int i = 0; i < flashRenderers.Length; i++)
        {
            if (flashRenderers[i] != null) flashRenderers[i].SetPropertyBlock(flashMpb);
        }
    }

    private void LateUpdate()
    {
        if (character == null) return;

        // 局部时间缩放：CC.Move 的 dt 和 Animancer.Playable.Speed 都按 character.TimeScale 缩。
        // 卡肉时整个角色（物理 + 动画）一起冻结。全局慢动作走 Unity Time.timeScale 自动包含在 Time.deltaTime 里。
        float scale = character.TimeScale;
        float scaledDt = scale == 1f ? Time.deltaTime : Time.deltaTime * scale;

        // Controller 死亡时被禁用以"删除碰撞"。disabled 状态下 Move/isGrounded 调用是 no-op 但有 Unity 警告，统一跳过。
        if (Controller != null && Controller.enabled)
        {
            Controller.Move(character.WishVelocity * scaledDt);
            character.IsGrounded = Controller.isGrounded;
        }
        transform.rotation = character.Rotation;
        character.Position = transform.position;

        if (Animancer != null)
        {
            // 切 AnimSet（如果 WeaponComponent 通知）。在 drive 之前做，保证 trigger 用新 AnimSet 的 clip
            if (character.WeaponAnimDirty)
            {
                character.WeaponAnimDirty = false;
                LoadWeaponAnimSet(character.CurrentWeaponAnimSetPath);
            }

            // 全局动画速度：character.TimeScale × （特定 state 如 Shoot 还会乘 RecoilAnimSpeed 在 Play 时设）
            // AnimancerComponent.Graph.Speed 是图根 speed，所有 state 共乘
            Animancer.Graph.Speed = scale;

            DriveAnimation();
        }

        // Flash 闪烁 + Death 溶解，合到同一份 MPB 一起写出。用 scaledDt 让卡肉期间这两个特效也一起冻住。
        bool flashActive = flashTimer > 0f;
        bool needWrite = flashActive || dissolving;

        float flashK = 0f;
        if (flashActive)
        {
            flashTimer -= scaledDt;
            flashK = HitFlashDuration > 0f ? Mathf.Clamp01(flashTimer / HitFlashDuration) : 0f;
            if (flashTimer <= 0f) { flashTimer = 0f; flashK = 0f; }
        }

        float dissolveK = 0f;
        if (dissolving)
        {
            dissolveTimer += scaledDt;
            dissolveK = DissolveDuration > 0f ? Mathf.Clamp01(dissolveTimer / DissolveDuration) : 1f;
        }

        if (needWrite) WriteShaderState(flashK, dissolveK);
    }

    /// <summary>核心动画状态机：按 Character 触发字段优先级 Play 对应 clip。Animancer 替代 Animator state machine。
    ///
    /// **优先级**（从高到低）：
    ///   Die（最高，触发后不再切其他）→ Melee → Holster → Equip → Reload → Shoot → Locomotion
    ///
    /// **互斥语义**：一次性 trigger（Die/Melee/Holster/Equip/Reload/Shoot）触发后 view 记 activeOneShotState，
    /// 直到 state.NormalizedTime &gt;= 1 或新 trigger 来才让 Locomotion 接管——避免 Reload 被 Locomotion 半路打断。
    ///
    /// **Shoot 特殊**：高频射击不走 activeOneShotState 锁定（每帧 trigger 重置 state，靠 RecoilAnimSpeed 让 clip 在
    /// FireInterval 内播完，自然衔接）。Shoot 不阻塞 Locomotion——边跑边射的 view 表现靠 Animancer Layer 上半身 mask
    /// 实现（当前 MVP 不分层，Shoot 会暂时覆盖 Locomotion）。</summary>
    private void DriveAnimation()
    {
        if (currentAnimSet == null) return;

        // Combat target layer：mask 配了走 Layer 1（上半身）；没配走 Layer 0（覆盖 Locomotion，单层模式）
        var combatLayer = useUpperBodyLayer ? Animancer.Layers[1] : Animancer.Layers[0];

        // 1. 死亡：全身覆盖。强化：Layer 0 weight=1 + Layer 1 weight=0 + fade=0 立即切 Death pose
        if (character.Die)
        {
            character.Die = false;
            var clip = character.DeathVariant == 0 ? currentAnimSet.DeathL : currentAnimSet.DeathR;
#if UNITY_EDITOR
            Debug.Log($"[CharacterView] {name} Die triggered: variant={character.DeathVariant}, clip={(clip != null ? clip.name : "NULL")}, layer0Weight={Animancer.Layers[0].Weight}");
#endif
            if (clip != null)
            {
                Animancer.Layers[0].SetWeight(1f);  // 强制 weight=1 防止 layer 之前被 fade 到 0
                activeOneShotState = Animancer.Layers[0].Play(clip, 0f);  // fade=0 立即切，不给其他东西机会
#if UNITY_EDITOR
                Debug.Log($"[CharacterView] {name} After Play: stateClip={activeOneShotState?.Clip?.name}, stateIsPlaying={activeOneShotState?.IsPlaying}, layer0Weight={Animancer.Layers[0].Weight}");
#endif
            }
            if (useUpperBodyLayer && Animancer.Layers.Count > 1) Animancer.Layers[1].SetWeight(0f);
            currentLayer0Mixer = null;
            layer0FullBodyActive = true;
            if (Controller != null) Controller.enabled = false;
            return;
        }
        if (character.IsDead) return;

        // 2. 近战（**全身覆盖**：Melee 包含全身动作如转身/前冲，走 Layer 0 而非 Combat layer）
        if (character.MeleeAttack)
        {
            character.MeleeAttack = false;
            var clip = character.MeleeType == 0 ? currentAnimSet.MeleeHard : currentAnimSet.MeleeKick;
            if (clip != null)
            {
                activeOneShotState = Animancer.Layers[0].Play(clip, currentAnimSet.DefaultFade);
                layer0FullBodyActive = true;
                currentLayer0Mixer = null;
            }
            if (useUpperBodyLayer && Animancer.Layers.Count > 1) Animancer.Layers[1].StartFade(0f, currentAnimSet.DefaultFade);
            return;
        }
        // 3. 切枪 Holster
        else if (character.WeaponHolster)
        {
            character.WeaponHolster = false;
            if (currentAnimSet.Holster != null) activeOneShotState = combatLayer.Play(currentAnimSet.Holster, currentAnimSet.DefaultFade);
        }
        // 4. 切枪 Equip
        else if (character.WeaponSwap)
        {
            character.WeaponSwap = false;
            if (currentAnimSet.Equip != null) activeOneShotState = combatLayer.Play(currentAnimSet.Equip, currentAnimSet.DefaultFade);
        }
        // 5. 换弹
        else if (character.Reload)
        {
            character.Reload = false;
            if (currentAnimSet.Reload != null) activeOneShotState = combatLayer.Play(currentAnimSet.Reload, currentAnimSet.DefaultFade);
        }
        // 6. 开火（高频，每发覆盖；RecoilAnimSpeed 让 clip 在 FireInterval 内播完）
        else if (character.Shoot)
        {
            character.Shoot = false;
            var clip = character.HeavyRecoil ? currentAnimSet.ShootHeavy : currentAnimSet.ShootLight;
            if (clip != null)
            {
                var s = combatLayer.Play(clip, currentAnimSet.ShootFade);
                if (s != null && character.RecoilAnimSpeed > 0f) s.Speed = character.RecoilAnimSpeed;
                activeOneShotState = s;
            }
        }
        // 7a. Layer 0 全身覆盖中（Melee 触发）：等待 melee 播完才回 Locomotion mixer，避免半路被打断
        if (layer0FullBodyActive)
        {
            if (activeOneShotState != null && activeOneShotState.IsPlaying && activeOneShotState.NormalizedTime < 1f)
                return;  // Melee 还在播，下半身锁定全身动画，不切 mixer
            // Melee 播完：解锁 + Layer 1 weight 恢复 1（Combat 重新可用） + UpdateLocomotion 重新 Play mixer
            layer0FullBodyActive = false;
            if (useUpperBodyLayer && Animancer.Layers.Count > 1) Animancer.Layers[1].SetWeight(1f);
            activeOneShotState = null;
        }
        // 7b. 单层模式 + Combat one-shot 未播完：等待
        else if (!useUpperBodyLayer && activeOneShotState != null && activeOneShotState.IsPlaying && activeOneShotState.NormalizedTime < 1f)
        {
            return;
        }
        // 7c. 双层模式 + Combat one-shot 播完：淡出 Layer 1 让 Locomotion 透出来
        else if (activeOneShotState != null && (activeOneShotState.NormalizedTime >= 1f || !activeOneShotState.IsPlaying))
        {
            if (useUpperBodyLayer && Animancer.Layers.Count > 1) Animancer.Layers[1].StartFade(0f, currentAnimSet.DefaultFade);
            activeOneShotState = null;
        }

        // 8. Locomotion 走 Layer 0 mixer（always-on：分层模式跟 Combat 并行，单层模式被 Combat 覆盖）
        UpdateLocomotion();
    }

    /// <summary>Locomotion 驱动：
    ///   - 非瞄准 → LinearMixerState 1D（Idle/Walk/Run/Sprint 按 AnimSpeedRatio 真实 m/s blend）
    ///   - 瞄准 → CartesianMixerState 2D（9 child idle+8 方向，按 (AnimMoveX, AnimMoveY) blend）替代旧 BlendTree 2D
    /// mixer 实例切换走 DefaultFade 淡入。</summary>
    private void UpdateLocomotion()
    {
        // 选当前 mixer 实例 + 设 Parameter（1D float vs 2D Vector2 分两套）
        if (character.IsAiming && aimLocomotionMixer != null)
        {
            if (currentLayer0Mixer != aimLocomotionMixer)
            {
                Animancer.Layers[0].Play(aimLocomotionMixer, currentAnimSet.DefaultFade);
                currentLayer0Mixer = aimLocomotionMixer;
            }
            // 2D：本地坐标 (X=横向, Y=朝前后)，AnimMoveX/Y 已在 MoveComponent 按 AimSpeed 归一化到 [-1,1]
            // SmoothDamp 平滑 mixer 输入——MoveComponent 转向瞬切，没 damp 会让 9 child 权重瞬变硬切（视觉无 blend）
            smoothedAnimMoveX = Mathf.SmoothDamp(smoothedAnimMoveX, character.AnimMoveX, ref smoothMoveXVel, AnimMoveDampTime, Mathf.Infinity, Time.deltaTime);
            smoothedAnimMoveY = Mathf.SmoothDamp(smoothedAnimMoveY, character.AnimMoveY, ref smoothMoveYVel, AnimMoveDampTime, Mathf.Infinity, Time.deltaTime);
            aimLocomotionMixer.ParameterX = smoothedAnimMoveX;
            aimLocomotionMixer.ParameterY = smoothedAnimMoveY;
        }
        else if (locomotionMixer != null)
        {
            if (currentLayer0Mixer != locomotionMixer)
            {
                Animancer.Layers[0].Play(locomotionMixer, currentAnimSet.DefaultFade);
                currentLayer0Mixer = locomotionMixer;
            }
            // 1D：AnimSpeedRatio 是真实水平速度 m/s
            locomotionMixer.Parameter = character.AnimSpeedRatio;
        }
    }
}
