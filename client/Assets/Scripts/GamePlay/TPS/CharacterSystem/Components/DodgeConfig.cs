using UnityEngine;

/// <summary>
/// 翻滚手感配置（ScriptableObject）：把 <see cref="DodgeComponent"/> 的可调参数挪到资产里，可在 Inspector
/// **拖曲线 / 调数值**。<see cref="CharacterFactory"/> 经 <see cref="DodgeComponent.DodgeConfigPath"/> 指向本资产
/// （Resources 相对路径），DodgeComponent.Attach 时加载并覆盖组件默认值；资产缺失则回退组件内置默认（不崩）。
///
/// 创建：菜单 Assets/Create/TPS/Dodge Config，放到 Resources 下对应路径（见 <see cref="CharacterResPath.PlayerDodgeConfig"/>，
/// 默认 Resources/Character/Player/Config/DodgeConfig.asset）。
/// </summary>
[CreateAssetMenu(menuName = "TPS/Dodge Config", fileName = "DodgeConfig")]
public class DodgeConfig : ScriptableObject
{
    [Header("位移")]
    [Tooltip("一次翻滚的总位移（米）")]
    public float Distance = 4f;
    [Tooltip("翻滚时长（秒）= 全身锁定总时长")]
    public float Duration = 1f;
    [Tooltip("位移随进度的分布曲线：x=进度 0→1，y=已位移占比 0→1（终点须到 1，否则冲不满 Distance）。\n默认 ease-out：起步爆发、收尾刹车。")]
    public AnimationCurve DistanceProfile = new AnimationCurve(new Keyframe(0f, 0f, 0f, 2f), new Keyframe(1f, 1f, 0f, 0f));

    [Header("时机 / 二连冷却（第一下→短 cd 接第二下；第二下→长 cd）")]
    [Tooltip("二连第一下后的短冷却（秒）")]
    public float ShortCooldown = 0.5f;
    [Tooltip("二连第二下后的长冷却（秒）")]
    public float LongCooldown = 2f;
    [Tooltip("二连复位窗（秒，从第一下起算）：超过没接第二下则退回第一下")]
    public float ComboResetWindow = 2f;
    [Tooltip("进入翻滚的淡入时长（秒）")]
    public float EnterFade = 0.3f;
    [Tooltip("翻滚结束回 locomotion 的淡入时长（秒）")]
    public float RecoverFade = 0.12f;

    [Header("尾段转向瞄准（翻滚结束面向鼠标）")]
    [Tooltip("是否在翻滚尾段把朝向转向瞄准/移动方向。false=整段保持起手朝向")]
    public bool TurnToAim = true;
    [Tooltip("从归一化进度的哪一点开始转向（前段保持起手朝向把方向 clip 滚干净）。0.6 ≈ 位移基本走完+无敌帧结束后才转，避免半截转身")]
    [Range(0f, 1f)] public float TurnToAimStartNorm = 0.6f;
    [Tooltip("尾段转向的 slerp 速率（指数收敛、帧率无关）。25 ≈ 在剩余尾段+恢复淡入内转到位")]
    public float TurnToAimRate = 25f;

    [Header("无敌帧")]
    [Tooltip("是否开启无敌帧。false=纯位移闪身（无免伤）")]
    public bool Invulnerable = true;
    [Tooltip("无敌帧开始（归一化时间 0→1，相对 Duration）")]
    [Range(0f, 1f)] public float InvulnStartNorm = 0f;
    [Tooltip("无敌帧结束（归一化时间 0→1）。一般留点收尾破绽（如 0.6），不要全程无敌")]
    [Range(0f, 1f)] public float InvulnEndNorm = 0.6f;
}
