using System.Collections.Generic;
using UnityEngine;
using YFramework.Config;

/// <summary>
/// 掉落物系统(<see cref="IGameService"/> + <see cref="ITickable"/>)。统一负责把物品"丢"到世界,
/// 并管理掉落物 Actor 的生命周期 —— 既是**工厂**(建模型 + 物理 + <see cref="DropItemActor"/> + <see cref="DropItemView"/>)
/// 又是**管理器**(维护 actor 列表、注册 <see cref="ActorWorld"/>、Tick、延迟移除),对照 <see cref="TowerManager"/>。
/// 生成的掉落物靠重力落到地面,玩家靠近按 F 捡回背包。<see cref="BagSystem.Discard"/> 调 <see cref="DropAtPlayer"/> 在玩家身前丢出。
///
/// 掉落物纳入 Actor/View 体系(和角色/塔一致):模型 GameObject 由本系统运行时拼装(非单一 prefab),
/// 故走 <see cref="ViewManager.RegisterView"/> 注册已建好的 view,而非 LoadBaseView。
/// 当前掉落物无逻辑组件,Tick 是廉价空转,保留是为了一致 + 便于以后加组件(如限时消失)。
///
/// **模型(当前)**:按品质加载占位 box 预制体 <c>Item/Prefabs/DropBox_&lt;品质&gt;</c>
/// (5 个品质各一个、已上色,由菜单 Tools/Bag/Build Item Drop Prefabs 生成),尺寸随配表 <c>Width/Height</c> 缩放;
/// 预制体缺失时退回运行时拼一个上色 cube(不崩)。
/// **接真实模型**:把对应 prefab 换成真模型即可;或给 item 配表加 <c>modelPath</c> 列、改 <see cref="BuildModel"/> 走 <c>cfg.ModelPath</c>,丢弃/捡起逻辑不动。
/// CharacterManager / ViewManager / ActorWorld 注册晚于本系统,一律懒取。
/// </summary>
public class DropItemSystem : IGameService, ITickable
{
    private const int IgnoreRaycastLayer = 2; // Unity 内置层:掉落物落地碰撞照常,但不被射线(子弹等)命中

    private GameContext ctx;
    private CharacterManager characterMgr;
    private ViewManager viewMgr;
    private ActorWorld world;
    private BagSystem bagSystem;
    // 占位 box 材质按品质缓存复用:每丢一次 new Material 会泄漏(不随 GameObject 回收),故只建 5 个共用。
    private readonly Dictionary<ItemQuality, Material> matCache = new Dictionary<ItemQuality, Material>();

    private readonly List<DropItemActor> drops = new List<DropItemActor>();
    // 延迟移除:捡起在 F 触发回调里发生(不在 Tick 内),但统一走 deferred 更安全(去重、避免改正在遍历的列表)。
    private readonly List<DropItemActor> toRemove = new List<DropItemActor>();
    private bool ticking;

    public void Init(GameContext context)
    {
        ctx = context;
        ctx.TryGet(out bagSystem);
    }

    public void Shutdown()
    {
        for (int i = drops.Count - 1; i >= 0; i--)
            RemoveImmediate(drops[i]);
        drops.Clear();
        toRemove.Clear();

        foreach (var mat in matCache.Values)
            if (mat != null) Object.Destroy(mat);
        matCache.Clear();
        ctx = null;
        characterMgr = null;
        viewMgr = null;
        world = null;
        bagSystem = null;
    }

    public void Tick(float dt)
    {
        ticking = true;
        try
        {
            for (int i = 0; i < drops.Count; i++)
                drops[i].Tick(dt);
        }
        finally { ticking = false; }

        if (toRemove.Count > 0)
        {
            for (int i = 0; i < toRemove.Count; i++) RemoveImmediate(toRemove[i]);
            toRemove.Clear();
        }
    }

    /// <summary>请求移除掉落物(deferred):<see cref="DropItemView.Interact"/> 全部捡走后调。
    /// 统一注销 ActorWorld + 销毁 view(连带 GameObject) + Dispose actor。</summary>
    public void RemoveDropItem(DropItemActor actor)
    {
        if (actor == null) return;
        if (ticking)
        {
            if (!toRemove.Contains(actor)) toRemove.Add(actor);
            return;
        }
        RemoveImmediate(actor);
    }

    private void RemoveImmediate(DropItemActor actor)
    {
        if (actor == null) return;
        drops.Remove(actor);
        if (world == null) ctx?.TryGet(out world);
        world?.Unregister(actor.ID);
        if (viewMgr == null) ctx?.TryGet(out viewMgr);
        viewMgr?.RemoveBaseView(actor.ID); // 销毁 view 的 GameObject
        actor.Dispose();
    }

    /// <summary>在玩家身前丢出一个掉落物(给一点向前上方的初速度,落到地面)。</summary>
    public void DropAtPlayer(int itemId, int count, int rotation)
    {
        GetPlayerPose(out Vector3 pos, out Vector3 forward);
        Vector3 dropPos = pos + Vector3.up * 1.0f + forward * 1.2f; // 抬高+身前,避免压在脚下
        var go = Spawn(itemId, count, rotation, dropPos);
        if (go == null) return;
        var rb = go.GetComponent<Rigidbody>();
        if (rb != null) rb.velocity = forward * 2.5f + Vector3.up * 1.5f; // 向前上方抛出
    }

