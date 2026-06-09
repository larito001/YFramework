using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 菜单：Tools/TPS/Build TimeBubble Prefab
/// 生成时间圈能量球预制体到 Resources/Bubble/TimeBubble.prefab：球 mesh + MeshRenderer
/// （挂 Art/Bubble/TimeBubble.mat，**GUID 引用**——材质留在 Art 下不必进 Resources）、去掉 collider、关阴影。
/// <see cref="TimeScaleZone"/> 运行时 Resources.Load 这个预制体并实例化（按半径缩放）。
/// 改材质参数：直接调 Art/Bubble/TimeBubble.mat；改结构：重跑本菜单覆盖（guid 保留）。
/// </summary>
public static class TimeBubblePrefabBuilder
{
    private const string MaterialPath = "Assets/Art/Bubble/TimeBubble.mat";
    private const string OutPath = "Assets/Resources/Bubble/TimeBubble.prefab";

    [MenuItem("Tools/TPS/Build TimeBubble Prefab")]
    public static void Build()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            Debug.LogError($"[TimeBubblePrefab] 找不到材质 {MaterialPath}，中止。");
            return;
        }

        // 球 primitive 直径=1：TimeScaleZone 实例化后 localScale = Radius*2 → 半径 = Radius。
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "TimeBubbleDome";
        var col = sphere.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col); // 纯显示，无物理碰撞
        var mr = sphere.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;

        EnsureFolder("Assets/Resources/Bubble");
        PrefabUtility.SaveAsPrefabAsset(sphere, OutPath, out bool ok);
        Object.DestroyImmediate(sphere);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(ok ? $"[TimeBubblePrefab] 生成 {OutPath}" : $"[TimeBubblePrefab] 保存失败 {OutPath}");
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
