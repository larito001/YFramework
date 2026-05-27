using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// 菜单：Tools/TPS/Build Player Animator Controller
/// 重新生成 Assets/Resources/Animations/playerController.controller：
///   Base Layer（全身）：
///     Walk (state, 2D Freeform Cartesian, MoveX/MoveY)    瞄准时，举枪 + 8 方向 strafe
///     Sprint (sub-SM)                                     不瞄准时
///       Idle (default, Rifle_Idle_GunDown)
///       Start (Rifle_SprintStart)        Idle  →[Speed>0.1]→ Start
///       Loop  (Rifle_SprintLoop)         Start →[exitTime]→ Loop
///       Stop  (Rifle_SprintStop_RU)      Loop  →[Speed<0.1]→ Stop  →[exitTime]→ Idle
///       AnyState →[IsAiming]→ Walk (跨 SM 出口)
///     Equipping (EquipRifle)                              切枪过场
///     MeleeHard / MeleeKick                               近战
///     IsAiming 切换 Walk ↔ Sprint(sub-SM Entry → Idle)
///     WeaponSwap trigger → AnyState → Equipping
///     MeleeAttack trigger + MeleeType → AnyState → MeleeHard / MeleeKick
///   Upper Body Recoil Layer：UpperBodyMask + Additive，IsShooting=true 切到 ShootLoop_Additive
public static class PlayerAnimatorBuilder
{
    private const string ControllerPath = "Assets/Resources/Animations/playerController.controller";
    private const string RifleFbxPath = "Assets/Art/Animations/RifleAnimsetPro.fbx";
    private const string DiagonalsFbxPath = "Assets/Art/Animations/RifleAnimsetPro_Diagonals.fbx";
    private const string AdditionalsFbxPath = "Assets/Art/Animations/RifleAnimsetPro_Additionals.fbx";
    private const string EquipsFbxPath = "Assets/Art/Animations/RifleAnimsetPro_Equips.fbx";
    private const string SprintFbxPath = "Assets/Art/Animations/RifleAnimsetPro_Sprint.fbx";
    private const string UpperBodyMaskPath = "Assets/Art/Characters/UpperBodyMask.mask";

    private const string ParamMoveX = "MoveX";
    private const string ParamMoveY = "MoveY";
    private const string ParamSpeed = "Speed";
    private const string ParamIsShooting = "IsShooting";
    private const string ParamIsAiming = "IsAiming";
    private const string ParamMeleeAttack = "MeleeAttack";
    private const string ParamMeleeType = "MeleeType";
    private const string ParamWeaponSwap = "WeaponSwap";

    private const int MeleeTypeHard = 0;
    private const int MeleeTypeKick = 1;

    private const float Diag = 0.70710677f;

