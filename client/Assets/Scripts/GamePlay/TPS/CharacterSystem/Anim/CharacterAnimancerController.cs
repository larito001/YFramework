using Animancer;
using UnityEngine;

/// <summary>
/// **玩家**动画驱动器。继承 <see cref="LocomotionAnimController"/>（locomotion / death / 分层 / one-shot 生命周期），
/// 在此只实现**武器 + 瞄准**战斗段：
///   - <see cref="WeaponAnimSet"/> 加载（combat/aim 跟武器走，切枪时换；Tick 里按 character.WeaponAnimDirty 自治加载）
///   - 瞄准 2D Cartesian mixer（8 方向 strafe，替代基类 1D locomotion；带 SmoothDamp 平滑）
///   - 武器 one-shot：Melee（全身） &gt; Holster &gt; Equip &gt; Reload &gt; Shoot（上半身）
///
/// 通用层（Die / 非瞄准 locomotion / Layer mask / one-shot 进出生命周期）全在基类，玩家行为不变。
/// </summary>
public class CharacterAnimancerController : LocomotionAnimController
{
    /// <summary>Aim mixer ParameterX/Y 的 SmoothDamp 时间。MoveComponent 转向瞬切，没 damp 会导致 mixer 9 child 权重瞬变硬切。</summary>
    public float AnimMoveDampTime = 0.1f;

    // ── WeaponAnimSet（跟武器走）──
    private WeaponAnimSet weaponAnimSet;
    private CartesianMixerState aimLocomotionMixer; // 瞄准 8 方向（用 weaponAnimSet 的 clip）

    // Aim mixer SmoothDamp
    private float smoothedAnimMoveX;
    private float smoothedAnimMoveY;
    private float smoothMoveXVel;
    private float smoothMoveYVel;

    public override void Dispose()
    {
        weaponAnimSet = null;
        aimLocomotionMixer = null;
        smoothedAnimMoveX = 0f;
        smoothedAnimMoveY = 0f;
        smoothMoveXVel = 0f;
        smoothMoveYVel = 0f;
        base.Dispose();
    }

    /// <summary>切 WeaponAnimSet（如果 WeaponComponent 通知）。non-aim locomotion mixer 不动（跟 character 走）。</summary>
    protected override void PreDrive(Character character)
    {
        if (character.WeaponAnimDirty)
        {
            character.WeaponAnimDirty = false;
            LoadWeaponAnimSet(character.CurrentWeaponAnimSetPath);
        }
    }

    /// <summary>武器战斗段（上半身 one-shot）。clip 来自 WeaponAnimSet（无武器则消费 trigger 不播）。
    /// 近战已上移为通用"技能"（基类技能分支处理），这里只剩 Holster/Equip/Reload/Shoot，均不短路（返回 false 走生命周期）。</summary>
    protected override bool DriveCombat(Character character, float fade)
    {
        var combatLayer = useUpperBodyLayer ? Animancer.Layers[1] : Animancer.Layers[0];

        if (weaponAnimSet == null)
        {
            // 没 weaponAnimSet 时消费 combat trigger 避免下一帧重复触发
            if (character.WeaponHolster) character.WeaponHolster = false;
            if (character.WeaponSwap) character.WeaponSwap = false;
            if (character.Reload) character.Reload = false;
            if (character.Shoot) character.Shoot = false;
            return false;
        }

        if (character.WeaponHolster)
        {
            character.WeaponHolster = false;
            if (weaponAnimSet.Holster != null) activeOneShotState = combatLayer.Play(weaponAnimSet.Holster, fade);
        }
        else if (character.WeaponSwap)
        {
            character.WeaponSwap = false;
            if (weaponAnimSet.Equip != null) activeOneShotState = combatLayer.Play(weaponAnimSet.Equip, fade);
        }
        else if (character.Reload)
        {
            character.Reload = false;
            if (weaponAnimSet.Reload != null) activeOneShotState = combatLayer.Play(weaponAnimSet.Reload, fade);
        }
        else if (character.Shoot)
        {
            character.Shoot = false;
            var clip = character.HeavyRecoil ? weaponAnimSet.ShootHeavy : weaponAnimSet.ShootLight;
            if (clip != null)
            {
                var s = combatLayer.Play(clip, weaponAnimSet.ShootFade);
                if (s != null && character.RecoilAnimSpeed > 0f) s.Speed = character.RecoilAnimSpeed;
                activeOneShotState = s;
            }
        }
        return false;
    }

