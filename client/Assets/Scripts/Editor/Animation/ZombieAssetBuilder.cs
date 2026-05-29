using System.Linq;
using Animancer;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 菜单：Tools/TPS/Build Skill &amp; Anim Assets
/// 一键生成所有技能 / 动画运行时资产（含 clip 引用 + 配置）：
///   僵尸（从 Assets/Art/Animations/Zombie 下 fbx）：
///   - Resources/Zombie/Zombie.prefab（MotusMan_v55 角色网格 = 僵尸动画的 Humanoid 骨架源 + AnimancerComponent + CharacterController + <see cref="ZombieView"/>）
///   - Resources/Zombie/ZombieAnimSet.asset（<see cref="CharacterAnimSet"/>）：idle / 慢走(Walk) / 快跑(Chase=Run) + 死亡 + 阈值
///   - Resources/Skill/ZombieAttack.asset（<see cref="SkillDef"/>）：Stand_To_Atk → Atk_Loop(命中窗扣血) → Atk_End，原地无位移
///   - Resources/Skill/ZombieLeap.asset（<see cref="SkillDef"/>）：Jump_Start → Jump_Air(前冲位移) → Jump_End(落地命中窗)
///   玩家（从 RifleAnimsetPro fbx）：
///   - Resources/Skill/PlayerMelee.asset（<see cref="SkillDef"/>）：单段 Rifle_Melee_Hard，前冲 + 命中窗（CharacterFactory 配 V 键释放）
/// 同时给 locomotion / Atk_Loop 这些循环 clip 的 fbx 导入设 loopTime=true，保证 mixer 能循环播。
///
/// 生成后 <see cref="CharacterFactory"/>（CreateCharacter / CreateZombie）即可按 Resources 路径加载这些资产跑起来。
/// 重新跑会覆盖旧资产（先删后建）。
/// </summary>
public static class ZombieAssetBuilder
{
    private const string Root = "Assets/Art/Animations/Zombie/";

    // ── 玩家近战 clip 所在的 RifleAnimsetPro fbx（多 take，按名查找）──
    private static readonly string[] RifleFbxPaths =
    {
        "Assets/Art/Animations/Human/RifleAnimsetPro.fbx",
        "Assets/Art/Animations/Human/RifleAnimsetPro_Additionals.fbx",
        "Assets/Art/Animations/Human/RifleAnimsetPro_Equips.fbx",
        "Assets/Art/Animations/Human/RifleAnimsetPro_Diagonals.fbx",
        "Assets/Art/Animations/Human/RifleAnimsetPro_Sprint.fbx",
    };

    // ── clip fbx 路径 ──
    private const string IdleFbx   = Root + "Idle/Zombie_Idle_1_v2_IPC.fbx";
    private const string WalkFbx   = Root + "Walk/Zombie_Walk_F_1_Loop_IPC.fbx";
    private const string RunFbx    = Root + "Chase/Zombie_Chase_1_Loop_IPC.fbx";
    private const string DeathLFbx = Root + "Death/Zombie_Death_Forward_2_IPC.fbx";
    private const string DeathRFbx = Root + "Death/Zombie_Death_Back_Mid_1_IPC.fbx";
    private const string AtkStartFbx = Root + "Attack/Zombie_Stand_To_Atk_1_IPC.fbx";
    private const string AtkLoopFbx  = Root + "Attack/Zombie_Atk_Loop_1_IPC.fbx";
    private const string AtkEndFbx   = Root + "Attack/Zombie_Atk_End_1_IPC.fbx";
    private const string JumpStartFbx = Root + "HyperChase/Zombie_HyperChase_4_Jump_Start_IPC.fbx";
    private const string JumpAirFbx   = Root + "HyperChase/Zombie_HyperChase_4_Jump_Air_IPC.fbx";
    private const string JumpEndFbx   = Root + "HyperChase/Zombie_HyperChase_4_Jump_End_HA1_IPC.fbx";