    /// <summary>在世界指定位置生成一个掉落物(模型 + 碰撞 + 重力 + <see cref="DropItemActor"/> + <see cref="DropItemView"/>)。失败返回 null。</summary>
    public GameObject Spawn(int itemId, int count, int rotation, Vector3 worldPos)
    {
        var cfg = bagSystem != null ? bagSystem.GetItem(itemId) : null;
        if (cfg == null) { Debug.LogWarning($"[DropItemSystem] 配表无物品 {itemId},不生成掉落物"); return null; }

        var go = BuildModel(cfg);
        go.name = $"Drop_{itemId}_{cfg.Name}";
        go.transform.position = worldPos;
        go.layer = IgnoreRaycastLayer;

        if (go.GetComponent<Collider>() == null) go.AddComponent<BoxCollider>(); // 落地需要碰撞体
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 1f;

        // 纳入 Actor/View 体系:建 actor + 在模型 GO 上挂 view,经 ViewManager 注册(模型是运行时拼的,
        // 用 RegisterView 而非 LoadBaseView),再注册 ActorWorld + 入管理列表。
        var actor = new DropItemActor
        {
            ItemId = itemId,
            Count = Mathf.Max(1, count),
            DropRotation = rotation,
            DisplayName = !string.IsNullOrEmpty(cfg.Name) ? cfg.Name : "物品",
        };
        actor.Position = worldPos;

        var view = go.AddComponent<DropItemView>();
        if (viewMgr == null) ctx.TryGet(out viewMgr);
        if (world == null) ctx.TryGet(out world);
        viewMgr?.RegisterView(view, actor); // 内部 Bind(actor) → 注册世界交互 + 入 Views 字典
        world?.Register(actor);
        drops.Add(actor);
        return go;
    }

    /// <summary>
    /// 构建掉落物外观:按品质加载占位 box 预制体 <c>Item/Prefabs/DropBox_&lt;品质&gt;</c>(已上色),
    /// 再按配表 Width/Height 缩放(不同物品尺寸有别)。预制体缺失则退回运行时拼一个上色 cube。
    /// 接真实模型:换对应 prefab,或 item 配表加 modelPath 后这里改走 cfg.ModelPath。
    /// </summary>
    private GameObject BuildModel(Item cfg)
    {
        var quality = (ItemQuality)cfg.Quality;
        int w = Mathf.Clamp(cfg.Width <= 0 ? 1 : cfg.Width, 1, 4);
        int h = Mathf.Clamp(cfg.Height <= 0 ? 1 : cfg.Height, 1, 4);

        GameObject go;
        var prefab = Resources.Load<GameObject>($"Item/Prefabs/DropBox_{quality}");
        if (prefab != null)
        {
            go = Object.Instantiate(prefab);
        }
        else
        {
            // 兜底:预制体缺失(没跑 Tools/Bag/Build Item Drop Prefabs)时运行时拼一个上色 cube,避免不显示。
            go = GameObject.CreatePrimitive(PrimitiveType.Cube); // 自带 BoxCollider/MeshRenderer
            var rend = go.GetComponent<MeshRenderer>();
            if (rend != null) rend.sharedMaterial = GetQualityMaterial(quality);
            Debug.LogWarning($"[DropItemSystem] 未找到预制体 Item/Prefabs/DropBox_{quality}，已退回运行时生成(跑 Tools/Bag/Build Item Drop Prefabs 生成)");
        }

        go.transform.localScale = new Vector3(w * 0.3f, 0.3f, h * 0.3f); // 不同物品尺寸有别
        return go;
    }

    /// <summary>取/建某品质的占位材质(仅 BuildModel 兜底路径用,按品质上色,缓存复用避免泄漏 Material)。</summary>
    private Material GetQualityMaterial(ItemQuality quality)
    {
        if (matCache.TryGetValue(quality, out var mat) && mat != null) return mat;
        var shader = Shader.Find("Universal Render Pipeline/Lit"); // URP:默认材质会变粉,显式建材质
        if (shader == null) shader = Shader.Find("Standard");
        mat = new Material(shader) { color = ItemQualityPalette.Accent(quality) };
        matCache[quality] = mat;
        return mat;
    }

    /// <summary>玩家世界坐标与朝向:优先用 view 的 transform(实时);取不到退回逻辑 Position。</summary>
    private void GetPlayerPose(out Vector3 pos, out Vector3 forward)
    {
        pos = Vector3.up;
        forward = Vector3.forward;
        if (characterMgr == null) ctx.TryGet(out characterMgr);
        var player = characterMgr != null ? characterMgr.Player : null;
        if (player == null) return;

        if (viewMgr == null) ctx.TryGet(out viewMgr);
        if (viewMgr != null && viewMgr.TryGetView(player.ID, out var view) && view != null)
        {
            pos = view.transform.position;
            forward = view.transform.forward;
        }
        else
        {
            pos = player.Position;
        }
    }
}
