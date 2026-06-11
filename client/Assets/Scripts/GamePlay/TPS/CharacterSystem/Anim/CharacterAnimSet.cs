using UnityEngine;

/// <summary>
/// 角色级动画集：跟**角色**绑定（不跟武器走）。含**下半身全部 locomotion**（含瞄准 strafe）+ death + UpperBodyMask + Mixer 阈值。
/// 跟 <see cref="WeaponAnimSet"/> 分工：
///   - **CharacterAnimSet（角色级）**：Idle / Walk / Run / Sprint（非瞄准 1D locomotion）+ Aim 8 方向 strafe（瞄准 2D locomotion，下半身）+ DeathL/R + UpperBodyMask + 阈值
///   - **WeaponAnimSet（武器级）**：IdleGunPose / AimPose（上身持枪 pose）+ Shoot / Reload / Equip / Holster（上身 one-shot）
///
/// 下半身（含瞄准 strafe）跟武器无关，全角色共用一份；切武器只重新加载 WeaponAnimSet（上身），CharacterAnimSet 保持——下半身 mixer 不被打断、不重建。
///
/// **使用流程**（美工）：
///   1. Project 窗口 → Create → TPS → CharacterAnimSet
///   2. 放 Assets/Resources/Character/ 下，命名 &lt;角色名&gt;AnimSet.asset（如 PlayerAnimSet.asset）
///   3. 拖 Idle / Walk / Run / Sprint（非瞄准）+ AimIdle / AimWalk / 8 方向 strafe（瞄准下身）+ DeathL / DeathR；拖 UpperBody.mask；调阈值
///   4. 程序员把 Resources 相对路径配到 Factory：character.CurrentCharacterAnimSetPath = "Character/Player/Animations/PlayerAnimSet"
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

    [Header("瞄准下身 Locomotion（CartesianMixerState 2D，按 AnimMoveX/Y 选 clip。上身被 WeaponAnimSet 的 AimPose 覆盖，这里只剩腿）")]
    [Tooltip("瞄准静立（中心 (0,0)）。8 方向 strafe 缺失时也作 1D 兜底")]
    public AnimationClip AimIdle;
    [Tooltip("1D 兜底：8 方向 strafe 缺失时用此 clip 走任意方向")]
    public AnimationClip AimWalk;
    [Tooltip("前 (0,1)。null 时 fallback AimWalk")]
    public AnimationClip AimWalkFwd;
    [Tooltip("后 (0,-1)")]
    public AnimationClip AimWalkBwd;
    [Tooltip("左 (-1,0)")]
    public AnimationClip AimStrafeLeft;
    [Tooltip("右 (1,0)")]
    public AnimationClip AimStrafeRight;
    [Tooltip("前左 (-0.707, 0.707)。null 时 fallback AimWalkFwd")]
    public AnimationClip AimStrafeFL;
    [Tooltip("前右 (0.707, 0.707)")]
    public AnimationClip AimStrafeFR;
    [Tooltip("后左 (-0.707, -0.707)。null 时 fallback AimWalkBwd")]
    public AnimationClip AimStrafeBL;
    [Tooltip("后右 (0.707, -0.707)")]
    public AnimationClip AimStrafeBR;

    [Header("闪避（DodgeComponent 按移动意图相对角色朝向 4 向选 clip；clip 为 in-place，位移由代码驱动）")]
    [Tooltip("前向闪避：移动意图相对角色朝向偏前时")]
    public AnimationClip DodgeFwd;
    [Tooltip("后向闪避：偏后时；也是【无方向输入】时的默认后撤步")]
    public AnimationClip DodgeBwd;
    [Tooltip("左向闪避：偏左时")]
    public AnimationClip DodgeLeft;
    [Tooltip("右向闪避：偏右时")]
    public AnimationClip DodgeRight;

    [Header("死亡变体（DeathVariant 索引）")]
    public AnimationClip DeathL;   // DeathVariant=0
    public AnimationClip DeathR;   // DeathVariant=1

    [Header("分层（上下身分离）")]
    [Tooltip("上半身骨骼 mask。配了启用 Animancer Layer 1（Combat 走上半身 / Locomotion 走全身）；留空则 Combat 覆盖全身（单层模式）")]
    public AvatarMask UpperBodyMask;

    [Header("Fade")]
    [Tooltip("Locomotion 之间切换 / 触发 state 进入的淡入时长")]
    public float DefaultFade = 0.1f;

    /// <summary>按方向取闪避 clip，带缺失兜底（缺某向 → DodgeBwd → DodgeFwd）。
    /// FullBodyDriver 解析闪避意图时调——把原先散在 DodgeComponent 的"选向 + 兜底"逻辑收到资产侧，逻辑层不再碰 clip。
    /// 用 <c>== null</c>（Unity 重载）而非 ?? ——避免 fake-null 取到已销毁引用。</summary>
    public AnimationClip GetDodgeClip(DodgeDir dir)
    {
        AnimationClip clip = dir switch
        {
            DodgeDir.Fwd => DodgeFwd,
            DodgeDir.Bwd => DodgeBwd,
            DodgeDir.Left => DodgeLeft,
            DodgeDir.Right => DodgeRight,
            _ => DodgeBwd,
        };
        if (clip == null) clip = DodgeBwd;
        if (clip == null) clip = DodgeFwd;
        return clip;
    }
}
