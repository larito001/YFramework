using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// 菜单：Tools/TPS/Build Player Animator Controller
/// 重新生成 Assets/Resources/Animations/playerController.controller：
///   Base Layer（全身）：
///     Walk   (2D BlendTree, MoveX/MoveY, 9 采样)        瞄准时，举枪 + 8 方向 strafe
///     Sprint (1D BlendTree, Speed)                       不瞄准时，Idle_GunDown ↔ SprintLoop
///     MeleeHard / MeleeKick                              近战，AnyState 触发
///     IsAiming 切换 Walk ↔ Sprint
///     MeleeAttack + MeleeType → AnyState → MeleeHard / MeleeKick
///   Upper Body Equip Layer：UpperBodyMask + Override，Idle 空 motion ↔ Equipping(EquipRifle)
///     切枪只动上半身，下半身继续走/跑
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
        // Sprint BlendTree clips
        var idleDown = Require(clips, "Rifle_Idle_GunDown");
        var sprintLoop = Require(clips, "Rifle_SprintLoop");
        // 其他
        var shootAdd = Require(clips, "Rifle_ShootLoop_Additive");
        var meleeHard = Require(clips, "Rifle_Melee_Hard");
        var meleeKick = Require(clips, "Rifle_Melee_Kick");
        var equip = Require(clips, "EquipRifle");
        if (idle == null || wFwd == null || wBwd == null || wLeft == null || wRight == null ||
            wFwdL == null || wFwdR == null || wBwdL == null || wBwdR == null ||
            idleDown == null || sprintLoop == null ||
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
            idleDown, sprintLoop, meleeHard, meleeKick);
        BuildEquipLayer(controller, equip, mask);
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
        AnimationClip idleDown, AnimationClip sprintLoop,
        AnimationClip meleeHard, AnimationClip meleeKick)
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

        // ── Sprint BlendTree（不瞄准时，1D，Idle_GunDown ↔ SprintLoop）──
        var sprintTree = new BlendTree
        {
            name = "SprintTree",
            blendType = BlendTreeType.Simple1D,
            blendParameter = ParamSpeed,
            hideFlags = HideFlags.HideInHierarchy,
        };
        AssetDatabase.AddObjectToAsset(sprintTree, controller);
        sprintTree.AddChild(idleDown, 0f);
        sprintTree.AddChild(sprintLoop, 1f);

        // ── 顶层 states ──
        var walk = sm.AddState("Walk", new Vector3(280, 60, 0));
        walk.motion = walkTree;

        var sprint = sm.AddState("Sprint", new Vector3(280, 180, 0));
        sprint.motion = sprintTree;

        var hard = sm.AddState("MeleeHard", new Vector3(560, 0, 0));
        hard.motion = meleeHard;

        var kick = sm.AddState("MeleeKick", new Vector3(560, 100, 0));
        kick.motion = meleeKick;

        sm.defaultState = sprint; // 出生不瞄准

        // Walk ↔ Sprint（IsAiming 切换）
        var w2s = walk.AddTransition(sprint);
        w2s.hasExitTime = false;
        w2s.duration = 0.15f;
        w2s.AddCondition(AnimatorConditionMode.IfNot, 0f, ParamIsAiming);

        var s2w = sprint.AddTransition(walk);
        s2w.hasExitTime = false;
        s2w.duration = 0.15f;
        s2w.AddCondition(AnimatorConditionMode.If, 0f, ParamIsAiming);

        // 近战：AnyState → MeleeHard/Kick，播完按 IsAiming 回 Walk 或 Sprint
        AddAnyStateTransition(sm, hard, ParamMeleeAttack,
            (AnimatorConditionMode.Equals, MeleeTypeHard, ParamMeleeType));
        AddAnyStateTransition(sm, kick, ParamMeleeAttack,
            (AnimatorConditionMode.Equals, MeleeTypeKick, ParamMeleeType));
        AddReturnToLocomotion(hard, walk, sprint);
        AddReturnToLocomotion(kick, walk, sprint);
    }

    /// 上半身切枪层（Override + UpperBodyMask）：Idle 空 motion 让下半身和 Base 的上半身原样透出，
    /// WeaponSwap trigger 切到 Equipping(EquipRifle)，播完回 Idle。下半身全程不受影响。
    private static void BuildEquipLayer(AnimatorController controller, AnimationClip equip, AvatarMask mask)
    {
        var sm = new AnimatorStateMachine
        {
            name = "Upper Body Equip",
            hideFlags = HideFlags.HideInHierarchy,
        };
        AssetDatabase.AddObjectToAsset(sm, controller);

        var idleState = sm.AddState("Idle", new Vector3(260, 120, 0));
        // motion=null：空 motion，Override 层下不动任何骨骼，Base 的上半身原样显示

        var equippingState = sm.AddState("Equipping", new Vector3(460, 120, 0));
        equippingState.motion = equip;

        sm.defaultState = idleState;

        // AnyState → Equipping on WeaponSwap trigger（canSelf=false，防止 trigger 期间自循环）
        var t = sm.AddAnyStateTransition(equippingState);
        t.hasExitTime = false;
        t.duration = 0.1f;
        t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If, 0f, ParamWeaponSwap);

        // Equipping → Idle exit time
        var back = equippingState.AddTransition(idleState);
        back.hasExitTime = true;
        back.exitTime = 0.85f;
        back.duration = 0.15f;

        var layer = new AnimatorControllerLayer
        {
            name = "Upper Body Equip",
            defaultWeight = 1f,
            blendingMode = AnimatorLayerBlendingMode.Override,
            avatarMask = mask,
            stateMachine = sm,
        };
        controller.AddLayer(layer);
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

    /// src 播完按 IsAiming 选择回 walk / sprint。
    private static void AddReturnToLocomotion(AnimatorState src, AnimatorState walk, AnimatorState sprint)
    {
        var toWalk = src.AddTransition(walk);
        toWalk.hasExitTime = true;
        toWalk.exitTime = 0.85f;
        toWalk.duration = 0.15f;
        toWalk.AddCondition(AnimatorConditionMode.If, 0f, ParamIsAiming);

        var toSprint = src.AddTransition(sprint);
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
