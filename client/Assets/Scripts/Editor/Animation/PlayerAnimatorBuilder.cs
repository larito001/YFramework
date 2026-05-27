using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// 菜单：Tools/TPS/Build Player Animator Controller
/// 重新生成 Assets/Art/Animations/playerController.controller：
///   Base Layer（全身）：Locomotion BlendTree + 近战 AnyState 触发
///     BlendTree: 2D Freeform Cartesian，参数 MoveX / MoveY，9 采样点
///     AnyState → MeleeHard / MeleeKick: MeleeAttack 触发 + MeleeType 选型，播完回 Locomotion
///   Upper Body Recoil Layer：UpperBodyMask + Additive，IsShooting=true 切到 ShootLoop_Additive
///     Idle 状态空 motion；Recoil 状态叠加后坐力偏移
/// 多武器扩展：本 controller 状态命名武器无关，用 AnimatorOverrideController 换 clip 即可。
/// 重跑会删除旧 controller 重建（meta guid 会变）。
public static class PlayerAnimatorBuilder
{
    private const string ControllerPath = "Assets/Art/Animations/playerController.controller";
    private const string RifleFbxPath = "Assets/Art/Animations/RifleAnimsetPro.fbx";
    private const string DiagonalsFbxPath = "Assets/Art/Animations/RifleAnimsetPro_Diagonals.fbx";
    private const string UpperBodyMaskPath = "Assets/Art/Characters/UpperBodyMask.mask";

    private const string ParamMoveX = "MoveX";
    private const string ParamMoveY = "MoveY";
    private const string ParamIsShooting = "IsShooting";
    private const string ParamMeleeAttack = "MeleeAttack";
    private const string ParamMeleeType = "MeleeType";

    private const int MeleeTypeHard = 0;
    private const int MeleeTypeKick = 1;

    // 45° 方向归一化分量
    private const float Diag = 0.70710677f;

    [MenuItem("Tools/TPS/Build Player Animator Controller")]
    public static void Build()
    {
        var clips = new Dictionary<string, AnimationClip>();
        if (!LoadClipsInto(clips, RifleFbxPath)) return;
        if (!LoadClipsInto(clips, DiagonalsFbxPath)) return;

        var idle = Require(clips, "Rifle_Idle");
        var fwd = Require(clips, "Rifle_WalkFwdLoop");
        var bwd = Require(clips, "Rifle_WalkBwdLoop");
        var left = Require(clips, "Rifle_StrafeLeftLoop");
        var right = Require(clips, "Rifle_StrafeRightLoop");
        var fwdL = Require(clips, "Rifle_StrafeLeft45Loop");
        var fwdR = Require(clips, "Rifle_StrafeRight45Loop");
        var bwdL = Require(clips, "Rifle_StrafeLeft135Loop");
        var bwdR = Require(clips, "Rifle_StrafeRight135Loop");
        var shootAdd = Require(clips, "Rifle_ShootLoop_Additive");
        var meleeHard = Require(clips, "Rifle_Melee_Hard");
        var meleeKick = Require(clips, "Rifle_Melee_Kick");
        if (idle == null || fwd == null || bwd == null || left == null || right == null ||
            fwdL == null || fwdR == null || bwdL == null || bwdR == null || shootAdd == null ||
            meleeHard == null || meleeKick == null)
            return;

        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
        if (mask == null)
        {
            Debug.LogError($"[PlayerAnimator] 找不到上半身遮罩 {UpperBodyMaskPath}");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            AssetDatabase.DeleteAsset(ControllerPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter(ParamMoveX, AnimatorControllerParameterType.Float);
        controller.AddParameter(ParamMoveY, AnimatorControllerParameterType.Float);
        controller.AddParameter(ParamIsShooting, AnimatorControllerParameterType.Bool);
        controller.AddParameter(ParamMeleeAttack, AnimatorControllerParameterType.Trigger);
        controller.AddParameter(ParamMeleeType, AnimatorControllerParameterType.Int);

        BuildBaseLayer(controller, idle, fwd, bwd, left, right, fwdL, fwdR, bwdL, bwdR,
            meleeHard, meleeKick);
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
            // FBX 导入会附带名为 __preview__XXX 的临时 clip，跳过
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

    private static void BuildBaseLayer(AnimatorController controller, AnimationClip idle,
        AnimationClip fwd, AnimationClip bwd, AnimationClip left, AnimationClip right,
        AnimationClip fwdL, AnimationClip fwdR, AnimationClip bwdL, AnimationClip bwdR,
        AnimationClip meleeHard, AnimationClip meleeKick)
    {
        // CreateAnimatorControllerAtPath 已建好 Base Layer 和它的 StateMachine 子资源
        var sm = controller.layers[0].stateMachine;

        var tree = new BlendTree
        {
            name = "Locomotion",
            blendType = BlendTreeType.FreeformCartesian2D,
            blendParameter = ParamMoveX,
            blendParameterY = ParamMoveY,
            hideFlags = HideFlags.HideInHierarchy,
        };
        AssetDatabase.AddObjectToAsset(tree, controller);

        tree.AddChild(idle, new Vector2(0f, 0f));
        tree.AddChild(fwd, new Vector2(0f, 1f));
        tree.AddChild(bwd, new Vector2(0f, -1f));
        tree.AddChild(left, new Vector2(-1f, 0f));
        tree.AddChild(right, new Vector2(1f, 0f));
        tree.AddChild(fwdL, new Vector2(-Diag, Diag));
        tree.AddChild(fwdR, new Vector2(Diag, Diag));
        tree.AddChild(bwdL, new Vector2(-Diag, -Diag));
        tree.AddChild(bwdR, new Vector2(Diag, -Diag));

        var loco = sm.AddState("Locomotion", new Vector3(260, 120, 0));
        loco.motion = tree;
        sm.defaultState = loco;

        var hard = sm.AddState("MeleeHard", new Vector3(560, 60, 0));
        hard.motion = meleeHard;

        var kick = sm.AddState("MeleeKick", new Vector3(560, 180, 0));
        kick.motion = meleeKick;

        // AnyState 触发近战：MeleeAttack trigger + MeleeType 选型
        AddMeleeAnyTransition(sm, hard, MeleeTypeHard);
        AddMeleeAnyTransition(sm, kick, MeleeTypeKick);

        // 近战播完回 Locomotion
        AddMeleeExitTransition(hard, loco);
        AddMeleeExitTransition(kick, loco);
    }

    private static void AddMeleeAnyTransition(AnimatorStateMachine sm, AnimatorState dst, int meleeType)
    {
        var t = sm.AddAnyStateTransition(dst);
        t.hasExitTime = false;
        t.duration = 0.1f;
        t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If, 0f, ParamMeleeAttack);
        t.AddCondition(AnimatorConditionMode.Equals, meleeType, ParamMeleeType);
    }

    private static void AddMeleeExitTransition(AnimatorState src, AnimatorState dst)
    {
        var t = src.AddTransition(dst);
        t.hasExitTime = true;
        t.exitTime = 0.9f;
        t.duration = 0.15f;
    }

    private static void BuildRecoilLayer(AnimatorController controller, AnimationClip shootAdd, AvatarMask mask)
    {
        var sm = new AnimatorStateMachine
        {
            name = "Upper Body Recoil",
            hideFlags = HideFlags.HideInHierarchy,
        };
        AssetDatabase.AddObjectToAsset(sm, controller);

        // 空 motion 状态：Additive 层不施加任何偏移，让 Base Layer 的上半身原样呈现
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
