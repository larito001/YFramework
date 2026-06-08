using UnityEngine;

/// <summary>
/// 局部时间缩放圈：圈内 Actor（角色 / 子弹 / 任意 Actor）的 <see cref="Actor.ZoneScale"/> 被压到
/// <see cref="InsideScale"/>（默认 0.5），离开还原。**全局缩放 <see cref="TimeScaleService.GlobalScale"/> 保持 1 不变**。
///
/// **纯逻辑、不走 Unity 物理**：本组件只负责"登记自己 + 显示能量球"。真正的"谁在圈内"判断由
/// <see cref="TimeScaleZoneService"/> 每帧对所有 Actor 做距离比较完成（见该类注释）。
/// 圈不挂任何 Collider，也没有 OnTrigger——因为子弹无 Collider、靠代码挪 transform 不产生触发事件，
/// 且触发器会被弹道 raycast 命中导致子弹在圈表面消失。
///
/// 可视化＝一个干净的亮蓝能量球（着色器 <c>Custom/TimeScaleShield</c>：Fresnel 边缘 + 体积噪声流动 + Scene Color 折射）。
/// 用法：代码里一行 <see cref="Spawn"/>，见 GameStartScene 测试块；或挂到物体上配 Radius / InsideScale。
/// </summary>
public class TimeScaleZone : MonoBehaviour
{
    [Tooltip("圈内 Actor 的区域时间缩放（1=正常，0.5=半速，0=冻结）")]
    public float InsideScale = 1f;

    [Tooltip("圈半径（米）。Awake 时同步给能量球；TimeScaleZoneService 用它做距离判断。")]
    public float Radius = 3f;

    // 预制材质资产路径（Resources 下，不含扩展名）。改材质参数在该 .mat 上调即可。
    private const string BubbleMaterialPath = "Materials/TimeBubble";

    private TimeScaleZoneService zoneService;

    /// <summary>一行生成一个测试圈。center=世界坐标，radius=半径，insideScale=圈内缩放。</summary>
    public static TimeScaleZone Spawn(Vector3 center, float radius = 3f, float insideScale = 0.5f)
    {
        // 先 inactive 再加组件 → Awake 推迟到 SetActive(true)，保证读到下面设好的 Radius/InsideScale。
        var go = new GameObject("TestTimeScaleZone");
        go.SetActive(false);
        go.transform.position = center;
        var zone = go.AddComponent<TimeScaleZone>();
        zone.Radius = radius;
        zone.InsideScale = insideScale;
        go.SetActive(true);
        return zone;
    }

    private void Awake()
    {
        BuildBubble();
    }

    // ── 可视化：球体 Mesh + Custom/TimeScaleShield 能量球。纯显示，collider 一律删掉。 ──
    private void BuildBubble()
    {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "TimeBubbleDome";
        var col = sphere.GetComponent<Collider>();
        if (col != null) Destroy(col); // 圈是纯逻辑，不要任何物理碰撞体

        var t = sphere.transform;
        t.SetParent(transform, false);
        t.localPosition = Vector3.zero;                 // 与圈心同心
        t.localScale = Vector3.one * (Radius * 2f);     // 球 primitive 直径=1 → ×2R = 半径 Radius

        var mr = sphere.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        // 用预先做好的材质资产（Resources/Materials/TimeBubble.mat），不在运行时 new Material。
        var mat = Resources.Load<Material>(BubbleMaterialPath);
        if (mat == null)
        {
            // 兜底：资产缺失时才退回运行时生成，避免完全不显示。
            var sh = Shader.Find("Custom/TimeScaleShield");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh != null) mat = new Material(sh);
            Debug.LogWarning($"[TimeScaleZone] 未找到材质 Resources/{BubbleMaterialPath}.mat，已退回运行时生成");
        }
        if (mat != null) mr.sharedMaterial = mat;
    }

    private TimeScaleZoneService ZoneService()
    {
        if (zoneService != null) return zoneService;
        var ctx = GameLoop.Instance != null ? GameLoop.Instance.Ctx : null;
        if (ctx != null) ctx.TryGet(out zoneService);
        return zoneService;
    }

    // 在注册表里登记自己，TimeScaleZoneService 每帧据此对所有 Actor 做距离判断。
    // OnEnable/OnDisable 配对，兼容 SetActive 开关；圈消失后服务下一帧把残留 ZoneScale 复位为 1。
    private void OnEnable() => ZoneService()?.Register(this);

    private void OnDisable() => ZoneService()?.Unregister(this);

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, Radius);
    }
}
