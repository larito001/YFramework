using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 菜单：Tools/Bag/Build Item Drop Prefabs
/// 生成掉落物占位 box 预制体到 Resources/Item/Prefabs/DropBox_&lt;品质&gt;.prefab（5 个品质各一个，按品质上色）：
/// 立方体 mesh + BoxCollider + MeshRenderer（挂 Art/Item/DropBox_&lt;品质&gt;.mat，URP/Lit，**GUID 引用**——
/// 材质本体留在 Art 下、不进 Resources）。<see cref="DropItemSystem"/> 丢弃时按物品品质 Resources.Load
/// 对应预制体并实例化（再按配表 Width/Height 缩放）。
/// 接真实模型：把对应 prefab 换成真模型即可；改颜色：调 Art/Item 下的 .mat（或改 ItemQualityPalette 后重跑）。
/// 重跑覆盖（guid 保留）。
/// </summary>
public static class ItemDropPrefabBuilder
{
    private const string MatFolder = "Assets/Art/Item";
    private const string PrefabFolder = "Assets/Resources/Item/Prefabs";

    [MenuItem("Tools/Bag/Build Item Drop Prefabs")]
    public static void Build()
    {
        EnsureFolder(MatFolder);
        EnsureFolder(PrefabFolder);

        var shader = Shader.Find("Universal Render Pipeline/Lit"); // URP：默认材质会变粉，显式建材质
        if (shader == null) shader = Shader.Find("Standard");

        int n = 0;
        foreach (ItemQuality q in Enum.GetValues(typeof(ItemQuality)))
        {
            // 材质（按品质上色，复用/覆盖；预制体按 GUID 引用它）
            var matPath = $"{MatFolder}/DropBox_{q}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.shader = shader;
            mat.color = ItemQualityPalette.Accent(q);
            EditorUtility.SetDirty(mat);

            // 预制体：单位立方体（自带 BoxCollider）+ 上色材质。DropItemSystem 实例化后按 Width/Height 缩放。
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"DropBox_{q}";
            cube.GetComponent<MeshRenderer>().sharedMaterial = mat;
            PrefabUtility.SaveAsPrefabAsset(cube, $"{PrefabFolder}/DropBox_{q}.prefab", out bool ok);
            UnityEngine.Object.DestroyImmediate(cube);
            if (ok) n++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ItemDropPrefab] 完成：生成 {n} 个品质掉落 box 预制体到 {PrefabFolder}/");
    }

    // 递归建目录（AssetDatabase.CreateFolder 不能一次建多级）。
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int i = path.LastIndexOf('/');
        var parent = path.Substring(0, i);
        var leaf = path.Substring(i + 1);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
