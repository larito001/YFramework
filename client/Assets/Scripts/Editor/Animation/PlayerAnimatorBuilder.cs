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
///   Upper Body Recoil Layer：UpperBodyMask + Override，Shoot trigger 触发单次 ShootOnce/ShootGrenade（HeavyRecoil 选），
///     RecoilSpeed 驱动 state.speed 让单发在 FireInterval 内播完，canSelf=true 让连发每发重播
///   Upper Body Reload Layer：UpperBodyMask + Override，Reload trigger 切到 Reloading，IsReloading=false 回 Idle
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
    private const string ParamReload = "Reload";
    private const string ParamIsReloading = "IsReloading";
    private const string ParamShoot = "Shoot";
    private const string ParamHeavyRecoil = "HeavyRecoil";
    private const string ParamRecoilSpeed = "RecoilSpeed";

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
        var shootLight = Require(clips, "Rifle_ShootOnce");      // 单次轻后坐力，全自动 / 半自动通用
        var shootHeavy = Require(clips, "Rifle_ShootGrenade");   // 单次重后坐力，导弹/榴弹之类
        var meleeHard = Require(clips, "Rifle_Melee_Hard");
        var meleeKick = Require(clips, "Rifle_Melee_Kick");
        var equip = Require(clips, "EquipRifle");
        var reload = Require(clips, "Rifle_Reload_2");
        if (idle == null || wFwd == null || wBwd == null || wLeft == null || wRight == null ||
            wFwdL == null || wFwdR == null || wBwdL == null || wBwdR == null ||
            idleDown == null || sprintLoop == null ||
            shootLight == null || shootHeavy == null || meleeHard == null || meleeKick == null || equip == null || reload == null)
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
        controller.AddParameter(ParamReload, AnimatorControllerParameterType.Trigger);
        controller.AddParameter(ParamIsReloading, AnimatorControllerParameterType.Bool);
        controller.AddParameter(ParamShoot, AnimatorControllerParameterType.Trigger);
        controller.AddParameter(ParamHeavyRecoil, AnimatorControllerParameterType.Bool);
        // RecoilSpeed 默认 1：未由 view 写过时 Recoil 状态以原始速度播放，不卡帧
        var recoilSpeedParam = new AnimatorControllerParameter
        {
            name = ParamRecoilSpeed,
            type = AnimatorControllerParameterType.Float,
            defaultFloat = 1f,
        };
        controller.AddParameter(recoilSpeedParam);

        BuildBaseLayer(controller, idle, wFwd, wBwd, wLeft, wRight, wFwdL, wFwdR, wBwdL, wBwdR,
            idleDown, sprintLoop, meleeHard, meleeKick);
        BuildEquipLayer(controller, equip, mask);
        BuildRecoilLayer(controller, shootLight, shootHeavy, mask);
        BuildReloadLayer(controller, reload, mask);

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

        // ── Sprint BlendTree（不瞄准时，1D）──
        // 2 采样点：0=Idle_GunDown，1=SprintLoop。Speed 参数范围 [0,1]
        // 反滑步靠 view 端 Anim.speed = horiz / ReferenceMoveSpeed 全局缩放，不用 timeScale tricks
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
        walk.tag = "Locomotion"; // view 用此 tag 决定是否 Anim.speed 缩放

        var sprint = sm.AddState("Sprint", new Vector3(280, 180, 0));
        sprint.motion = sprintTree;
        sprint.tag = "Locomotion";

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

    /// 上半身换弹层（Override + UpperBodyMask）：Idle 空 motion 让 Base/Equip 的上半身原样透出，
    /// Reload trigger 切到 Reloading(Rifle_Reload_2)，等 IsReloading=false 回 Idle。
    /// 用 Bool 退出（而非 exit time）：和 WeaponComponent 的 reloadTimer 状态同步，换枪打断时动画立刻回 Idle。
    /// 下半身全程不受影响。
    private static void BuildReloadLayer(AnimatorController controller, AnimationClip reload, AvatarMask mask)
    {
        var sm = new AnimatorStateMachine
        {
            name = "Upper Body Reload",
            hideFlags = HideFlags.HideInHierarchy,
        };
        AssetDatabase.AddObjectToAsset(sm, controller);

        var idleState = sm.AddState("Idle", new Vector3(260, 120, 0));
        // motion=null：空 motion，Override 层下不动任何骨骼

        var reloadingState = sm.AddState("Reloading", new Vector3(460, 120, 0));
        reloadingState.motion = reload;

        sm.defaultState = idleState;

        // AnyState → Reloading on Reload trigger（canSelf=false 防 trigger 期间自循环）
        var t = sm.AddAnyStateTransition(reloadingState);
        t.hasExitTime = false;
        t.duration = 0.1f;
        t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If, 0f, ParamReload);

        // Reloading → Idle when IsReloading=false（由 WeaponComponent 计时器到点或换枪打断清零）
        var back = reloadingState.AddTransition(idleState);
        back.hasExitTime = false;
        back.duration = 0.15f;
        back.AddCondition(AnimatorConditionMode.IfNot, 0f, ParamIsReloading);

        var layer = new AnimatorControllerLayer
        {
            name = "Upper Body Reload",
            defaultWeight = 1f,
            blendingMode = AnimatorLayerBlendingMode.Override,
            avatarMask = mask,
            stateMachine = sm,
        };
        controller.AddLayer(layer);
    }

    /// 上半身后坐力层（Override + UpperBodyMask）：每次开火 FireComponent 发 Shoot trigger，
    /// 由 HeavyRecoil 选择 ShootLight(Rifle_ShootOnce) / ShootHeavy(Rifle_ShootGrenade)。
    /// 用 Override 而非 Additive：ShootOnce/ShootGrenade 是绝对姿势（不是 _Additive 那种 delta clip），
    /// 走 Additive 会把整段动画 delta 叠到 Base 上，姿势会扭曲。Override + 上半身 Mask 直接把上半身切到 recoil 姿势，
    /// Idle 状态 motion=null 时不 override，Base 的瞄准姿势透出。
    /// 状态用 RecoilSpeed Float 驱动 speed，让单次动画在 FireInterval 内播完。
    /// canTransitionToSelf=true 让连续开火每发都从头重播动画（节奏跟随实际射速）。
    private static void BuildRecoilLayer(AnimatorController controller,
        AnimationClip shootLight, AnimationClip shootHeavy, AvatarMask mask)
    {
        var sm = new AnimatorStateMachine
        {
            name = "Upper Body Recoil",
            hideFlags = HideFlags.HideInHierarchy,
        };
        AssetDatabase.AddObjectToAsset(sm, controller);

        var idle = sm.AddState("Idle", new Vector3(260, 120, 0));
        // motion=null：Additive 层下空 motion 不叠加任何骨骼偏移

        var light = sm.AddState("ShootLight", new Vector3(460, 60, 0));
        light.motion = shootLight;
        light.speedParameterActive = true;
        light.speedParameter = ParamRecoilSpeed;

        var heavy = sm.AddState("ShootHeavy", new Vector3(460, 180, 0));
        heavy.motion = shootHeavy;
        heavy.speedParameterActive = true;
        heavy.speedParameter = ParamRecoilSpeed;

        sm.defaultState = idle;

        // AnyState → ShootLight：Shoot trigger + HeavyRecoil=false，canSelf=true 让每发重播
        var toLight = sm.AddAnyStateTransition(light);
        toLight.hasExitTime = false;
        toLight.duration = 0.02f;
        toLight.canTransitionToSelf = true;
        toLight.AddCondition(AnimatorConditionMode.If, 0f, ParamShoot);
        toLight.AddCondition(AnimatorConditionMode.IfNot, 0f, ParamHeavyRecoil);

        // AnyState → ShootHeavy：Shoot trigger + HeavyRecoil=true，canSelf=true
        var toHeavy = sm.AddAnyStateTransition(heavy);
        toHeavy.hasExitTime = false;
        toHeavy.duration = 0.02f;
        toHeavy.canTransitionToSelf = true;
        toHeavy.AddCondition(AnimatorConditionMode.If, 0f, ParamShoot);
        toHeavy.AddCondition(AnimatorConditionMode.If, 0f, ParamHeavyRecoil);

        // 单次动画播完回 Idle（用 exit time，因为 ShootEvent 已经是脉冲，没有 IsShooting 持续态可读）
        var lightToIdle = light.AddTransition(idle);
        lightToIdle.hasExitTime = true;
        lightToIdle.exitTime = 0.9f;
        lightToIdle.duration = 0.1f;

        var heavyToIdle = heavy.AddTransition(idle);
        heavyToIdle.hasExitTime = true;
        heavyToIdle.exitTime = 0.9f;
        heavyToIdle.duration = 0.1f;

        var layer = new AnimatorControllerLayer
        {
            name = "Upper Body Recoil",
            defaultWeight = 1f,
            blendingMode = AnimatorLayerBlendingMode.Override,
            avatarMask = mask,
            stateMachine = sm,
        };
        controller.AddLayer(layer);
    }
}