    /// <summary>瞄准时用 weaponAnimSet 的 2D Cartesian mixer（SmoothDamp 平滑）；否则回退基类 1D locomotion。</summary>
    protected override void UpdateLocomotion(Character character)
    {
        if (character.IsAiming && aimLocomotionMixer != null)
        {
            float fade = overrideNextLocomotionFade > 0f ? overrideNextLocomotionFade : GetDefaultFade();
            if (currentLayer0Mixer != aimLocomotionMixer)
            {
                Animancer.Layers[0].Play(aimLocomotionMixer, fade);
                currentLayer0Mixer = aimLocomotionMixer;
                overrideNextLocomotionFade = 0f;
            }
            smoothedAnimMoveX = Mathf.SmoothDamp(smoothedAnimMoveX, character.AnimMoveX, ref smoothMoveXVel, AnimMoveDampTime, Mathf.Infinity, Time.deltaTime);
            smoothedAnimMoveY = Mathf.SmoothDamp(smoothedAnimMoveY, character.AnimMoveY, ref smoothMoveYVel, AnimMoveDampTime, Mathf.Infinity, Time.deltaTime);
            aimLocomotionMixer.ParameterX = smoothedAnimMoveX;
            aimLocomotionMixer.ParameterY = smoothedAnimMoveY;
        }
        else
        {
            base.UpdateLocomotion(character);
        }
    }

    /// <summary>加载 WeaponAnimSet（切枪时换）+ 重建 aim mixer。non-aim locomotion mixer 不动（跟 character 走）。</summary>
    private void LoadWeaponAnimSet(string path)
    {
        weaponAnimSet = null;
        aimLocomotionMixer = null;
        // 不清 currentLayer0Mixer 也不清 activeOneShotState——切枪不该打断下半身 locomotion，combat trigger 自然下一帧覆盖

        if (string.IsNullOrEmpty(path)) return;
        if (resMgr == null) return;
        var set = resMgr.Load<WeaponAnimSet>(path);
        if (set == null)
        {
            Debug.LogWarning($"[CharacterAnimancerController] WeaponAnimSet 加载失败: {path}");
            return;
        }

        weaponAnimSet = set;
        BuildAimMixer();
    }

    /// <summary>构造瞄准 aim mixer：CartesianMixerState 9 child（idle 中心 + 8 方向 strafe），按 (AnimMoveX, AnimMoveY) 2D blend。
    /// null 方向 clip 用 AimWalk/AimWalkFwd/Bwd 兜底。</summary>
    private void BuildAimMixer()
    {
        if (weaponAnimSet == null || weaponAnimSet.AimIdle == null) return;

        var fallback = weaponAnimSet.AimWalk != null ? weaponAnimSet.AimWalk : weaponAnimSet.AimIdle;
        var fwd = weaponAnimSet.AimWalkFwd != null ? weaponAnimSet.AimWalkFwd : fallback;
        var bwd = weaponAnimSet.AimWalkBwd != null ? weaponAnimSet.AimWalkBwd : fallback;
        var right = weaponAnimSet.AimStrafeRight != null ? weaponAnimSet.AimStrafeRight : fallback;
        var left = weaponAnimSet.AimStrafeLeft != null ? weaponAnimSet.AimStrafeLeft : fallback;
        var fr = weaponAnimSet.AimStrafeFR != null ? weaponAnimSet.AimStrafeFR : fwd;
        var fl = weaponAnimSet.AimStrafeFL != null ? weaponAnimSet.AimStrafeFL : fwd;
        var br = weaponAnimSet.AimStrafeBR != null ? weaponAnimSet.AimStrafeBR : bwd;
        var bl = weaponAnimSet.AimStrafeBL != null ? weaponAnimSet.AimStrafeBL : bwd;

        aimLocomotionMixer = new CartesianMixerState();
        aimLocomotionMixer.AddRange(weaponAnimSet.AimIdle, fwd, fr, right, br, bwd, bl, left, fl);
        const float d = 0.7071f;
        aimLocomotionMixer.SetThresholds(
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(d, d),
            new Vector2(1f, 0f),
            new Vector2(d, -d),
            new Vector2(0f, -1f),
            new Vector2(-d, -d),
            new Vector2(-1f, 0f),
            new Vector2(-d, d));
        var idleChild = aimLocomotionMixer.GetChild(0);
        if (idleChild != null) aimLocomotionMixer.DontSynchronize(idleChild);
    }
}
