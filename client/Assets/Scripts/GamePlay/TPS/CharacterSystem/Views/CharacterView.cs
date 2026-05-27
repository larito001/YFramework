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

    private Character character;
    private HealthComponent subscribedHealth;
    private Renderer[] flashRenderers;
    private MaterialPropertyBlock flashMpb;
    private float flashTimer;
    private static readonly int HashFlashAmount = Shader.PropertyToID("_FlashAmount");
    private static readonly int HashFlashColor  = Shader.PropertyToID("_FlashColor");

    private static readonly int HashMoveX = Animator.StringToHash("MoveX");
    private static readonly int HashMoveY = Animator.StringToHash("MoveY");
    private static readonly int HashSpeed = Animator.StringToHash("Speed");
    private static readonly int HashIsShooting = Animator.StringToHash("IsShooting");
    private static readonly int HashIsAiming = Animator.StringToHash("IsAiming");
    private static readonly int HashMeleeAttack = Animator.StringToHash("MeleeAttack");
    private static readonly int HashMeleeType = Animator.StringToHash("MeleeType");
    private static readonly int HashWeaponSwap = Animator.StringToHash("WeaponSwap");
    private static readonly int HashReload = Animator.StringToHash("Reload");
    private static readonly int HashIsReloading = Animator.StringToHash("IsReloading");
    private static readonly int HashShoot = Animator.StringToHash("Shoot");
    private static readonly int HashHeavyRecoil = Animator.StringToHash("HeavyRecoil");
    private static readonly int HashRecoilSpeed = Animator.StringToHash("RecoilSpeed");

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
        subscribedHealth = character.Get<HealthComponent>();
        if (subscribedHealth != null) subscribedHealth.OnDamaged += OnDamagedFlash;
    }

    /// <summary>view 被 ViewManager.RemoveBaseView 销毁时清 Actor 引用，
    /// 与 BulletView.OnDespawn 对齐：避免 Unity Destroy 排队期间 LateUpdate 还跑一帧
    /// 命中已 Dispose 的 character 引用做出脏写。</summary>
    private void OnDestroy()
    {
        if (subscribedHealth != null)
        {
            subscribedHealth.OnDamaged -= OnDamagedFlash;
            subscribedHealth = null;
        }
        character = null;
        ID = -1;
    }

    private void OnDamagedFlash(float amount, int attackerId)
    {
        flashTimer = HitFlashDuration;
    }

    /// <summary>把当前 _FlashAmount 写到所有 renderer 的 MaterialPropertyBlock。
    /// 用 MPB 而不是 material[] 是为了不打破 SRP Batcher / 不产生 material 实例 leak。
    /// 材质 shader 必须暴露 _FlashAmount 和 _FlashColor，否则这步是 no-op，不会报错。</summary>
    private void WriteFlash(float amount)
    {
        if (flashRenderers == null || flashRenderers.Length == 0) return;
        if (flashMpb == null) flashMpb = new MaterialPropertyBlock();
        flashMpb.SetFloat(HashFlashAmount, amount);
        flashMpb.SetColor(HashFlashColor, HitFlashColor);
        for (int i = 0; i < flashRenderers.Length; i++)
        {
            if (flashRenderers[i] != null) flashRenderers[i].SetPropertyBlock(flashMpb);
        }
    }

    private void LateUpdate()
    {
        if (character == null) return;

        Controller.Move(character.WishVelocity * Time.deltaTime);
        transform.rotation = character.Rotation;
        character.Position = transform.position;
        character.IsGrounded = Controller.isGrounded;

        if (Anim != null)
        {
            // 动画播放倍率：在 Locomotion tag 的状态用 Character.AnimPlaybackRate（MoveComponent 按 walk/sprint/aim 写入），
            // 其他状态（Melee/Equipping/Idle 等）保持 1x 避免误缩
            var stateInfo = Anim.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsTag("Locomotion"))
            {
                var horiz = new Vector2(character.WishVelocity.x, character.WishVelocity.z).magnitude;
                // 几乎静止时回 1，避免 Idle 姿势被 walk/sprint 倍率扭曲
                Anim.speed = horiz > 0.05f ? character.AnimPlaybackRate : 1f;
            }
            else
            {
                Anim.speed = 1f;
            }

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
        }

        // 受击闪烁：从 HitFlashDuration 线性衰减到 0，过 0 后再写一帧 0 把 MPB 关掉
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            float k = HitFlashDuration > 0f ? Mathf.Clamp01(flashTimer / HitFlashDuration) : 0f;
            if (flashTimer <= 0f) { flashTimer = 0f; k = 0f; }
            WriteFlash(k);
        }
    }
}
