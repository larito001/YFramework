using UnityEngine;
using YFramework.Config;

/// <summary>
/// 掉落物系统(<see cref="IGameService"/>)。统一负责把物品"丢"到世界:
/// 生成带模型 + 物理的 <see cref="DropItem"/>,靠重力落到地面,玩家靠近按 F 捡回背包。
/// <see cref="BagSystem.Discard"/> 调 <see cref="DropAtPlayer"/> 在玩家身前丢出。
///
/// **模型(当前)**:按品质上色的 box 占位——URP/Lit 运行时建材质(默认材质在 URP 下会变粉),
/// 尺寸随配表 <c>Width/Height</c>。
/// **接真实模型**:给 item 配表加 <c>modelPath</c> 列,改 <see cref="BuildModel"/> 走
/// <c>ResMgr.Load&lt;GameObject&gt;(cfg.ModelPath)</c> 实例化即可,丢弃/捡起逻辑不动。
/// CharacterManager / ViewManager 注册晚于本系统,一律懒取。
/// </summary>
public class DropItemSystem : IGameService
{
    private const int IgnoreRaycastLayer = 2; // Unity 内置层:掉落物落地碰撞照常,但不被射线(子弹等)命中

    private GameContext ctx;
    private CharacterManager characterMgr;
    private ViewManager viewMgr;
    private BagSystem bagSystem;

    public void Init(GameContext context)
    {
        ctx = context;
        ctx.TryGet(out bagSystem);
    }

    public void Shutdown()
    {
        ctx = null;
        characterMgr = null;
        viewMgr = null;
        bagSystem = null;
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

    /// <summary>在世界指定位置生成一个掉落物(模型 + 碰撞 + 重力 + <see cref="DropItem"/>)。失败返回 null。</summary>
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

        var drop = go.AddComponent<DropItem>();
        drop.Setup(itemId, count, rotation);
        return go;
    }

    /// <summary>
    /// 构建掉落物外观。**当前**:按品质上色的 box 占位(URP/Lit 运行时材质,尺寸随配表 Width/Height)。
    /// 接真实模型时只改这里:resMgr.Load&lt;GameObject&gt;(cfg.ModelPath) → Instantiate。
    /// </summary>
    private GameObject BuildModel(Item cfg)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); // 自带 BoxCollider/MeshRenderer
        int w = Mathf.Clamp(cfg.Width <= 0 ? 1 : cfg.Width, 1, 4);
        int h = Mathf.Clamp(cfg.Height <= 0 ? 1 : cfg.Height, 1, 4);
        go.transform.localScale = new Vector3(w * 0.3f, 0.3f, h * 0.3f); // 不同物品尺寸有别

        var rend = go.GetComponent<MeshRenderer>();
        if (rend != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit"); // URP:默认材质会变粉,显式建材质
            if (shader == null) shader = Shader.Find("Standard");
            rend.sharedMaterial = new Material(shader)
            {
                color = ItemQualityPalette.Accent((ItemQuality)cfg.Quality) // 按品质上色
            };
        }
        return go;
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
