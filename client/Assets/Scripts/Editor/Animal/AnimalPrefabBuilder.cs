using System.IO;
using UnityEditor;
using UnityEditor.Animations;
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
/// 死亡动画各动物的 Animator bool 参数名不一样(isDead / isDead_0 ...)，这里从 AnimatorController
/// 自动探测(取名字含 dead/die/death 的 bool 参数)，烘焙进 prefab 上的 AnimalEntity，探测不到则回退 isDead。
///
/// 重跑会覆盖已有 prefab（guid 保留）。Art/Animals 下的源 prefab 不动。
/// 新增动物：在 <see cref="SourceFiles"/> 加一行文件名，并在 animal 配表 prefab 列填 Animals/{文件名}。
/// </summary>
public static class AnimalPrefabBuilder
{
    private const string ResourceFolder = "Assets/Resources";
    private const string OutSubfolder = "Animals";
    private const string PackRoot = "Assets/Art/Animals/Low Poly Animated Animals/Prefabs/Animals";

    // 与 animal 配表 prefab 列一一对应(Animals/{文件名})。野鸭->Goose(包内无鸭,用最接近的水禽),野兔/野猪复用为兔子/猪。
    private static readonly string[] SourceFiles =
    {
        // 原有 3 种
        "Goose", "Rabbit_Brown", "Boar",
        // 新增 19 种(熊/牛/骆驼/奶牛/鳄鱼/鹿/大象/长颈鹿/山羊/鸡/河马/马/狮子/麋鹿/犀牛/绵羊/老虎/狼/斑马)
        "Bear_Grizzly", "Cow_Horns_Brown", "Camel_Dromedary", "Cow_White", "Crocodile", "Deer",
        "Elephant_Male", "Giraffe", "Goat", "Hen", "Hippo", "Horse_Brown", "Lion_Male", "Reindeer",
        "Rhino", "Sheep_Wool", "Tiger", "Wolf", "Zebra",
    };

