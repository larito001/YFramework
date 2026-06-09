using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 菜单：Tools/TPS/Build Combo Demo (Knife)
/// 一键生成一套**可玩的近战连招 demo**，落盘到 Resources/Character/Skills/，并配合 knife 的 ComboGraphPath 跑起来：
///   - Knife_Atk1 / Atk2 / Atk3.asset（<see cref="SkillDef"/>，各单段轻击，CancelFromNorm 配在命中后→取消窗可接）
///   - Knife_Heavy.asset（<see cref="SkillDef"/>，重击 / 收尾）
///   - KnifeCombo.asset（<see cref="ComboGraph"/>）：
///       [0]Neutral (Light→Atk1, Heavy→Heavy)
///       [1]Atk1   (Light→Atk2)
///       [2]Atk2   (Light→Atk3, Heavy→Heavy)   // 轻轻轻 = 三连；轻轻重 = 重击收尾
///       [3]Atk3   末段
///       [4]Heavy  末段
/// 跑完进游戏切到 knife（数字键 5）：左键连点 = 三连段，左左 V = 重击收尾，单 V = 重击。
///
/// **clip 来源**：直接从现有 PlayerMelee / PlayerKnife / PlayerKnifeV 资产里收集（GUID 引用，与 fbx 文件位置无关）——
/// 比按 fbx 路径查健壮（fbx 可能被移动 / 重命名）。重跑覆盖旧资产（先删后建）。
/// 注：连招招式**不进** SkillCastComponent.SkillPaths——ComboGraph 直接引用 SkillDef，由 ComboComponent 调 Cast(SkillDef)。
/// </summary>
public static class ComboAssetBuilder
{
    // 复用来源：从这些现有近战 SkillDef 里抽 clip 引用（按顺序去重收集）
    private static readonly string[] ClipSourceSkills =
    {
        "Assets/Resources/Character/Skills/PlayerMelee.asset",
        "Assets/Resources/Character/Skills/PlayerKnife.asset",
        "Assets/Resources/Character/Skills/PlayerKnifeV.asset",
    };

