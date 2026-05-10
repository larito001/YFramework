#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

internal static class AllPrefabBuilders
{
    [MenuItem("YFramework/Build Prefabs/[All]")]
    public static void BuildAll()
    {
        Debug.Log("[PrefabBuilder] BuildAll 开始");
        // ↓↓↓ 自动维护区域：本 skill 重新执行时仅重写此区域 ↓↓↓
        BagDropConfirmPanelPrefabBuilder.Build();
        BagPanelPrefabBuilder.Build();
        // ↑↑↑ 自动维护区域结束 ↑↑↑
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PrefabBuilder] BuildAll 完成");
    }
}
#endif
