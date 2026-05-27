using UnityEngine;
using YOTO;

/// <summary>
/// 角色的 view：被动从 Character 读数据驱动 CC / Transform / Animator / 武器挂点。
/// Character 不知道 view 存在，view 通过 Bind 拿到 Character 引用，只读意图、回写物理状态。
/// 在 LateUpdate 跑：保证 GameLoop.Update 里所有组件 Tick 写完意图后再消费。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class CharacterView : BaseView
{
    public CharacterController Controller { get; private set; }
    public Animator Anim { get; private set; }

    /// <summary>反滑步参考：BlendTree walk clip 的内禀位移速度（m/s）。</summary>
    public float ReferenceWalkSpeed = 1.6f;

    /// <summary>武器挂点骨骼名（RifleAnimsetPro 的 Dummy 用的是 RightHandProp）。</summary>
    public string WeaponSocketName = "RightHandProp";

    private Character character;
    private RuntimeAnimatorController defaultAnimController;
    private RuntimeAnimatorController appliedAnimController;

    private Transform weaponSocket;
    private GameObject spawnedWeapon;
    private string spawnedWeaponPath;
    private ResMgr resMgr;

    private static readonly int HashMoveX = Animator.StringToHash("MoveX");
    private static readonly int HashMoveY = Animator.StringToHash("MoveY");
    private static readonly int HashIsShooting = Animator.StringToHash("IsShooting");
    private static readonly int HashMeleeAttack = Animator.StringToHash("MeleeAttack");
    private static readonly int HashMeleeType = Animator.StringToHash("MeleeType");

    private void Awake()
    {
        Controller = GetComponent<CharacterController>();
        if (Controller == null)
            Controller = gameObject.AddComponent<CharacterController>();
        Anim = GetComponentInChildren<Animator>();
        if (Anim == null)
            Debug.LogWarning($"[CharacterView] {name} 找不到 Animator");
        else
        {
            defaultAnimController = Anim.runtimeAnimatorController;
            appliedAnimController = defaultAnimController;
        }
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
            var targetCtl = character.CurrentAnimController != null
                ? character.CurrentAnimController
                : defaultAnimController;
            if (targetCtl != appliedAnimController)
            {
                Anim.runtimeAnimatorController = targetCtl;
                appliedAnimController = targetCtl;
            }

            // 反滑步：locomotion 速率按真实水平速度比例放缩
            var horiz = new Vector2(character.WishVelocity.x, character.WishVelocity.z).magnitude;
            Anim.speed = horiz > 0.05f && ReferenceWalkSpeed > 0.01f
                ? Mathf.Max(1f, horiz / ReferenceWalkSpeed)
                : 1f;

            Anim.SetFloat(HashMoveX, character.AnimMoveX);
            Anim.SetFloat(HashMoveY, character.AnimMoveY);
            Anim.SetBool(HashIsShooting, character.IsShooting);
            if (character.MeleeAttack)
            {
                Anim.SetInteger(HashMeleeType, character.MeleeType);
                Anim.SetTrigger(HashMeleeAttack);
                character.MeleeAttack = false;
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
            // path 没变，但 offset 可能改了——同步一下
            if (spawnedWeapon != null)
            {
                spawnedWeapon.transform.localPosition = character.CurrentWeaponLocalPosition;
                spawnedWeapon.transform.localRotation = Quaternion.Euler(character.CurrentWeaponLocalEuler);
            }
            return;
        }

        // 卸旧
        if (spawnedWeapon != null) Destroy(spawnedWeapon);
        if (!string.IsNullOrEmpty(spawnedWeaponPath) && resMgr != null)
            resMgr.Release<GameObject>(spawnedWeaponPath);
        spawnedWeapon = null;
        spawnedWeaponPath = null;

        // 挂新
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
