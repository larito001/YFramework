using Animancer;
using UnityEngine;
using YOTO;

/// <summary>
/// 角色的 view：被动从 Character 读数据驱动 CC / Transform / 视觉反馈。
/// **动画驱动委托给 <see cref="CharacterAnimancerController"/>**——view 只管 CC 物理回写、受击闪烁、死亡溶解、飘字、订事件。
///
/// 数据流：组件 Tick 写"意图"字段 → view.LateUpdate 读意图驱动 CC + animController.Tick → animController 驱动 Animancer。
/// view 通过 animController.OnDeathTriggered 事件做 GameObject 级响应（disable CC）。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class CharacterView : BaseView
{
    public CharacterController Controller { get; private set; }
    public Animator Anim { get; private set; }
    public AnimancerComponent Animancer { get; private set; }

    /// <summary>瞄准 mixer ParameterX/Y 的 SmoothDamp 时间（秒）。Inspector 可调，Bind 时传给 controller。
    /// 0.1 = 6 帧追平柔和不拖沓；调小过渡硬，调大过渡软。</summary>
    public float AnimMoveDampTime = 0.1f;

    /// <summary>受击闪烁颜色。要求材质 shader 暴露 _FlashColor / _FlashAmount（如 Custom/CharacterHitFlash）。</summary>
    public Color HitFlashColor = Color.white;
    /// <summary>受击闪烁时长（秒）。从 1 线性衰减回 0。</summary>
    public float HitFlashDuration = 0.12f;
    /// <summary>飘字相对脚下 Position.y 的偏移（米）。1.8 ≈ 头顶一点点。</summary>
    public float FlyTextHeight = 1.8f;

    /// <summary>死亡溶解总时长（秒），跟 AutoDespawnComponent.Delay 对齐。</summary>
    public float DissolveDuration = 3f;
    public Color DissolveEdgeColor = new Color(1f, 0.45f, 0.05f, 1f);
    public float DissolveEdgeWidth = 0.08f;
    public float DissolveEdgeEmission = 3f;

    private Character character;
    private HealthComponent subscribedHealth;
    private FlyTextMgr flyTextMgr;
    private ResMgr resMgr;
    private TimeScaleService timeScaleService;
    private AnimConductor animController;

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
        if (Controller == null) Controller = gameObject.AddComponent<CharacterController>();

        // Animancer 接管动画驱动：自动 AddComponent，Animator 在子物体时手动指引用
        Animancer = GetComponent<AnimancerComponent>();
        if (Animancer == null) Animancer = gameObject.AddComponent<AnimancerComponent>();
        Anim = Animancer.Animator;
        if (Anim == null)
        {
            Anim = GetComponentInChildren<Animator>();
            if (Anim != null) Animancer.Animator = Anim;
        }
        if (Anim == null)
            Debug.LogWarning($"[CharacterView] {name} 找不到 Animator——Animancer 无法工作");

        // 保留 runtimeAnimatorController 作为兜底：WeaponAnimSet 未加载时 default Idle pose 不至于 T-pose；Animancer.Play 后 Playables graph 覆盖
        animController = CreateController();

        flashRenderers = GetComponentsInChildren<Renderer>(true);
    }

    /// <summary>创建本 view 用的动画驱动器。玩家用 <see cref="CharacterAnimancerController"/>（武器+瞄准）；
    /// 僵尸 view（<see cref="ZombieView"/>）override 返回 <see cref="ZombieAnimancerController"/>（技能链）。
    /// 在 Awake 调用，此时序列化字段已就绪。</summary>
    protected virtual AnimConductor CreateController()
        => new CharacterAnimancerController { AnimMoveDampTime = AnimMoveDampTime };

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
            ctx.TryGet(out timeScaleService);
        }

        // 接入 animController：传 CharacterAnimSet path（一次性加载，运行时不换）
        // AnimMoveDampTime 已在 CreateController() 注入（仅玩家 controller 用）
        animController.Init(Animancer, resMgr, character.CurrentCharacterAnimSetPath);
        animController.OnDeathTriggered += OnAnimDeathTriggered;

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
        if (animController != null)
        {
            animController.OnDeathTriggered -= OnAnimDeathTriggered;
            animController.Dispose();
            animController = null;
        }
        flyTextMgr = null;
        resMgr = null;
        timeScaleService = null;
        character = null;
        ID = -1;
    }

    private void OnDamaged(DamageInfo info)
    {
        flashTimer = HitFlashDuration;
        if (flyTextMgr != null && character != null)
        {
            // 飘字读 FinalAmount（含暴击 / 抗性后）
            flyTextMgr.AddText($"-{Mathf.RoundToInt(info.FinalAmount)}",
                character.Position + Vector3.up * FlyTextHeight, FlyTextType.Quick);
        }
    }

    private void OnDeathDissolve(int attackerId)
    {
        dissolving = true;
        dissolveTimer = 0f;
    }

    /// <summary>animController 消费 Die trigger 后回调——view 这里做 GameObject 级响应（disable CC 让子弹穿过尸体）。</summary>
    private void OnAnimDeathTriggered()
    {
        if (Controller != null) Controller.enabled = false;
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

        // 有效缩放 = 全局缩放 × 本 actor 局部缩放。全局缩放不再走 Time.timeScale（恒为 1），
        // 所以 view 用 unscaledDeltaTime × 有效缩放 自己折算（CC 物理、闪烁、溶解、动画速度全用它）。
        float globalScale = timeScaleService != null ? timeScaleService.GlobalScale : 1f;
        // 有效缩放 = 全局 × 有效局部缩放(Actor.LocalScale = TimeScale 卡肉 × ZoneScale 区域减速)。
        // 主动 view 自己跑 unscaledDeltaTime 物理/动画，必须乘 LocalScale，否则角色在减速圈/卡肉时不会变慢。
        float scale = globalScale * character.LocalScale;
        float scaledDt = Time.unscaledDeltaTime * scale;

        // 1. CC 物理 + transform 同步
        if (Controller != null && Controller.enabled)
        {
            Controller.Move(character.WishVelocity * scaledDt);
            character.IsGrounded = Controller.isGrounded;
        }
        transform.rotation = character.Rotation;
        character.Position = transform.position;

        // 2. 动画驱动委托给 controller
        animController?.Tick(character, scale);

        // 3. Flash 闪烁 + Death 溶解 视觉反馈（用 scaledDt 让卡肉期间也冻住）
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
}