    [MenuItem("Tools/TPS/Build Animal Prefabs")]
    public static void Build()
    {
        EnsureFolder(ResourceFolder, "Assets", "Resources");
        EnsureFolder($"{ResourceFolder}/{OutSubfolder}", ResourceFolder, OutSubfolder);

        int ok = 0, fail = 0;
        foreach (var file in SourceFiles)
        {
            var srcPath = $"{PackRoot}/{file}.prefab";
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(srcPath);
            if (src == null)
            {
                Debug.LogError($"[AnimalPrefab] 找不到源 prefab: {srcPath}");
                fail++;
                continue;
            }

            var name = file;
            var prefabPath = $"{ResourceFolder}/{OutSubfolder}/{name}.prefab";

            // 实例化后彻底解包成独立层级（不再链接美术源 prefab），再剥掉非 idle 组件
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(src);
            try
            {
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                StripToIdle(instance);
                AddHitZones(instance);

                var animator = instance.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.runtimeAnimatorController == null)
                    Debug.LogWarning($"[AnimalPrefab] {name} 没有 Animator 或未挂控制器，idle 可能不会播放");

                // 烘焙 AnimalEntity 并自动探测死亡参数名(运行时 AnimalSystem 只覆盖 animalId/score,保留这里的 deathBool)
                var entity = instance.GetComponent<AnimalEntity>();
                if (entity == null) entity = instance.AddComponent<AnimalEntity>();
                entity.deathBool = DetectDeathBool(animator, name);

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

    /// <summary>
    /// 从动物的 AnimatorController 自动探测死亡动画对应的 bool 参数名:取第一个名字含 dead/die/death 的 bool 参数。
    /// 探测不到(无控制器/无此类参数)则回退 "isDead",此时运行时死亡动画可能不播,但仍会按时销毁(见 AnimalEntity)。
    /// </summary>
    private static string DetectDeathBool(Animator animator, string name)
    {
        var rac = animator != null ? animator.runtimeAnimatorController : null;
        // 源 prefab 一般直接挂 AnimatorController;若是 Override,取其底层控制器
        var ac = rac as AnimatorController;
        if (ac == null && rac is AnimatorOverrideController over) ac = over.runtimeAnimatorController as AnimatorController;
        if (ac == null) return "isDead";

        foreach (var p in ac.parameters)
        {
            if (p.type != AnimatorControllerParameterType.Bool) continue;
            var n = p.name.ToLowerInvariant();
            if (n.Contains("dead") || n.Contains("die") || n.Contains("death"))
                return p.name;
        }
        Debug.LogWarning($"[AnimalPrefab] {name} 未在 Animator 找到死亡 bool 参数,回退 isDead(死亡动画可能不播)");
        return "isDead";
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
    /// 按模型渲染包围盒切出三个不重叠的部位碰撞体(各挂 <see cref="AnimalHitZone"/>):
    ///   头  = 体长方向「前 40%」的上半;
    ///   心脏 = 「前 40%」的下半(前胸);
    ///   身体 = 余下「后 60%」全高。
    /// 三块拼起来 ≈ 整只动物;射线打中哪块就算哪个部位(头/心脏一枪死,身体两枪)。
    /// 「前」是体长轴(取 X/Z 中较长者)的哪一端,优先按名字含 head 的骨骼判定,找不到默认 +轴。
    /// spawn 时根节点缩放会一并作用到碰撞体上。
    /// </summary>
    private static void AddHitZones(GameObject root)
    {
        // 统一用我们切的三块:先清掉源模型可能自带的碰撞体
        foreach (var c in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c, true);

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        // 实例化时根节点在原点且为单位变换,世界包围盒可直接当局部盒用
        var b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        Vector3 min = b.min, max = b.max, size = b.size, center = b.center;

        bool axisZ = size.z >= size.x;                  // 较长的水平轴 = 体长轴
        float len = axisZ ? size.z : size.x;
        float front = 0.40f * len;                       // 前 40% 为头+心脏区
        float midY = center.y;                           // 中线分上(头)/下(心脏)

        // 判定「前」在体长轴哪一端
        float headCoord = HeadCoordOnAxis(root, axisZ, out bool found);
        float centerCoord = axisZ ? center.z : center.x;
        bool frontPositive = !found || headCoord >= centerCoord;

        float aMin = axisZ ? min.z : min.x;
        float aMax = axisZ ? max.z : max.x;
        float frontLo, frontHi, rearLo, rearHi;
        if (frontPositive) { frontHi = aMax; frontLo = aMax - front; rearLo = aMin; rearHi = aMax - front; }
        else               { frontLo = aMin; frontHi = aMin + front; rearLo = aMin + front; rearHi = aMax; }

        if (axisZ)
        {
            MakeZoneBox(root, "Hit_Head",  min.x, max.x, midY,  max.y, frontLo, frontHi, HitZone.Head);
            MakeZoneBox(root, "Hit_Heart", min.x, max.x, min.y, midY,  frontLo, frontHi, HitZone.Heart);
            MakeZoneBox(root, "Hit_Body",  min.x, max.x, min.y, max.y, rearLo,  rearHi,  HitZone.Body);
        }
        else
        {
            MakeZoneBox(root, "Hit_Head",  frontLo, frontHi, midY,  max.y, min.z, max.z, HitZone.Head);
            MakeZoneBox(root, "Hit_Heart", frontLo, frontHi, min.y, midY,  min.z, max.z, HitZone.Heart);
            MakeZoneBox(root, "Hit_Body",  rearLo,  rearHi,  min.y, max.y, min.z, max.z, HitZone.Body);
        }
    }

    /// <summary>给某部位盒加一层贴合的发光高亮盒(头/心脏/身体各一色)。无碰撞、不投影,只做视觉标注。</summary>
    private static void AddGlow(GameObject zoneBox, Vector3 size, HitZone zone)
    {
        var glow = GameObject.CreatePrimitive(PrimitiveType.Cube);
        glow.name = "Glow";
        var col = glow.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col); // 高亮盒不参与命中判定

        glow.transform.SetParent(zoneBox.transform, false);
        glow.transform.localPosition = Vector3.zero;
        glow.transform.localRotation = Quaternion.identity;
        glow.transform.localScale = size; // 内置 Cube 是 1×1×1,缩放后正好贴合部位盒

        var mr = glow.GetComponent<MeshRenderer>();
        mr.sharedMaterial = GetGlowMaterial(zone);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    /// <summary>取(没有则建)部位高亮材质:头=暖黄、心脏=红、身体=绿,均用 Hunting/WeakPointHighlight。</summary>
    private static Material GetGlowMaterial(HitZone zone)
    {
        const string dir = "Assets/Art/Effects";
        string name; Color color;
        switch (zone)
        {
            case HitZone.Head:  name = "WeakHead";  color = new Color(1f, 0.85f, 0.2f);  break; // 暖黄
            case HitZone.Heart: name = "WeakHeart"; color = new Color(1f, 0.22f, 0.16f); break; // 红
            default:            name = "WeakBody";  color = new Color(0.25f, 1f, 0.35f);  break; // 绿
        }
        string path = $"{dir}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Hunting/WeakPointHighlight");
        if (shader == null)
        {
            Debug.LogError("[AnimalPrefab] 找不到 Hunting/WeakPointHighlight shader,部位高亮未生成");
            return null;
        }
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        var mat = new Material(shader) { name = name };
        mat.SetColor("_Color", color);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    /// <summary>名字含 "head" 的骨骼在体长轴上的世界坐标(用于判定动物朝向);找不到 found=false。</summary>
    private static float HeadCoordOnAxis(GameObject root, bool axisZ, out bool found)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.ToLowerInvariant().Contains("head"))
            {
                found = true;
                return axisZ ? t.position.z : t.position.x;
            }
        }
        found = false;
        return 0f;
    }

    /// <summary>按世界轴对齐区间建一个部位碰撞体子物体(root 在原点单位变换,世界=局部)。</summary>
    private static void MakeZoneBox(GameObject root, string name,
        float xMin, float xMax, float yMin, float yMax, float zMin, float zMax, HitZone zone)
    {
        var go = new GameObject(name, typeof(BoxCollider), typeof(AnimalHitZone));
        go.transform.SetParent(root.transform, true);
        go.transform.position = new Vector3((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f, (zMin + zMax) * 0.5f);
        go.transform.localRotation = Quaternion.identity;

        var col = go.GetComponent<BoxCollider>();
        col.center = Vector3.zero;
        var sizeV = new Vector3(Mathf.Abs(xMax - xMin), Mathf.Abs(yMax - yMin), Mathf.Abs(zMax - zMin));
        col.size = sizeV;
        go.GetComponent<AnimalHitZone>().zone = zone;

        // 每个部位叠一层发光高亮盒标注命中范围:头=黄、心脏=红、身体=绿
        AddGlow(go, sizeV, zone);
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
