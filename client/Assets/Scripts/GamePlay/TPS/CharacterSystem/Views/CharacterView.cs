using UnityEngine;
using YOTO;

/// <summary>
/// 角色的 view：被动从 Character 读数据驱动 CC / Transform / Animator。
/// Character 不知道 view 存在，view 通过 Bind 拿到 Character 引用，只读意图、回写物理状态。
/// 在 LateUpdate 跑：保证 GameLoop.Update 里所有组件 Tick 写完意图后再消费。
///
/// 武器模型挂载不在这里 —— 由 Weapon Actor + WeaponView 各自处理，WeaponView 通过 ViewManager
/// 反查本 CharacterView 的 socket 子物体来 reparent。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class CharacterView : BaseView
{
    public CharacterController Controller { get; private set; }
    public Animator Anim { get; private set; }

    /// <summary>BlendTree 方向参数 (MoveX/MoveY) 的平滑时间（秒）。Animator.SetFloat damp 版本用。
    /// MoveComponent 里方向是瞬切的（用户要求转弯无 lerp），但 BlendTree 视觉上需要软化，否则连续切方向时姿势会瞬移。
    /// 0.1 = 大约 6 帧内追上目标值，体感顺滑且不拖沓。</summary>
    public float AnimMoveDampTime = 0.1f;

    /// <summary>受击闪烁颜色。要求材质 shader 暴露 _FlashColor / _FlashAmount（如 Custom/CharacterHitFlash）。</summary>
    public Color HitFlashColor = Color.white;
    /// <summary>受击闪烁时长（秒）。从 1 线性衰减回 0；过短不易察觉，过长拖尾感强。</summary>
    public float HitFlashDuration = 0.12f;

    /// <summary>死亡溶解总时长（秒）。和 HealthComponent.AutoRemoveDelay 对齐，让"完全消失"刚好赶上 Despawn。
    /// 越大效果越缓；shader 端用 _DissolveAmount 在这段时间内从 0 线性推到 1。</summary>
    public float DissolveDuration = 3f;
    /// <summary>溶解边缘高亮颜色（着火/魔法粉等）。和 shader 的 _DissolveEdgeColor 对应。</summary>
    public Color DissolveEdgeColor = new Color(1f, 0.45f, 0.05f, 1f);
    /// <summary>溶解边缘宽度（noise 单位）。和 shader 的 _DissolveEdgeWidth 对应，越大边带越粗。</summary>
    public float DissolveEdgeWidth = 0.08f;
    /// <summary>溶解边缘亮度（自发光强度）。&gt;1 让边缘超亮，配合后期 bloom 出火光感。</summary>
    public float DissolveEdgeEmission = 3f;

    private Character character;
    private HealthComponent subscribedHealth;
    private Renderer[] flashRenderers;
    private MaterialPropertyBlock flashMpb;
    private float flashTimer;
    private float dissolveTimer;   // 0 表示未死/未启动；>0 时每帧+=dt 推进到 DissolveDuration
    private bool dissolving;
    private static readonly int HashFlashAmount = Shader.PropertyToID("_FlashAmount");
    private static readonly int HashFlashColor  = Shader.PropertyToID("_FlashColor");
    private static readonly int HashDissolveAmount       = Shader.PropertyToID("_DissolveAmount");
    private static readonly int HashDissolveEdgeColor    = Shader.PropertyToID("_DissolveEdgeColor");
    private static readonly int HashDissolveEdgeWidth    = Shader.PropertyToID("_DissolveEdgeWidth");
    private static readonly int HashDissolveEdgeEmission = Shader.PropertyToID("_DissolveEdgeEmission");

    private static readonly int HashMoveX = Animator.StringToHash("MoveX");
    private static readonly int HashMoveY = Animator.StringToHash("MoveY");
    private static readonly int HashSpeed = Animator.StringToHash("Speed");
    private static readonly int HashIsShooting = Animator.StringToHash("IsShooting");
    private static readonly int HashIsAiming = Animator.StringToHash("IsAiming");
    private static readonly int HashMeleeAttack = Animator.StringToHash("MeleeAttack");
    private static readonly int HashMeleeType = Animator.StringToHash("MeleeType");
    private static readonly int HashWeaponSwap = Animator.StringToHash("WeaponSwap");
    private static readonly int HashWeaponHolster = Animator.StringToHash("WeaponHolster");
    private static readonly int HashReload = Animator.StringToHash("Reload");
    private static readonly int HashIsReloading = Animator.StringToHash("IsReloading");
    private static readonly int HashShoot = Animator.StringToHash("Shoot");
    private static readonly int HashHeavyRecoil = Animator.StringToHash("HeavyRecoil");
    private static readonly int HashRecoilSpeed = Animator.StringToHash("RecoilSpeed");
    private static readonly int HashDie = Animator.StringToHash("Die");
    private static readonly int HashDeathVariant = Animator.StringToHash("DeathVariant");

    private void Awake()
    {
        Controller = GetComponent<CharacterController>();
        if (Controller == null)
            Controller = gameObject.AddComponent<CharacterController>();
        Anim = GetComponentInChildren<Animator>();
        if (Anim == null)
            Debug.LogWarning($"[CharacterView] {name} 找不到 Animator");

        // 受击闪烁要驱动的所有 renderer：包含 SkinnedMeshRenderer + MeshRenderer，
        // include inactive=true 兜底 prefab 里临时 disable 的部件（如不同武器槽 socket）。
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

        // 订阅受击事件：HealthComponent 在 ApplyDamage 里触发 OnDamaged，view 拿来刷一发 _FlashAmount=1
        // 死亡事件：OnDied 触发后启动溶解定时器，shader 的 _DissolveAmount 在 DissolveDuration 内 0→1
        subscribedHealth = character.Get<HealthComponent>();
        if (subscribedHealth != null)
        {
            subscribedHealth.OnDamaged += OnDamagedFlash;
            subscribedHealth.OnDied += OnDeathDissolve;
        }
    }

    /// <summary>view 被 ViewManager.RemoveBaseView 销毁时清 Actor 引用，
    /// 与 BulletView.OnDespawn 对齐：避免 Unity Destroy 排队期间 LateUpdate 还跑一帧
    /// 命中已 Dispose 的 character 引用做出脏写。</summary>
    private void OnDestroy()
    {
        if (subscribedHealth != null)
        {
            subscribedHealth.OnDamaged -= OnDamagedFlash;
            subscribedHealth.OnDied -= OnDeathDissolve;
            subscribedHealth = null;
        }
        character = null;
        ID = -1;
    }

    private void OnDamagedFlash(float amount, int attackerId)
    {
        flashTimer = HitFlashDuration;
    }

    private void OnDeathDissolve(int attackerId)
    {
        dissolving = true;
        dissolveTimer = 0f;
    }

    /// <summary>把当前的 flash + dissolve 参数一起写到所有 renderer 的 MaterialPropertyBlock 上。
    /// 用 MPB 而不是 material[] 是为了不打破 SRP Batcher / 不产生 material 实例 leak。
    /// 材质 shader 必须暴露对应 _FlashAmount / _DissolveAmount 等 property，否则这一步是 no-op，不会报错。</summary>
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

        // 局部时间缩放：CC.Move 的 dt 和 Animator.speed 都按 character.TimeScale 缩，
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

        if (Anim != null)
        {
            // 动画播放倍率：在 Locomotion tag 的状态用 Character.AnimPlaybackRate（MoveComponent 按 walk/sprint/aim 写入），
            // 其他状态（Melee/Equipping/Idle 等）保持 1x 避免误缩。最后再乘 TimeScale 实现卡肉冻结动画。
            var stateInfo = Anim.GetCurrentAnimatorStateInfo(0);
            float animBase;
            if (stateInfo.IsTag("Locomotion"))
            {
                var horiz = new Vector2(character.WishVelocity.x, character.WishVelocity.z).magnitude;
                // 几乎静止时回 1，避免 Idle 姿势被 walk/sprint 倍率扭曲
                animBase = horiz > 0.05f ? character.AnimPlaybackRate : 1f;
            }
            else
            {
                animBase = 1f;
            }
            Anim.speed = animBase * scale;

            // MoveX/MoveY 用 damp 版本平滑：连续切 WASD 方向时 BlendTree 姿势不瞬移
            // Speed 已被 MoveComponent 的 Acceleration 平滑，view 端不再二次 damp
            Anim.SetFloat(HashMoveX, character.AnimMoveX, AnimMoveDampTime, Time.deltaTime);
            Anim.SetFloat(HashMoveY, character.AnimMoveY, AnimMoveDampTime, Time.deltaTime);
            Anim.SetFloat(HashSpeed, character.AnimSpeedRatio);
            Anim.SetBool(HashIsShooting, character.IsShooting);
            Anim.SetBool(HashIsAiming, character.IsAiming);
            if (character.MeleeAttack)
            {
                Anim.SetInteger(HashMeleeType, character.MeleeType);
                Anim.SetTrigger(HashMeleeAttack);
                character.MeleeAttack = false;
            }
            if (character.WeaponHolster)
            {
                Anim.SetTrigger(HashWeaponHolster);
                character.WeaponHolster = false;
            }
            if (character.WeaponSwap)
            {
                Anim.SetTrigger(HashWeaponSwap);
                character.WeaponSwap = false;
            }
            Anim.SetBool(HashIsReloading, character.IsReloading);
            if (character.Reload)
            {
                Anim.SetTrigger(HashReload);
                character.Reload = false;
            }
            // Recoil 层：HeavyRecoil 选 ShootLight / ShootHeavy；RecoilSpeed 让单次动画在 FireInterval 内播完
            Anim.SetBool(HashHeavyRecoil, character.HeavyRecoil);
            Anim.SetFloat(HashRecoilSpeed, character.RecoilAnimSpeed);
            if (character.Shoot)
            {
                Anim.SetTrigger(HashShoot);
                character.Shoot = false;
            }
            // 死亡：DeathVariant 在 SetTrigger 前写好，AnyState 转移用变体条件分流到 DeathL / DeathR
            if (character.Die)
            {
                Anim.SetInteger(HashDeathVariant, character.DeathVariant);
                Anim.SetTrigger(HashDie);
                character.Die = false;
                // 倒地后删除碰撞：CC disabled 让子弹/角色都穿过尸体。3s 后 HealthComponent 走 deferred remove 整个清掉。
                if (Controller != null) Controller.enabled = false;
            }
        }

        // Flash 闪烁 + Death 溶解，合到同一份 MPB 一起写出（两个特效用同一个材质 shader）。
        // 用 scaledDt（已含 TimeScale），让卡肉期间这两个特效也一起冻住，整体感才连贯。
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