    [MenuItem("Tools/TPS/Build Combo Demo (Knife)")]
    public static void Build()
    {
        Selection.objects = new Object[0];

        var clips = CollectClips(ClipSourceSkills);
        if (clips.Count == 0)
        {
            Debug.LogError("[ComboAssetBuilder] 收集不到任何近战 clip —— 确认 PlayerMelee / PlayerKnife / PlayerKnifeV.asset 存在（先跑 Tools/TPS/Build Skill & Anim Assets）。中止。");
            return;
        }
        // clip 不足时夹取复用：哪怕只有 1 个 clip 也能出 demo（各段复用同一 clip，连招逻辑照样可验）
        AnimationClip Clip(int i) => clips[Mathf.Min(i, clips.Count - 1)];

        EnsureFolder("Assets/Resources/Character/Skills");

        // 每招单段。CancelFromNorm 都 >= 命中窗 EndNorm（命中先打完，取消窗才开 → 连招接得上又不吃命中）。
        var atk1  = MakeSkill("Knife_Atk1",  Clip(0), fwd: 1.0f, hitStart: 0.20f, hitEnd: 0.45f, dmg: 20f, tier: HitstopTier.Short, cancelFrom: 0.50f, recoverFade: 0.18f);
        var atk2  = MakeSkill("Knife_Atk2",  Clip(1), fwd: 1.0f, hitStart: 0.20f, hitEnd: 0.45f, dmg: 25f, tier: HitstopTier.Short, cancelFrom: 0.50f, recoverFade: 0.18f);
        var atk3  = MakeSkill("Knife_Atk3",  Clip(2), fwd: 1.4f, hitStart: 0.25f, hitEnd: 0.50f, dmg: 45f, tier: HitstopTier.Long,  cancelFrom: 0.65f, recoverFade: 0.25f); // 收尾：更前冲 + 高伤 + 稍长后摇
        var heavy = MakeSkill("Knife_Heavy", Clip(3), fwd: 0.8f, hitStart: 0.20f, hitEnd: 0.50f, dmg: 50f, tier: HitstopTier.Long,  cancelFrom: 0.60f, recoverFade: 0.25f);
        CreateOrReplace(atk1,  "Assets/Resources/Character/Skills/Knife_Atk1.asset");
        CreateOrReplace(atk2,  "Assets/Resources/Character/Skills/Knife_Atk2.asset");
        CreateOrReplace(atk3,  "Assets/Resources/Character/Skills/Knife_Atk3.asset");
        CreateOrReplace(heavy, "Assets/Resources/Character/Skills/Knife_Heavy.asset");

        // 招式图：节点引用上面刚落盘的 SkillDef（已是持久资产，引用可序列化）
        var graph = ScriptableObject.CreateInstance<ComboGraph>();
        graph.Nodes = new[]
        {
            Node("Neutral", null,  Link(ComboButton.Light, 1), Link(ComboButton.Heavy, 4)),
            Node("Atk1",    atk1,  Link(ComboButton.Light, 2)),
            Node("Atk2",    atk2,  Link(ComboButton.Light, 3), Link(ComboButton.Heavy, 4)),
            Node("Atk3",    atk3),
            Node("Heavy",   heavy),
        };
        CreateOrReplace(graph, "Assets/Resources/Character/Skills/KnifeCombo.asset");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ComboAssetBuilder] 完成（复用 {clips.Count} 个 clip）：Knife_Atk1/2/3 + Knife_Heavy + KnifeCombo 已生成到 Resources/Character/Skills/。\n" +
                  "切到 knife（数字键 5）：左键连点=三连段，左左V=重击收尾，单V=重击。knife 的 ComboGraphPath 已指向 Character/Skills/KnifeCombo。");
    }

    // ── helpers ──

    /// <summary>从一组现有 SkillDef 资产里按顺序收集去重后的 clip 引用。</summary>
    private static List<AnimationClip> CollectClips(string[] skillAssetPaths)
    {
        var list = new List<AnimationClip>();
        foreach (var p in skillAssetPaths)
        {
            var def = AssetDatabase.LoadAssetAtPath<SkillDef>(p);
            if (def == null || def.Segments == null) continue;
            foreach (var seg in def.Segments)
                if (seg != null && seg.Clip != null && !list.Contains(seg.Clip)) list.Add(seg.Clip);
        }
        return list;
    }

    private static SkillDef MakeSkill(string name, AnimationClip clip, float fwd, float hitStart, float hitEnd,
                                      float dmg, HitstopTier tier, float cancelFrom, float recoverFade)
    {
        var s = ScriptableObject.CreateInstance<SkillDef>();
        s.Name = name;
        s.RecoverFade = recoverFade;
        s.Segments = new[]
        {
            new SkillDef.SkillSegment
            {
                Clip = clip,
                Fade = 0.08f,
                HoldDuration = 0f,
                ForwardDistance = fwd,
                DistanceProfile = null,
                CancelFromNorm = cancelFrom,
                HitWindows = new[]
                {
                    new SkillDef.HitWindow
                    {
                        StartNorm = hitStart, EndNorm = hitEnd,
                        Radius = 1.0f, ForwardOffset = 0.8f, Height = 1.0f,
                        Damage = new DamageSpec { BaseDamage = dmg, HitstopTier = tier },
                    },
                },
                Shake = new[] { new SkillDef.SkillShake { StartNorm = hitStart, Intensity = 0.16f, Duration = 0.14f } },
            },
        };
        return s;
    }

    private static ComboGraph.ComboNode Node(string name, SkillDef skill, params ComboGraph.ComboLink[] links)
        => new ComboGraph.ComboNode { Name = name, Skill = skill, Links = links };

    private static ComboGraph.ComboLink Link(ComboButton btn, int target, ComboDir dir = ComboDir.Any)
        => new ComboGraph.ComboLink { Button = btn, Direction = dir, TargetNode = target };

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
