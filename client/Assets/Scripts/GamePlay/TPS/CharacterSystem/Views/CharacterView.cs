using UnityEngine;

/// <summary>
/// 角色的 view：被动从 Character 读数据驱动 CC / Transform / Animator。
/// Character 不知道 view 存在，view 通过 Bind 拿到 Character 引用，只读意图、回写物理状态。
/// 在 LateUpdate 跑：保证 GameLoop.Update 里所有组件 Tick 写完意图后再消费。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class CharacterView : BaseView
{
    public CharacterController Controller { get; private set; }
    public Animator Anim { get; private set; }

    /// <summary>反滑步参考：BlendTree walk clip 的内禀位移速度（m/s）。
    /// 角色实际水平速度除以这个值放缩 Animator.speed，让脚步对齐位移。
    /// Rifle_WalkFwdLoop 实测约 1.6 m/s，加 sprint 也只到 2~3，刚好范围。</summary>
    public float ReferenceWalkSpeed = 1.6f;

    private Character character;

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
    }

    public override void Bind(Actor actor, int id)
    {
        character = actor as Character;
        ID = id;
        if (character == null) return;

        // 给逻辑层初值：transform 当前位置/朝向就是出生点
        character.Position = transform.position;
        character.Rotation = transform.rotation;
        character.IsGrounded = Controller.isGrounded;
    }

    private void LateUpdate()
    {
        if (character == null) return;

        // 1. 应用速度意图（包含重力 y 分量）
        Controller.Move(character.WishVelocity * Time.deltaTime);

        // 2. 应用朝向意图（旋转平滑由组件做完，view 直接套用）
        transform.rotation = character.Rotation;

        // 3. 物理状态回写
        character.Position = transform.position;
        character.IsGrounded = Controller.isGrounded;

        // 4. 驱动动画
        if (Anim != null)
        {
            // 反滑步：locomotion 速率按真实水平速度比例放缩
            // 静止时回 1（idle 不会滑），低速时不降速以免动作过慢，高速时按比例放快
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
                character.MeleeAttack = false; // 消费 trigger
            }
        }
    }
}
