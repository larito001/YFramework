using UnityEditor;
using UnityEngine;

/// <summary>
/// **一次性迁移工具**：把所有 <see cref="SkillDef"/> 每段的 <see cref="SkillDef.SkillSegment.Duration"/> 回填成
/// 旧式段时长（<c>HoldDuration > 0 ? HoldDuration : Clip.length</c>）。
///
/// 背景：SkillCastComponent 原先用 <c>seg.Clip.length</c> 算段时长——这让逻辑层读 AnimationClip。
/// 解耦后运行时改用显式 <see cref="SkillDef.SkillSegment.Duration"/>，clip 提取移到 view 层。本工具保证
/// 现有技能资产在切换前就把 Duration 填成与原 clip.length 一致的值，**零时序变化、零破坏**。
///
/// 用法：菜单 <c>Tools/TPS/Backfill SkillDef Durations</c>，跑一次即可（幂等：已填非 0 的段不覆盖）。
/// </summary>
public static class SkillDefDurationBackfill
{
    [MenuItem("Tools/TPS/Backfill SkillDef Durations")]
    public static void Backfill()
    {
        var guids = AssetDatabase.FindAssets("t:SkillDef");
        int assetCount = 0, segFilled = 0, segSkippedNoSource = 0, segAlreadySet = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var def = AssetDatabase.LoadAssetAtPath<SkillDef>(path);
            if (def == null || def.Segments == null) continue;

            bool dirty = false;
            for (int i = 0; i < def.Segments.Length; i++)
            {
                var seg = def.Segments[i];
                if (seg == null) continue;

                if (seg.Duration > 0f) { segAlreadySet++; continue; } // 幂等：已手填/已回填的不动

                float src = seg.HoldDuration > 0f ? seg.HoldDuration
                          : (seg.Clip != null ? seg.Clip.length : 0f);
                if (src <= 0f)
                {
                    // 无 clip 且无 HoldDuration —— 原本就是退化段（运行时会被跳过），无可回填来源
                    Debug.LogWarning($"[Backfill] {def.name} 第 {i} 段无 Clip 且 HoldDuration<=0，Duration 留 0（仍是退化段）。", def);
                    segSkippedNoSource++;
                    continue;
                }

                seg.Duration = src;
                segFilled++;
                dirty = true;
            }

            if (dirty)
            {
                EditorUtility.SetDirty(def);
                assetCount++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Backfill SkillDef Durations] 完成：{assetCount} 个资产更新，{segFilled} 段回填，{segAlreadySet} 段已有值跳过，{segSkippedNoSource} 段无来源（退化段）。");
    }
}
