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
            Anim.SetFloat(HashMoveX, character.AnimMoveX);
            Anim.SetFloat(HashMoveY, character.AnimMoveY);
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
