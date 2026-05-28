using UnityEngine;

/// <summary>
/// 角色级动画集：跟**角色**绑定（不跟武器走）。含通用 locomotion + death + UpperBodyMask + Mixer 阈值。
/// 跟 <see cref="WeaponAnimSet"/> 分工：
///   - **CharacterAnimSet**：Idle / Walk / Run / Sprint（locomotion 全身）+ DeathL/R（死亡）+ UpperBodyMask + locomotion 阈值
///   - **WeaponAnimSet**：Aim 8 方向 strafe + Shoot / Reload / Equip / Holster / Melee（上半身 + 持枪相关）
///
/// 切武器只重新加载 WeaponAnimSet，CharacterAnimSet 保持——下半身走路 mixer 不被打断。
///
/// **使用流程**（美工）：
///   1. Project 窗口 → Create → TPS → CharacterAnimSet
///   2. 放 Assets/Resources/Character/ 下，命名 &lt;角色名&gt;AnimSet.asset（如 PlayerAnimSet.asset）
///   3. 拖 Idle / Walk / Run / Sprint / DeathL / DeathR clip；拖 UpperBody.mask；调阈值
///   4. 程序员把 Resources 相对路径配到 Factory：character.CurrentCharacterAnimSetPath = "Character/PlayerAnimSet"
/// </summary>
[CreateAssetMenu(fileName = "CharacterAnimSet", menuName = "TPS/CharacterAnimSet", order = 99)]
public class CharacterAnimSet : ScriptableObject
{
    [Header("Locomotion 全身 (LinearMixerState 4 child)")]
    public AnimationClip Idle;
    public AnimationClip Walk;
    public AnimationClip Run;
    public AnimationClip Sprint;

    [Header("Locomotion 阈值（按 AnimSpeedRatio 真实 m/s blend）")]
    public float IdleThreshold = 0f;
    [Tooltip("一般 = MoveComponent.WalkSpeed")]
    public float WalkThreshold = 5f;
    public float RunThreshold = 6f;
    [Tooltip("一般 = MoveComponent.SprintSpeed")]
    public float SprintThreshold = 7f;

    [Header("死亡变体（DeathVariant 索引）")]
    public AnimationClip DeathL;   // DeathVariant=0
    public AnimationClip DeathR;   // DeathVariant=1

    [Header("分层（上下身分离）")]
    [Tooltip("上半身骨骼 mask。配了启用 Animancer Layer 1（Combat 走上半身 / Locomotion 走全身）；留空则 Combat 覆盖全身（单层模式）")]
    public AvatarMask UpperBodyMask;

    [Header("Fade")]
    [Tooltip("Locomotion 之间切换 / 触发 state 进入的淡入时长")]
    public float DefaultFade = 0.1f;
}
