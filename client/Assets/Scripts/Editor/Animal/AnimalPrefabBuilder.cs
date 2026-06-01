using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 菜单：Tools/TPS/Build Animal Prefabs
/// 把 Art/Animals 美术包里的源 prefab 包成 Resources/Animals/*.prefab，让运行时 Resources.Load 能加载。
/// 源 prefab 自带 Animator（控制器默认状态即 Idle），但还挂着 NavMeshAgent / CharacterController /
/// AudioSource 以及美术包自带、未随美术导入的 demo AI 脚本（在工程里是 missing script）。
/// 当前只要「站立 idle、无复杂行为」，所以生成时会剥掉这些组件，只保留模型层级 + Animator。
/// 复杂行为后续再在这些 prefab 上加（预留）。
///
/// 重跑会覆盖已有 prefab（guid 保留）。Art/Animals 下的源 prefab 不动。
/// 新增动物：在 SourcePrefabPaths 加一行源 prefab 路径，并在 animal 配表 prefab 列填 Animals/{文件名}。
/// </summary>
public static class AnimalPrefabBuilder
{
    private const string ResourceFolder = "Assets/Resources";
    private const string OutSubfolder = "Animals";
    private const string PackRoot = "Assets/Art/Animals/Low Poly Animated Animals/Prefabs/Animals";

    /// <summary>一种动物：源 prefab 路径 + 死亡动画对应的 Animator bool 参数名(各动物不同)。</summary>
    private readonly struct Spec
    {
        public readonly string Src;
        public readonly string DeathBool;
        public Spec(string src, string deathBool) { Src = src; DeathBool = deathBool; }
    }

    // 与 animal 配表 prefab 列一一对应：野鸭->Goose（美术包无鸭，用最接近的水禽代替）、野兔->Rabbit_Brown、野猪->Boar
    private static readonly Spec[] Specs =
    {
        new Spec(PackRoot + "/Goose.prefab", "isDead"),
        new Spec(PackRoot + "/Rabbit_Brown.prefab", "isDead_0"),
        new Spec(PackRoot + "/Boar.prefab", "isDead"),
    };

    [MenuItem("Tools/TPS/Build Animal Prefabs")]
    public static void Build()
    {
        EnsureFolder(ResourceFolder, "Assets", "Resources");
        EnsureFolder($"{ResourceFolder}/{OutSubfolder}", ResourceFolder, OutSubfolder);

        int ok = 0, fail = 0;
        foreach (var spec in Specs)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(spec.Src);
            if (src == null)
            {
                Debug.LogError($"[AnimalPrefab] 找不到源 prefab: {spec.Src}");
                fail++;
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(spec.Src);
            var prefabPath = $"{ResourceFolder}/{OutSubfolder}/{name}.prefab";

            // 实例化后彻底解包成独立层级（不再链接美术源 prefab），再剥掉非 idle 组件
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(src);
            try
            {
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                StripToIdle(instance);
                AddHitCollider(instance);

                var animator = instance.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.runtimeAnimatorController == null)
                    Debug.LogWarning($"[AnimalPrefab] {name} 没有 Animator 或未挂控制器，idle 可能不会播放");

                // 烘焙 AnimalEntity 并配好死亡参数名(运行时 AnimalSystem 只覆盖 animalId/score,保留这里的 deathBool)
                var entity = instance.GetComponent<AnimalEntity>();
                if (entity == null) entity = instance.AddComponent<AnimalEntity>();
                entity.deathBool = spec.DeathBool;

                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath, out bool success);
                if (success)
                {
                    Debug.Log($"[AnimalPrefab] 生成 {prefabPath}");
                    ok++;
                }
                else
                {
                    Debug.LogError($"[AnimalPrefab] 保存失败: {prefabPath}");
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
        Debug.Log($"[AnimalPrefab] 完成: 成功 {ok}, 失败 {fail}");
    }

    /// <summary>剥掉一切会驱动复杂行为/移动的组件，只留模型 + Animator（控制器默认状态即 idle）。</summary>
    private static void StripToIdle(GameObject root)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            var go = t.gameObject;
            DestroyAll<NavMeshAgent>(go);
            DestroyAll<CharacterController>(go);
            DestroyAll<AudioSource>(go);
            // 美术包自带的 demo AI 脚本未导入工程，是 missing script，一并清掉
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        }
    }

    /// <summary>
    /// 按模型渲染包围盒在根节点补一个被动 BoxCollider,供射击命中判定(原箱子占位本就有碰撞体)。
    /// 不带任何行为;spawn 时根节点的缩放会一并作用到碰撞体上。
    /// </summary>
    private static void AddHitCollider(GameObject root)
    {
        if (root.GetComponentInChildren<Collider>(true) != null) return; // 已有碰撞体则不重复加

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        // 实例化时根节点在原点且为单位变换,世界包围盒可直接当作根节点的局部盒
        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        var box = root.AddComponent<BoxCollider>();
        box.center = root.transform.InverseTransformPoint(bounds.center);
        box.size = bounds.size;
    }

    private static void DestroyAll<T>(GameObject go) where T : Component
    {
        foreach (var c in go.GetComponents<T>())
            if (c != null) Object.DestroyImmediate(c, true);
    }

    private static void EnsureFolder(string fullPath, string parent, string leaf)
    {
        if (AssetDatabase.IsValidFolder(fullPath)) return;
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