    [MenuItem("Tools/TPS/Build Player Animator Controller")]
    public static void Build()
    {
        var clips = new Dictionary<string, AnimationClip>();
        if (!LoadClipsInto(clips, RifleFbxPath)) return;
        if (!LoadClipsInto(clips, DiagonalsFbxPath)) return;
        if (!LoadClipsInto(clips, AdditionalsFbxPath)) return;
        if (!LoadClipsInto(clips, EquipsFbxPath)) return;
        if (!LoadClipsInto(clips, SprintFbxPath)) return;

        // Walk BlendTree clips
        var idle = Require(clips, "Rifle_Idle");
        var wFwd = Require(clips, "Rifle_WalkFwdLoop");
        var wBwd = Require(clips, "Rifle_WalkBwdLoop");
        var wLeft = Require(clips, "Rifle_StrafeLeftLoop");
        var wRight = Require(clips, "Rifle_StrafeRightLoop");
        var wFwdL = Require(clips, "Rifle_StrafeLeft45Loop");
        var wFwdR = Require(clips, "Rifle_StrafeRight45Loop");
        var wBwdL = Require(clips, "Rifle_StrafeLeft135Loop");
        var wBwdR = Require(clips, "Rifle_StrafeRight135Loop");
        // Sprint sub-SM clips（不瞄准时角色总是面向移动方向）
        var idleDown = Require(clips, "Rifle_Idle_GunDown");
        var sprintStart = Require(clips, "Rifle_SprintStart");
        var sprintLoop = Require(clips, "Rifle_SprintLoop");
        var sprintStop = Require(clips, "Rifle_SprintStop_RU");
        // 其他
        var shootAdd = Require(clips, "Rifle_ShootLoop_Additive");
        var meleeHard = Require(clips, "Rifle_Melee_Hard");
        var meleeKick = Require(clips, "Rifle_Melee_Kick");
        var equip = Require(clips, "EquipRifle");
        if (idle == null || wFwd == null || wBwd == null || wLeft == null || wRight == null ||
            wFwdL == null || wFwdR == null || wBwdL == null || wBwdR == null ||
            idleDown == null || sprintStart == null || sprintLoop == null || sprintStop == null ||
            shootAdd == null || meleeHard == null || meleeKick == null || equip == null)
            return;

        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
        if (mask == null)
        {
            Debug.LogError($"[PlayerAnimator] 找不到上半身遮罩 {UpperBodyMaskPath}");
            return;
        }

        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/Animations");

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            AssetDatabase.DeleteAsset(ControllerPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter(ParamMoveX, AnimatorControllerParameterType.Float);
        controller.AddParameter(ParamMoveY, AnimatorControllerParameterType.Float);
        controller.AddParameter(ParamSpeed, AnimatorControllerParameterType.Float);
        controller.AddParameter(ParamIsShooting, AnimatorControllerParameterType.Bool);
        controller.AddParameter(ParamIsAiming, AnimatorControllerParameterType.Bool);
        controller.AddParameter(ParamMeleeAttack, AnimatorControllerParameterType.Trigger);
        controller.AddParameter(ParamMeleeType, AnimatorControllerParameterType.Int);
        controller.AddParameter(ParamWeaponSwap, AnimatorControllerParameterType.Trigger);

        BuildBaseLayer(controller, idle, wFwd, wBwd, wLeft, wRight, wFwdL, wFwdR, wBwdL, wBwdR,
            idleDown, sprintStart, sprintLoop, sprintStop, meleeHard, meleeKick, equip);
        BuildRecoilLayer(controller, shootAdd, mask);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[PlayerAnimator] 生成完成: {ControllerPath}");
    }

    private static bool LoadClipsInto(Dictionary<string, AnimationClip> dict, string fbxPath)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        if (all == null || all.Length == 0)
        {
            Debug.LogError($"[PlayerAnimator] 找不到 FBX 资源 {fbxPath}");
            return false;
        }
        foreach (var obj in all)
        {
            if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                dict[clip.name] = clip;
        }
        return true;
    }

