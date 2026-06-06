#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 菜单:Tools/TPS/Build All Chapter Maps
/// 用 Nature 美术包里的自然景观预制体,按「每关一个主题」程序化拼出第 1~6 关的场景预制体到
/// <c>Resources/Map/</c> 下(沙漠/沼泽/草原/热带草原/竹林/戈壁)。
///
/// 与沙漠同一套做法(见 Chapter1DesertMapBuilder):
///   ChapterN_XXX
///     ├─ Ground  —— 一整块平面(URP 主题色材质,自带 MeshCollider,顶面 y=0),供射线贴地/动物站立
///     ├─ Props   —— 主题装饰散布在中心活动区(半径 PlayRadius)之外做边框点缀;全部剥碰撞体不挡射击
///     └─ Clouds  —— 高空云朵
/// 每关固定随机种子,结果可复现;重跑覆盖旧 prefab。换主题/装饰只改 <see cref="Themes"/> 表。
///
/// 生成后需在 map 配表(tools/excel/3xlsx/map.xlsx)把对应关卡的 scenePath 列填成 Map/ChapterN_XXX,
/// 再跑 publish_config.py 才会被 <see cref="GameStartScene"/> 加载。
/// </summary>
public static class ChapterMapsBuilder
{
    private const string NaturePack = "Assets/Art/Animals/Low Poly Animated Animals/Prefabs/Nature";
    private const string OutDir = "Assets/Resources/Map";
    private const string MatDir = "Assets/Art/Map";

    private const float GroundSize = 140f; // 地面边长(米)
    private const float PlayRadius = 16f;  // 中心活动区半径:动物/玩家在此,大装饰物避开

    /// <summary>一类散布装饰:预制体名 + 数量 + 缩放区间。</summary>
    private readonly struct Deco
    {
        public readonly string Name;
        public readonly int Count;
        public readonly float MinScale;
        public readonly float MaxScale;
        public Deco(string name, int count, float minScale, float maxScale)
        { Name = name; Count = count; MinScale = minScale; MaxScale = maxScale; }
    }

    /// <summary>一关的主题:输出名(= scenePath 尾) + 地面色 + 随机种子 + 装饰清单 + 是否铺干裂地表。</summary>
    private sealed class Theme
    {
        public string OutName;
        public Color Ground;
        public int Seed;
        public bool Cracked;   // 铺压扁的干裂地表斑块(戈壁/荒地用)
        public Deco[] Props;
    }

    // 第 1~6 关主题表(对应 map 配表 id 1~6:沙漠/沼泽/草原/热带草原/竹林/戈壁)
    private static readonly Theme[] Themes =
    {
        new Theme // 1 沙漠(仙人掌/枯树/猴面包树/石头/头骨 + 干裂地表)
        {
            OutName = "Chapter1_Desert", Ground = new Color(0.83f, 0.69f, 0.45f), Seed = 20240601, Cracked = true,
            Props = new[]
            {
                new Deco("Cactus_Big",  16, 0.8f, 1.4f),
                new Deco("Tree_Dead",   10, 0.9f, 1.5f),
                new Deco("Tree_Baobab",  5, 0.9f, 1.3f),
                new Deco("Stone_Flat",  14, 0.8f, 1.8f),
                new Deco("Skull_Human",  6, 0.8f, 1.2f),
            },
        },
        new Theme // 2 沼泽
        {
            OutName = "Chapter2_Swamp", Ground = new Color(0.30f, 0.38f, 0.28f), Seed = 20240602,
            Props = new[]
            {
                new Deco("Tree_Dead",         12, 0.9f, 1.6f),
                new Deco("Tree_Trunk",         8, 0.8f, 1.4f),
                new Deco("Mushroom_Toadstool",14, 0.8f, 1.6f),
                new Deco("Stone_Flat",        10, 0.7f, 1.4f),
                new Deco("Leaf_Plane",        10, 0.8f, 1.4f),
            },
        },
        new Theme // 3 草原
        {
            OutName = "Chapter3_Grassland", Ground = new Color(0.45f, 0.62f, 0.30f), Seed = 20240603,
            Props = new[]
            {
                new Deco("Tree_Forest",       14, 0.9f, 1.5f),
                new Deco("Stone_Flat",        10, 0.7f, 1.5f),
                new Deco("Mushroom_Toadstool", 8, 0.8f, 1.3f),
                new Deco("Carrot",             6, 0.8f, 1.2f),
                new Deco("Leaf_Simple",       10, 0.8f, 1.3f),
            },
        },
        new Theme // 4 热带草原(萨瓦纳:猴面包树 + 枯草色)
        {
            OutName = "Chapter4_Savanna", Ground = new Color(0.72f, 0.64f, 0.34f), Seed = 20240604,
            Props = new[]
            {
                new Deco("Tree_Baobab",        8, 1.0f, 1.6f),
                new Deco("Tree_Dead",          8, 0.9f, 1.4f),
                new Deco("Stone_Flat",        12, 0.7f, 1.6f),
                new Deco("Cactus_Big",         6, 0.8f, 1.2f),
            },
        },
        new Theme // 5 竹林
        {
            OutName = "Chapter5_Bamboo", Ground = new Color(0.38f, 0.55f, 0.32f), Seed = 20240605,
            Props = new[]
            {
                new Deco("Bamboo_Leaves",     20, 1.0f, 1.9f),
                new Deco("Tree_Forest",        8, 0.9f, 1.4f),
                new Deco("Mushroom_Toadstool", 8, 0.8f, 1.3f),
                new Deco("Stone_Flat",         8, 0.7f, 1.3f),
                new Deco("Leaf_Simple",        8, 0.8f, 1.2f),
            },
        },
        new Theme // 6 戈壁(碎石 + 干裂地表)
        {
            OutName = "Chapter6_Gobi", Ground = new Color(0.58f, 0.52f, 0.44f), Seed = 20240606, Cracked = true,
            Props = new[]
            {
                new Deco("Stone_Flat",        18, 0.8f, 1.9f),
                new Deco("Tree_Dead",          8, 0.9f, 1.4f),
                new Deco("Skull_Human",        6, 0.8f, 1.2f),
                new Deco("Cactus_Big",         6, 0.8f, 1.2f),
            },
        },
    };