    [MenuItem("Tools/TPS/Build Skill & Anim Assets")]
    public static void Build()
    {
        // 清空 Inspector 选中：本流程会重导入 fbx（SetLoop）+ 临时实例化/销毁 GameObject（建 prefab），
        // 若此时某个 fbx / 物体正被 Inspector 检视，重导入会让它的 serializedObject 变 null，抛一串
        // GameObjectInspector/SkinnedMeshRendererEditor.OnEnable 空引用（无害但刷屏）。先清选中规避。
        Selection.objects = new Object[0];

        // 1. 循环 clip 设 loopTime（locomotion 要循环 blend；Atk_Loop 要能 hold 住循环）
        SetLoop(IdleFbx); SetLoop(WalkFbx); SetLoop(RunFbx); SetLoop(AtkLoopFbx);

        // 2. 加载 clip
        var idle = LoadClip(IdleFbx);
        var walk = LoadClip(WalkFbx);
        var run = LoadClip(RunFbx);
        var deathL = LoadClip(DeathLFbx);
        var deathR = LoadClip(DeathRFbx);
        var atkStart = LoadClip(AtkStartFbx);
        var atkLoop = LoadClip(AtkLoopFbx);
        var atkEnd = LoadClip(AtkEndFbx);
        var jumpStart = LoadClip(JumpStartFbx);
        var jumpAir = LoadClip(JumpAirFbx);
        var jumpEnd = LoadClip(JumpEndFbx);
        if (idle == null || walk == null || run == null)
        {
            Debug.LogError("[ZombieAssetBuilder] locomotion clip 缺失，中止。");
            return;
        }

        EnsureFolder("Assets/Resources/Zombie");
        EnsureFolder("Assets/Resources/Skill");

        // 3. CharacterAnimSet（locomotion + death）
        var animSet = ScriptableObject.CreateInstance<CharacterAnimSet>();
        animSet.Idle = idle;
        animSet.Walk = walk;     // 慢走
        animSet.Run = run;       // 快跑（Chase）
        animSet.Sprint = null;
        animSet.IdleThreshold = 0f;
        animSet.WalkThreshold = 1.5f;
        animSet.RunThreshold = 4f;
        animSet.SprintThreshold = 6f;
        animSet.DeathL = deathL;
        animSet.DeathR = deathR;
        animSet.UpperBodyMask = null; // 僵尸单层（攻击/技能全身覆盖）
        animSet.DefaultFade = 0.15f;
        CreateOrReplace(animSet, "Assets/Resources/Zombie/ZombieAnimSet.asset");

        // 4. 攻击技能：Stand_To_Atk → Atk_Loop(命中窗) → Atk_End，原地
        var attack = ScriptableObject.CreateInstance<SkillDef>();
        attack.Name = "ZombieAttack";
        attack.RecoverFade = 0.2f;
        attack.ShakeIntensity = 0.15f; attack.ShakeDuration = 0.12f;
        attack.Segments = new[]
        {
            Seg(atkStart, 0.1f, 0f, 0f, null),
            Seg(atkLoop, 0.05f, 0.7f, 0f, new[]
            {
                Hit(0.15f, 0.6f, 1.2f, 1.0f, 1.0f, 25f, HitstopTier.Long),
            }),
            Seg(atkEnd, 0.05f, 0f, 0f, null),
        };
        CreateOrReplace(attack, "Assets/Resources/Skill/ZombieAttack.asset");

        // 5. 飞扑技能：Jump_Start → Jump_Air(前冲) → Jump_End(落地范围伤害)
        var leap = ScriptableObject.CreateInstance<SkillDef>();
        leap.Name = "ZombieLeap";
        leap.RecoverFade = 0.25f;
        leap.ShakeIntensity = 0.3f; leap.ShakeDuration = 0.2f; // 落地重击大震
        leap.Segments = new[]
        {
            Seg(jumpStart, 0.1f, 0f, 0.5f, null),
            Seg(jumpAir, 0.0f, 0f, 4.5f, null),
            Seg(jumpEnd, 0.0f, 0f, 0f, new[]
            {
                Hit(0.0f, 0.4f, 1.8f, 1.0f, 1.0f, 40f, HitstopTier.Long),
            }),
        };
        CreateOrReplace(leap, "Assets/Resources/Skill/ZombieLeap.asset");

        // 6. 玩家近战技能（恢复 V 键近战）
        BuildPlayerMelee();

        // 7. 僵尸 prefab（从 Idle fbx 的内嵌蒙皮网格 + Humanoid Animator 建，挂 ZombieView/Animancer/CC）
        BuildZombiePrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ZombieAssetBuilder] 完成：ZombieAnimSet + ZombieAttack + ZombieLeap + PlayerMelee + Zombie.prefab 已生成到 Resources/。");
    }

    /// <summary>从 MotusMan_v55 角色网格（= 僵尸所有动画 fbx 共用的 Humanoid 骨架/Avatar 源）生成
    /// Resources/Zombie/Zombie.prefab，挂 AnimancerComponent + CharacterController + <see cref="ZombieView"/>。
    /// 僵尸动画原生绑在这套骨架上，retarget 完美；它和玩家用的模型不同，外观上是独立的敌人。
    ///
    /// **用 Object.Instantiate（深拷贝成普通 prefab）而非 InstantiatePrefab**——后者存出来是 variant，
    /// 加的组件不落盘（之前的坑：生成的 prefab 是个无组件的 MotusMan variant，没 CC → 掉地板下 + Inspector 报空引用）。</summary>
    private static void BuildZombiePrefab()
    {
        const string ModelFbx = "Assets/Art/Characters/MotusMan_v55.fbx";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFbx);
        if (model == null) { Debug.LogError($"[ZombieAssetBuilder] 找不到角色模型 fbx: {ModelFbx}"); return; }

        GameObject go = null;
        try
        {
            go = (GameObject)Object.Instantiate(model); // 深拷贝整套层级 → SaveAsPrefabAsset 出自包含 prefab
            go.name = "Zombie";

            // ⚠ 不能用 `GetComponent ?? AddComponent`：GetComponent 缺失时返回 Unity 的"假 null"，
            // C# 的 ?? 按真引用判空会**放过假 null** 不触发 AddComponent，拿到一个 missing 组件引用（访问即抛 MissingComponentException）。
            // 必须用 Unity 重载的 `== null` 显式判。

            // Humanoid 模型实例化后根上有 Animator + Avatar
            var animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.GetComponentInChildren<Animator>();
            if (animator == null) Debug.LogWarning("[ZombieAssetBuilder] 模型无 Animator —— 确认 fbx Rig 设为 Humanoid。");

            var animancer = go.GetComponent<AnimancerComponent>();
            if (animancer == null) animancer = go.AddComponent<AnimancerComponent>();
            if (animator != null) animancer.Animator = animator;

            // CharacterController：与 Player.prefab 同参（脚在 y=0，spawn 不掉地板下）。
            // 关键：prefab 必须自带 CC——否则 CharacterView.Awake 兜底 AddComponent 用 Unity 默认 center(0,0,0)，胶囊底在 y=-1，半身陷地。
            var cc = go.GetComponent<CharacterController>();
            if (cc == null) cc = go.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 1f, 0f);
            cc.height = 2f;
            cc.radius = 0.5f;

            if (go.GetComponent<ZombieView>() == null) go.AddComponent<ZombieView>();

            EnsureFolder("Assets/Resources/Zombie");
            var saved = PrefabUtility.SaveAsPrefabAsset(go, "Assets/Resources/Zombie/Zombie.prefab", out bool ok);
            if (!ok || saved == null) Debug.LogError("[ZombieAssetBuilder] 保存 Zombie.prefab 失败");
        }
        finally
        {
            if (go != null) Object.DestroyImmediate(go);
        }
    }

    /// <summary>玩家近战 = **Hard → Kick 两段连招**全身技能（一次 V 顺序播完，释放途中不可打断）：
    ///   段0 Rifle_Melee_Hard：前冲 1.2m + 命中窗 [0.25,0.55] 扣 30 伤；
    ///   段1 Rifle_Melee_Kick：前冲 1.0m + 命中窗 [0.20,0.50] 扣 25 伤。
    /// 命中窗去重按段独立 → 同一目标被两段各打一次（2-hit）。展示多段技能链。
    /// CharacterFactory.CreateCharacter 已把 SkillCastComponent.SkillPaths[0] 配为 "Skill/PlayerMelee" + V 键释放。</summary>
    private static void BuildPlayerMelee()
    {
        var hard = LoadNamed("Rifle_Melee_Hard", RifleFbxPaths);
        var kick = LoadNamed("Rifle_Melee_Kick", RifleFbxPaths);
        if (hard == null) { Debug.LogError("[ZombieAssetBuilder] PlayerMelee 跳过：找不到 Rifle_Melee_Hard"); return; }
        if (kick == null) Debug.LogWarning("[ZombieAssetBuilder] 找不到 Rifle_Melee_Kick，PlayerMelee 退化为单段 Hard");

        var melee = ScriptableObject.CreateInstance<SkillDef>();
        melee.Name = "PlayerMelee";
        melee.RecoverFade = 0.2f;
        melee.ShakeIntensity = 0.18f; melee.ShakeDuration = 0.15f; // 复刻旧 MeleeComponent 震屏
        melee.Segments = kick != null
            ? new[]
            {
                Seg(hard, 0.1f, 0f, 1.2f, new[] { Hit(0.25f, 0.55f, 1.0f, 0.8f, 1.0f, 30f, HitstopTier.Long) }),
                Seg(kick, 0.08f, 0f, 1.0f, new[] { Hit(0.20f, 0.50f, 1.0f, 0.8f, 1.0f, 25f, HitstopTier.Long) }),
            }
            : new[]
            {
                Seg(hard, 0.1f, 0f, 1.2f, new[] { Hit(0.25f, 0.55f, 1.0f, 0.8f, 1.0f, 30f, HitstopTier.Long) }),
            };
        CreateOrReplace(melee, "Assets/Resources/Skill/PlayerMelee.asset");
    }

    // ── helpers ──

    private static SkillDef.SkillSegment Seg(AnimationClip clip, float fade, float hold, float fwd, SkillDef.HitWindow[] windows)
        => new SkillDef.SkillSegment
        {
            Clip = clip,
            Fade = fade,
            HoldDuration = hold,
            ForwardDistance = fwd,
            DistanceProfile = null,
            HitWindows = windows,
        };

    private static SkillDef.HitWindow Hit(float startN, float endN, float radius, float fwdOff, float height, float dmg, HitstopTier tier)
        => new SkillDef.HitWindow
        {
            StartNorm = startN,
            EndNorm = endN,
            Radius = radius,
            ForwardOffset = fwdOff,
            Height = height,
            Damage = new DamageSpec { BaseDamage = dmg, HitstopTier = tier },
        };

    private static AnimationClip LoadClip(string fbxPath)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        if (all == null || all.Length == 0)
        {
            Debug.LogError($"[ZombieAssetBuilder] 找不到 FBX: {fbxPath}");
            return null;
        }
        var clip = all.OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (clip == null) Debug.LogError($"[ZombieAssetBuilder] FBX 内无 AnimationClip: {fbxPath}");
        return clip;
    }

    /// <summary>从多 take fbx 列表里按 clip 名查找（RifleAnimsetPro 一个 fbx 内含多个 take）。</summary>
    private static AnimationClip LoadNamed(string clipName, string[] fbxPaths)
    {
        foreach (var p in fbxPaths)
        {
            var all = AssetDatabase.LoadAllAssetsAtPath(p);
            if (all == null) continue;
            var c = all.OfType<AnimationClip>().FirstOrDefault(x => x.name == clipName);
            if (c != null) return c;
        }
        return null;
    }

    private static void SetLoop(string fbxPath)
    {
        var imp = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (imp == null) { Debug.LogWarning($"[ZombieAssetBuilder] 非 ModelImporter，跳过 loop 设置: {fbxPath}"); return; }
        var clips = imp.clipAnimations;
        if (clips == null || clips.Length == 0) clips = imp.defaultClipAnimations;
        if (clips == null || clips.Length == 0) return;
        bool changed = false;
        for (int i = 0; i < clips.Length; i++)
            if (!clips[i].loopTime) { clips[i].loopTime = true; changed = true; }
        if (changed)
        {
            imp.clipAnimations = clips;
            imp.SaveAndReimport();
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        string leaf = path.Substring(slash + 1);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    private static void CreateOrReplace(Object asset, string path)
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(asset, path);
    }
}