    private static AnimationClip Require(Dictionary<string, AnimationClip> clips, string name)
    {
        if (clips.TryGetValue(name, out var c)) return c;
        Debug.LogError($"[PlayerAnimator] 找不到动画 take: {name}");
        return null;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;
        var parent = System.IO.Path.GetDirectoryName(folderPath).Replace('\\', '/');
        var leaf = System.IO.Path.GetFileName(folderPath);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    private static void BuildBaseLayer(AnimatorController controller,
        AnimationClip idle, AnimationClip wFwd, AnimationClip wBwd, AnimationClip wLeft, AnimationClip wRight,
        AnimationClip wFwdL, AnimationClip wFwdR, AnimationClip wBwdL, AnimationClip wBwdR,
        AnimationClip idleDown, AnimationClip sprintStart, AnimationClip sprintLoop, AnimationClip sprintStop,
        AnimationClip meleeHard, AnimationClip meleeKick, AnimationClip equip)
    {
        var sm = controller.layers[0].stateMachine;

        // ── Walk BlendTree（瞄准时，8 方向）──
        var walkTree = new BlendTree
        {
            name = "WalkTree",
            blendType = BlendTreeType.FreeformCartesian2D,
            blendParameter = ParamMoveX,
            blendParameterY = ParamMoveY,
            hideFlags = HideFlags.HideInHierarchy,
        };
        AssetDatabase.AddObjectToAsset(walkTree, controller);
        walkTree.AddChild(idle, new Vector2(0f, 0f));
        walkTree.AddChild(wFwd, new Vector2(0f, 1f));
        walkTree.AddChild(wBwd, new Vector2(0f, -1f));
        walkTree.AddChild(wLeft, new Vector2(-1f, 0f));
        walkTree.AddChild(wRight, new Vector2(1f, 0f));
        walkTree.AddChild(wFwdL, new Vector2(-Diag, Diag));
        walkTree.AddChild(wFwdR, new Vector2(Diag, Diag));
        walkTree.AddChild(wBwdL, new Vector2(-Diag, -Diag));
        walkTree.AddChild(wBwdR, new Vector2(Diag, -Diag));

        // ── 顶层 states ──
        var walk = sm.AddState("Walk", new Vector3(280, 60, 0));
        walk.motion = walkTree;

        var hard = sm.AddState("MeleeHard", new Vector3(560, 0, 0));
        hard.motion = meleeHard;

        var kick = sm.AddState("MeleeKick", new Vector3(560, 100, 0));
        kick.motion = meleeKick;

        var equipping = sm.AddState("Equipping", new Vector3(560, 200, 0));
        equipping.motion = equip;

        // ── Sprint sub-state machine（不瞄准时进入）──
        var sprintSM = BuildSprintSubSM(sm, walk, idleDown, sprintStart, sprintLoop, sprintStop);

        sm.defaultState = walk; // 任意默认都行，AnyState 会立即按 IsAiming 修正

        // Walk ↔ Sprint（IsAiming 切换）。Walk → Sprint 目标 sub-SM Entry，Sprint → Walk 由 sub-SM 内 AnyState 处理
        var w2s = walk.AddTransition(sprintSM);
        w2s.hasExitTime = false;
        w2s.duration = 0.15f;
        w2s.AddCondition(AnimatorConditionMode.IfNot, 0f, ParamIsAiming);

        // 近战：AnyState → MeleeHard/Kick，播完按 IsAiming 回 Walk 或 Sprint
        AddAnyStateTransition(sm, hard, ParamMeleeAttack,
            (AnimatorConditionMode.Equals, MeleeTypeHard, ParamMeleeType));
        AddAnyStateTransition(sm, kick, ParamMeleeAttack,
            (AnimatorConditionMode.Equals, MeleeTypeKick, ParamMeleeType));
        AddReturnToLocomotion(hard, walk, sprintSM);
        AddReturnToLocomotion(kick, walk, sprintSM);

        // 切枪：AnyState → Equipping，播完按 IsAiming 回 Walk 或 Sprint
        AddAnyStateTransition(sm, equipping, ParamWeaponSwap);
        AddReturnToLocomotion(equipping, walk, sprintSM);
    }

    /// Sprint sub-SM：Idle (default) → Start → Loop → Stop → Idle。
    /// AnyState 内 → Walk on IsAiming=true（跨 SM 出口）。
    private static AnimatorStateMachine BuildSprintSubSM(AnimatorStateMachine parent, AnimatorState walk,
        AnimationClip idleDown, AnimationClip sprintStart, AnimationClip sprintLoop, AnimationClip sprintStop)
    {
        var sprintSM = parent.AddStateMachine("Sprint", new Vector3(280, 180, 0));

        var sIdle = sprintSM.AddState("Idle", new Vector3(280, 60, 0));
        sIdle.motion = idleDown;

        var sStart = sprintSM.AddState("Start", new Vector3(280, 160, 0));
        sStart.motion = sprintStart;

        var sLoop = sprintSM.AddState("Loop", new Vector3(280, 260, 0));
        sLoop.motion = sprintLoop;

        var sStop = sprintSM.AddState("Stop", new Vector3(280, 360, 0));
        sStop.motion = sprintStop;
        sStop.speed = 1.8f; // sprintStop clip 偏长，加速到 1.8x 让急停干脆

        sprintSM.defaultState = sIdle;

        // Idle → Start：Speed 越过阈值
        var i2s = sIdle.AddTransition(sStart);
        i2s.hasExitTime = false;
        i2s.duration = 0.05f;
        i2s.AddCondition(AnimatorConditionMode.Greater, 0.1f, ParamSpeed);

        // Start → Loop：播到末尾自动接 Loop
        var s2l = sStart.AddTransition(sLoop);
        s2l.hasExitTime = true;
        s2l.exitTime = 0.85f;
        s2l.duration = 0.1f;

        // Loop → Stop：Speed 掉到阈值下
        var l2s = sLoop.AddTransition(sStop);
        l2s.hasExitTime = false;
        l2s.duration = 0.05f;
        l2s.AddCondition(AnimatorConditionMode.Less, 0.1f, ParamSpeed);

        // 急加速：Stop 期间又踩动方向键，跳回 Start
        var stop2start = sStop.AddTransition(sStart);
        stop2start.hasExitTime = false;
        stop2start.duration = 0.05f;
        stop2start.AddCondition(AnimatorConditionMode.Greater, 0.1f, ParamSpeed);

        // Stop → Idle：播一半就接 Idle（配合 sStop.speed=1.8 总停步约 0.4s）
        var stop2idle = sStop.AddTransition(sIdle);
        stop2idle.hasExitTime = true;
        stop2idle.exitTime = 0.5f;
        stop2idle.duration = 0.1f;

        // 每个 sub-state 各自挂一条退出到 Walk 的转换（IsAiming=true 立刻退出 Sprint）
        // 用 per-state exits 而不是 sub-SM AnyState：跨 SM 的 AnyState 在某些 Unity 版本行为不稳
        AddAimExit(sIdle, walk);
        AddAimExit(sStart, walk);
        AddAimExit(sLoop, walk);
        AddAimExit(sStop, walk);

        return sprintSM;
    }

    private static void AddAimExit(AnimatorState src, AnimatorState walk)
    {
        var t = src.AddTransition(walk);
        t.hasExitTime = false;
        t.duration = 0.15f;
        t.AddCondition(AnimatorConditionMode.If, 0f, ParamIsAiming);
    }

    /// AnyState → dst：trigger 触发，可选附加条件，canTransitionToSelf=false。
    private static void AddAnyStateTransition(AnimatorStateMachine sm, AnimatorState dst, string trigger,
        (AnimatorConditionMode mode, float threshold, string param)? extraCond = null)
    {
        var t = sm.AddAnyStateTransition(dst);
        t.hasExitTime = false;
        t.duration = 0.1f;
        t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        if (extraCond.HasValue)
        {
            var e = extraCond.Value;
            t.AddCondition(e.mode, e.threshold, e.param);
        }
    }

    /// src 播完按 IsAiming 选择回 walk（state）或 sprint（sub-SM Entry → Idle）。
    private static void AddReturnToLocomotion(AnimatorState src, AnimatorState walk, AnimatorStateMachine sprintSM)
    {
        var toWalk = src.AddTransition(walk);
        toWalk.hasExitTime = true;
        toWalk.exitTime = 0.85f;
        toWalk.duration = 0.15f;
        toWalk.AddCondition(AnimatorConditionMode.If, 0f, ParamIsAiming);

        var toSprint = src.AddTransition(sprintSM);
        toSprint.hasExitTime = true;
        toSprint.exitTime = 0.85f;
        toSprint.duration = 0.15f;
        toSprint.AddCondition(AnimatorConditionMode.IfNot, 0f, ParamIsAiming);
    }

    private static void BuildRecoilLayer(AnimatorController controller, AnimationClip shootAdd, AvatarMask mask)
    {
        var sm = new AnimatorStateMachine
        {
            name = "Upper Body Recoil",
            hideFlags = HideFlags.HideInHierarchy,
        };
        AssetDatabase.AddObjectToAsset(sm, controller);

        var idle = sm.AddState("Idle", new Vector3(260, 120, 0));

        var recoil = sm.AddState("Recoil", new Vector3(460, 120, 0));
        recoil.motion = shootAdd;

        sm.defaultState = idle;

        var toRecoil = idle.AddTransition(recoil);
        toRecoil.hasExitTime = false;
        toRecoil.duration = 0.05f;
        toRecoil.AddCondition(AnimatorConditionMode.If, 0f, ParamIsShooting);

        var toIdle = recoil.AddTransition(idle);
        toIdle.hasExitTime = false;
        toIdle.duration = 0.15f;
        toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, ParamIsShooting);

        var layer = new AnimatorControllerLayer
        {
            name = "Upper Body Recoil",
            defaultWeight = 1f,
            blendingMode = AnimatorLayerBlendingMode.Additive,
            avatarMask = mask,
            stateMachine = sm,
        };
        controller.AddLayer(layer);
    }
}