    private static readonly Deco[] Clouds =
    {
        new Deco("Cloud_Big",  5, 1.0f, 1.8f),
        new Deco("Cloud_Long", 4, 1.0f, 1.6f),
    };

    [MenuItem("Tools/TPS/Build All Chapter Maps")]
    public static void BuildAll()
    {
        if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);
        foreach (var t in Themes) BuildOne(t);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void BuildOne(Theme t)
    {
        Random.InitState(t.Seed);
        var root = new GameObject(t.OutName);
        try
        {
            BuildGround(root.transform, t);
            ScatterProps(root.transform, t.Props);
            BuildClouds(root.transform);

            var outPath = $"{OutDir}/{t.OutName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, outPath, out bool ok);
            Debug.Log(ok ? $"[ChapterMaps] 生成 {outPath}" : $"[ChapterMaps] 保存失败 {outPath}");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // ---------------- 地面 ----------------

    private static void BuildGround(Transform parent, Theme t)
    {
        // 一整块大平面(URP 主题色材质),顶面 y=0,自带 MeshCollider 供射线贴地/动物站立。
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.SetParent(parent, false);
        ground.transform.localScale = new Vector3(GroundSize / 10f, 1f, GroundSize / 10f); // Plane 原始边长 10
        ground.GetComponent<Renderer>().sharedMaterial = GetGroundMaterial(t.OutName, t.Ground);

        if (!t.Cracked) return;

        // 干裂地表细节:压扁的裂地斑块稀疏铺在地面上(不挡射击,已剥碰撞体)
        var crack = Load("Ground_Cracked");
        if (crack == null) return;
        var patches = new GameObject("CrackedPatches");
        patches.transform.SetParent(ground.transform, false);
        float half = GroundSize * 0.5f - 6f;
        for (int i = 0; i < 26; i++)
        {
            var p = (GameObject)PrefabUtility.InstantiatePrefab(crack, patches.transform);
            float s = Random.Range(3f, 6f);
            p.transform.localScale = new Vector3(s, s * 0.12f, s);
            p.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            PlaceOnGround(p, new Vector3(Random.Range(-half, half), 0f, Random.Range(-half, half)));
            StripColliders(p);
        }
    }

    /// <summary>取(没有则建)主题地面 URP 材质,存为资产以便随 prefab 序列化。</summary>
    private static Material GetGroundMaterial(string themeName, Color color)
    {
        string matPath = $"{MatDir}/{themeName}_Ground.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (existing != null) { existing.color = color; return existing; }

        if (!Directory.Exists(MatDir)) Directory.CreateDirectory(MatDir);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard"); // 退路
        var mat = new Material(shader) { name = $"{themeName}_Ground" };
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color", color); // 兼容 Standard
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f); // 哑光
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.05f);
        AssetDatabase.CreateAsset(mat, matPath);
        return mat;
    }

    // ---------------- 装饰散布 ----------------

    private static void ScatterProps(Transform parent, Deco[] scatter)
    {
        var props = new GameObject("Props");
        props.transform.SetParent(parent, false);
        float outer = GroundSize * 0.5f - 6f;

        foreach (var deco in scatter)
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

    // ---------------- 工具(同 Chapter1DesertMapBuilder) ----------------

    /// <summary>把物体放到 (x,0,z),并按其包围盒底部贴到地面 y=0。</summary>
    private static void PlaceOnGround(GameObject go, Vector3 xz)
    {
        go.transform.position = new Vector3(xz.x, 0f, xz.z);
        var b = WorldBounds(go);
        go.transform.position += new Vector3(0f, -b.min.y, 0f);
    }

    /// <summary>中心活动区(inner)外、地图边(outer)内的环形随机点(只取 x/z,面积均匀)。</summary>
    private static Vector3 RingPos(float inner, float outer)
    {
        float a = Random.Range(0f, Mathf.PI * 2f);
        float r = Mathf.Sqrt(Random.Range(inner * inner, outer * outer));
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
        if (go == null) Debug.LogWarning($"[ChapterMaps] 找不到 Nature 预制体: {path}");
        return go;
    }
}
#endif
