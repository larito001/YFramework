#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 菜单：Tools/TPS/Build Chapter1 Desert Map
/// 用 Nature 美术包里的自然景观预制体(裂地/仙人掌/枯树/猴面包树/石头/头骨/云)程序化拼出
/// 第一章「沙漠」场景预制体 <c>Resources/Map/Chapter1_Desert.prefab</c>。
///
/// 结构:
///   Chapter1_Desert
///     ├─ Ground            —— 一整块沙色平面(URP 材质,自带 MeshCollider,顶面 y=0)+ 压扁的裂地斑块做地表细节
///     ├─ Props             —— 仙人掌/枯树/猴面包树/石头/头骨,散布在中心活动区(半径 PlayRadius)之外做边框点缀
///     └─ Clouds            —— 高空云朵
///
/// 装饰物会被剥掉碰撞体(只保留 Ground 的地面碰撞体),避免挡住射击射线;一切按固定随机种子摆放,可复现。
/// 运行时由 <see cref="GameStartScene"/> 按选中关卡的 map 配表 scenePath 列实例化(沙漠=Map/Chapter1_Desert)。
///
/// 重跑覆盖旧 prefab。换/加装饰:改 <see cref="Scatter"/> 里的清单即可。
/// </summary>
public static class Chapter1DesertMapBuilder
{
    private const string NaturePack = "Assets/Art/Animals/Low Poly Animated Animals/Prefabs/Nature";
    private const string OutDir = "Assets/Resources/Map";
    private const string OutPath = OutDir + "/Chapter1_Desert.prefab";

    private const float GroundSize = 140f;   // 地面边长(米);相机远眺也铺满
    private const float PlayRadius = 16f;     // 中心活动区半径:动物/玩家在此,大装饰物避开
    private const int Seed = 20240601;        // 固定种子,保证每次生成一致

    /// <summary>一类散布装饰:预制体名 + 数量 + 缩放区间。</summary>
    private readonly struct Deco
    {
        public readonly string Name;
        public readonly int Count;
        public readonly float MinScale;
        public readonly float MaxScale;
        public Deco(string name, int count, float minScale, float maxScale)
        {
            Name = name; Count = count; MinScale = minScale; MaxScale = maxScale;
        }
    }

    private static readonly Deco[] Scatter =
    {
        new Deco("Cactus_Big",   16, 0.8f, 1.4f),
        new Deco("Tree_Dead",    10, 0.9f, 1.5f),
        new Deco("Tree_Baobab",   5, 0.9f, 1.3f),
        new Deco("Stone_Flat",   14, 0.8f, 1.8f),
        new Deco("Skull_Human",   6, 0.8f, 1.2f),
    };

    private static readonly Deco[] Clouds =
    {
        new Deco("Cloud_Big",  5, 1.0f, 1.8f),
        new Deco("Cloud_Long", 4, 1.0f, 1.6f),
    };

    [MenuItem("Tools/TPS/Build Chapter1 Desert Map")]
    public static void Build()
    {
        if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);
        Random.InitState(Seed);

