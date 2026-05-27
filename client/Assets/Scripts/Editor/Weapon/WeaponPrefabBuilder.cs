using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 菜单：Tools/TPS/Build Weapon Prefabs
/// 把 Art/Weapon/ 下的武器 FBX 包成 Resources/Weapon/*.prefab，让运行时 Resources.Load 能加载。
/// 重跑会覆盖已有的 prefab（guid 保留）。Art/Weapon/ 下的 FBX 原文件不动。
/// 新增武器：在 FbxAssetPaths 加一行 FBX 路径即可。
/// </summary>
public static class WeaponPrefabBuilder
{
    private const string ResourceFolder = "Assets/Resources";
    private const string OutSubfolder = "Weapon";

    private static readonly string[] FbxAssetPaths =
    {
        "Assets/Art/Weapon/RiflePlaceholder.FBX",
        "Assets/Art/Weapon/PistolPlaceholder.FBX",
    };

    [MenuItem("Tools/TPS/Build Weapon Prefabs")]
    public static void Build()
    {
        EnsureFolder(ResourceFolder, "Assets", "Resources");
        EnsureFolder($"{ResourceFolder}/{OutSubfolder}", ResourceFolder, OutSubfolder);

        int ok = 0, fail = 0;
        foreach (var fbxPath in FbxAssetPaths)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null)
            {
                Debug.LogError($"[WeaponPrefab] 找不到 FBX: {fbxPath}");
                fail++;
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(fbxPath);
            var prefabPath = $"{ResourceFolder}/{OutSubfolder}/{name}.prefab";

            // PrefabUtility.SaveAsPrefabAsset 会自动嵌入 FBX 引用，保留 mesh / 材质 / 子层级
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            try
            {
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath, out bool success);
                if (success)
                {
                    Debug.Log($"[WeaponPrefab] 生成 {prefabPath}");
                    ok++;
                }
                else
                {
                    Debug.LogError($"[WeaponPrefab] 保存失败: {prefabPath}");
                    fail++;
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[WeaponPrefab] 完成: 成功 {ok}, 失败 {fail}");
    }

    private static void EnsureFolder(string fullPath, string parent, string leaf)
    {
        if (AssetDatabase.IsValidFolder(fullPath)) return;
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
