using UnityEngine;
using YOTO;

/// <summary>
/// 角色的 view：被动从 Character 读数据驱动 CC / Transform / Animator / 武器挂点。
/// Character 不知道 view 存在，view 通过 Bind 拿到 Character 引用，只读意图、回写物理状态。
/// 在 LateUpdate 跑：保证 GameLoop.Update 里所有组件 Tick 写完意图后再消费。
/// 所有武器共用 prefab 上挂的默认 Animator Controller；切枪靠状态机 WeaponSwap trigger + 换模型。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class CharacterView : BaseView
{
    public CharacterController Controller { get; private set; }
    public Animator Anim { get; private set; }

    /// <summary>武器挂点骨骼名（RifleAnimsetPro 的 Dummy 用的是 RightHandProp）。</summary>
    public string WeaponSocketName = "RightHandProp";

    /// <summary>BlendTree 方向参数 (MoveX/MoveY) 的平滑时间（秒）。Animator.SetFloat damp 版本用。
    /// MoveComponent 里方向是瞬切的（用户要求转弯无 lerp），但 BlendTree 视觉上需要软化，否则连续切方向时姿势会瞬移。
    /// 0.1 = 大约 6 帧内追上目标值，体感顺滑且不拖沓。</summary>
    public float AnimMoveDampTime = 0.1f;

    private Character character;

    private Transform weaponSocket;
    private GameObject spawnedWeapon;
    private string spawnedWeaponPath;
    private ResMgr resMgr;

    private static readonly int HashMoveX = Animator.StringToHash("MoveX");
    private static readonly int HashMoveY = Animator.StringToHash("MoveY");
    private static readonly int HashSpeed = Animator.StringToHash("Speed");
    private static readonly int HashIsShooting = Animator.StringToHash("IsShooting");
    private static readonly int HashIsAiming = Animator.StringToHash("IsAiming");
    private static readonly int HashMeleeAttack = Animator.StringToHash("MeleeAttack");
    private static readonly int HashMeleeType = Animator.StringToHash("MeleeType");
    private static readonly int HashWeaponSwap = Animator.StringToHash("WeaponSwap");

    private void Awake()
    {
        Controller = GetComponent<CharacterController>();
        if (Controller == null)
            Controller = gameObject.AddComponent<CharacterController>();
        Anim = GetComponentInChildren<Animator>();
        if (Anim == null)
            Debug.LogWarning($"[CharacterView] {name} 找不到 Animator");
        weaponSocket = FindChildByName(transform, WeaponSocketName);
        if (weaponSocket == null)
            Debug.LogWarning($"[CharacterView] {name} 找不到骨骼 {WeaponSocketName}，武器挂载会失败");
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
        if (ctx != null) ctx.TryGet(out resMgr);
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
        }

        SyncWeaponModel();
    }

    /// 比较 Character.CurrentWeaponModelPath 和当前挂载，不一致才卸/挂，避免每帧拆装。
    private void SyncWeaponModel()
    {
        if (weaponSocket == null) return;
        var targetPath = character.CurrentWeaponModelPath;
        if (targetPath == spawnedWeaponPath)
        {
            if (spawnedWeapon != null)
            {
                spawnedWeapon.transform.localPosition = character.CurrentWeaponLocalPosition;
                spawnedWeapon.transform.localRotation = Quaternion.Euler(character.CurrentWeaponLocalEuler);
            }
            return;
        }

        if (spawnedWeapon != null) Destroy(spawnedWeapon);
        if (!string.IsNullOrEmpty(spawnedWeaponPath) && resMgr != null)
            resMgr.Release<GameObject>(spawnedWeaponPath);
        spawnedWeapon = null;
        spawnedWeaponPath = null;

        if (!string.IsNullOrEmpty(targetPath))
        {
            var prefab = resMgr != null
                ? resMgr.Load<GameObject>(targetPath)
                : Resources.Load<GameObject>(targetPath);
            if (prefab != null)
            {
                spawnedWeapon = Instantiate(prefab, weaponSocket);
                spawnedWeapon.transform.localPosition = character.CurrentWeaponLocalPosition;
                spawnedWeapon.transform.localRotation = Quaternion.Euler(character.CurrentWeaponLocalEuler);
                spawnedWeaponPath = targetPath;
            }
            else
            {
                Debug.LogError($"[CharacterView] 加载武器模型失败: {targetPath}");
            }
        }
    }

    private void OnDestroy()
    {
        if (!string.IsNullOrEmpty(spawnedWeaponPath) && resMgr != null)
            resMgr.Release<GameObject>(spawnedWeaponPath);
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChildByName(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