        var root = new GameObject("Chapter1_Desert");
        try
        {
            BuildGround(root.transform);
            ScatterProps(root.transform);
            BuildClouds(root.transform);

            PrefabUtility.SaveAsPrefabAsset(root, OutPath, out bool ok);
            Debug.Log(ok ? $"[DesertMap] 生成 {OutPath}" : $"[DesertMap] 保存失败 {OutPath}");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // ---------------- 地面 ----------------

    private static void BuildGround(Transform parent)
    {
        // 实底沙地:一整块大平面(URP 沙色材质),顶面 y=0。这是真正的地面,
        // 自带 MeshCollider 供射线贴地/动物站立——不再用裂地预制体平铺(那是凹凸碎块,会露底)。
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.SetParent(parent, false);
        ground.transform.localScale = new Vector3(GroundSize / 10f, 1f, GroundSize / 10f); // Plane 原始边长 10
        ground.GetComponent<Renderer>().sharedMaterial = GetSandMaterial();

        // 裂地只当「干裂地表」细节:压扁后稀疏铺在沙面上(含活动区),做颜色/纹理变化,不挡射击(已剥碰撞体)
        var crack = Load("Ground_Cracked");
        if (crack == null) return;
        var patches = new GameObject("CrackedPatches");
        patches.transform.SetParent(ground.transform, false);
        const int patchCount = 26;
        float half = GroundSize * 0.5f - 6f;
        for (int i = 0; i < patchCount; i++)
        {
            var p = (GameObject)PrefabUtility.InstantiatePrefab(crack, patches.transform);
            float s = Random.Range(3f, 6f);
            p.transform.localScale = new Vector3(s, s * 0.12f, s); // 压扁成贴地的干裂斑块
            p.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            float x = Random.Range(-half, half), z = Random.Range(-half, half);
            PlaceOnGround(p, new Vector3(x, 0f, z));
            StripColliders(p);
        }
    }

    /// <summary>取(没有则建)沙色 URP 材质,存为资产以便随 prefab 序列化。</summary>
    private static Material GetSandMaterial()
    {
        const string matDir = "Assets/Art/Map";
        const string matPath = matDir + "/DesertSand.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (existing != null) return existing;

        if (!Directory.Exists(matDir)) Directory.CreateDirectory(matDir);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard"); // 退路
        var mat = new Material(shader) { name = "DesertSand" };
        var sand = new Color(0.83f, 0.69f, 0.45f); // 暖沙色
        mat.SetColor("_BaseColor", sand);
        mat.SetColor("_Color", sand);              // 兼容 Standard
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f); // 哑光,不反光
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.05f);
        AssetDatabase.CreateAsset(mat, matPath);
        return mat;
    }

    // ---------------- 装饰散布 ----------------

    private static void ScatterProps(Transform parent)
    {
        var props = new GameObject("Props");
        props.transform.SetParent(parent, false);
        float outer = GroundSize * 0.5f - 6f;

        foreach (var deco in Scatter)
        {
            var src = Load(deco.Name);
            if (src == null) continue;
            for (int i = 0; i < deco.Count; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(src, props.transform);
                go.transform.localScale = Vector3.one * Random.Range(deco.MinScale, deco.MaxScale);
                go.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                PlaceOnGround(go, RingPos(PlayRadius, outer));
                StripColliders(go);
            }
        }
    }

    private static void BuildClouds(Transform parent)
    {
        var sky = new GameObject("Clouds");
        sky.transform.SetParent(parent, false);
        foreach (var deco in Clouds)
        {
            var src = Load(deco.Name);
            if (src == null) continue;
            for (int i = 0; i < deco.Count; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(src, sky.transform);
                go.transform.localScale = Vector3.one * Random.Range(deco.MinScale, deco.MaxScale);
                go.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                float r = Random.Range(0f, GroundSize * 0.45f);
                float a = Random.Range(0f, Mathf.PI * 2f);
                go.transform.position = new Vector3(Mathf.Cos(a) * r, Random.Range(45f, 70f), Mathf.Sin(a) * r);
                StripColliders(go);
            }
        }
    }

    // ---------------- 工具 ----------------

    /// <summary>把物体放到 (x,0,z),并按其包围盒底部贴到地面 y=0。</summary>
    private static void PlaceOnGround(GameObject go, Vector3 xz)
    {
        go.transform.position = new Vector3(xz.x, 0f, xz.z);
        var b = WorldBounds(go);
        go.transform.position += new Vector3(0f, -b.min.y, 0f);
    }

    /// <summary>中心活动区(inner)外、地图边(outer)内的环形随机点(只取 x/z)。</summary>
    private static Vector3 RingPos(float inner, float outer)
    {
        float a = Random.Range(0f, Mathf.PI * 2f);
        float r = Mathf.Sqrt(Random.Range(inner * inner, outer * outer)); // 面积均匀
        return new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
    }

    private static Bounds WorldBounds(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b;
    }

    private static void StripColliders(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(c, true);
    }

    private static GameObject Load(string name)
    {
        var path = $"{NaturePack}/{name}.prefab";
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null) Debug.LogWarning($"[DesertMap] 找不到 Nature 预制体: {path}");
        return go;
    }
}
#endif
