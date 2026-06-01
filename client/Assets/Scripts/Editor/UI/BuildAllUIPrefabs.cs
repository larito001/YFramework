#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 一键重建所有 UI 预制体。各面板预制体里的字号由各 *Builder 烘焙,改了 <see cref="UITheme.FontScale"/>(或任何 Builder 代码)后,
/// 已生成的 .prefab 不会自动更新,需重跑对应 <c>Tools/UI/Build *** Prefab</c>。本菜单把它们串起来一次跑完,省去逐个点。
/// 单个 Builder 抛异常不会中断整批:记录失败项,最后汇总并统一刷新资源库。
/// </summary>
public static class BuildAllUIPrefabs
{
    /// <summary>(名字, 构建委托) 清单——新增面板时在这里补一行即可纳入一键重建。</summary>
    private static readonly (string name, Action build)[] Builders =
    {
        ("CodexPanel",     CodexPanelBuilder.Build),
        ("ConfirmPanel",   ConfirmPanelBuilder.Build),
        ("EquipPanel",     EquipPanelBuilder.Build),
        ("FinishPanel",    FinishPanelBuilder.Build),
        ("GameMainPanel",  GameMainPanelBuilder.Build),
        ("LoadingPanel",   LoadingPanelBuilder.Build),
        ("MapSelectPanel", MapSelectPanelBuilder.Build),
        ("RewardClaimPanel", RewardClaimPanelBuilder.Build),
        ("SaveSlotPanel",  SaveSlotPanelBuilder.Build),
        ("SettingPanel",   SettingPanelBuilder.Build),
        ("ShopPanel",      ShopPanelBuilder.Build),
        ("StartPanel",     StartPanelBuilder.Build),
        ("TaskPanel",      TaskPanelBuilder.Build),
        ("Bag UI",         BagPrefabBuilder.BuildAll),
    };

    [MenuItem("Tools/UI/Build ALL UI Prefabs", priority = 0)]
    public static void BuildAll()
    {
        var failed = new List<string>();
        int total = Builders.Length;

        try
        {
            for (int i = 0; i < total; i++)
            {
                var (name, build) = Builders[i];
                EditorUtility.DisplayProgressBar("Build ALL UI Prefabs",
                    $"({i + 1}/{total}) {name}", (float)i / total);
                try
                {
                    build();
                    Debug.Log($"[BuildAllUIPrefabs] [OK] {name}");
                }
                catch (Exception e)
                {
                    failed.Add(name);
                    Debug.LogError($"[BuildAllUIPrefabs] [FAIL] {name} 构建失败:{e}");
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (failed.Count == 0)
            Debug.Log($"[BuildAllUIPrefabs] 全部完成,共 {total} 个 UI 预制体已重建。");
        else
            Debug.LogError($"[BuildAllUIPrefabs] 完成 {total - failed.Count}/{total},失败:{string.Join(", ", failed)}");
    }
}
#endif
