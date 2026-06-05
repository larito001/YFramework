using UnityEngine;
using UnityEngine.UI;
using YOTO;

/// <summary>
/// UI 里的武器 3D 模型预览(转台)。用一台只拍 <see cref="PreviewLayer"/> 层的离屏相机渲染到 RenderTexture,
/// 贴到调用方给的 <see cref="RawImage"/> 宿主上;模型在远离场景处缓慢自转,换成 Unlit 自发光以免受场景灯光影响。
///
/// 用法(面板里):
///   var preview = new WeaponModelPreview(hostRect, resMgr);
///   preview.Show("Weapon/AssaultRifle1_01");   // 切换展示的模型(空字符串=清空)
///   preview.SetActive(false);                  // 面板隐藏时停渲染省开销
///   preview.Dispose();                          // 彻底回收
///
/// 商店与装备界面共用此类(见 <see cref="ShopPanel"/> / <see cref="EquipPanel"/>)。
/// </summary>
public class WeaponModelPreview
{
    private const int PreviewLayer = 18; // ProjectSettings 中的 "WeaponPreview" 层
    private static int s_count;          // 给每个实例一个独立的世界原点,避免多预览同层互相入镜

    private readonly ResMgr res;
    private readonly RawImage image;
    private readonly Vector3 origin;
    private readonly bool spin;      // 是否自转(转台);false=固定不转
    private readonly bool sideView;  // 是否用正侧视图(否则默认 3/4 取景)
    private readonly int rtSize;     // RenderTexture 边长

    private RenderTexture rt;
    private Camera cam;
    private GameObject rig;
    private Transform pivot;
    private GameObject model;
    private string shownPath;
    private int showVersion; // 每次 Show/Clear 自增:异步加载回调比对,过期则丢弃(防快速切换覆盖)

    /// <param name="spin">是否缓慢自转(转台);装备卡用 false=不转。</param>
    /// <param name="sideView">true=正侧视图(看模型侧面);false=默认 3/4 取景。</param>
    /// <param name="rtSize">离屏贴图分辨率;卡片小图可调小省开销。</param>
    public WeaponModelPreview(RectTransform host, ResMgr res, bool spin = true, bool sideView = false, int rtSize = 640)
    {
        this.res = res;
        this.spin = spin;
        this.sideView = sideView;
        this.rtSize = Mathf.Max(64, rtSize);
        origin = new Vector3(s_count++ * 1000f, 5000f, 0f); // 间距 1000 远大于相机远裁剪 50,彼此看不到

        image = host.GetComponent<RawImage>();
        if (image == null) image = host.gameObject.AddComponent<RawImage>();
        image.raycastTarget = false;
        image.enabled = false;
    }

    /// <summary>展示指定 Resources 路径的武器模型(无扩展名)。传空则清空隐藏。</summary>
    public void Show(string modelPath)
    {
        if (string.IsNullOrEmpty(modelPath)) { Clear(); return; }

        EnsureRig();
        if (modelPath == shownPath && model != null) { image.enabled = true; return; }

        ClearModel();
        shownPath = modelPath;          // 立即占位,避免回调竞态
        int v = ++showVersion;
        if (res == null) { image.enabled = false; return; }
        res.LoadAsync<GameObject>(modelPath, prefab =>
        {
            if (v != showVersion || rig == null) // 期间又切了模型 / rig 已销毁:丢弃并配平
            {
                if (prefab != null) res.Release<GameObject>(modelPath);
                return;
            }
            if (prefab == null)
            {
                Debug.LogWarning($"[WeaponModelPreview] 模型未找到(确认已在 Resources/ 下): {modelPath}");
                image.enabled = false;
                return;
            }

            model = Object.Instantiate(prefab, pivot);
            res.Release<GameObject>(modelPath); // 实例已建,释放 prefab 引用(配平 LoadAsync 的 +1)
            // 动物预制体带弱点高亮/命中盒(AnimalHitZone),UI 预览里不该显示:整块关掉
            foreach (var hz in model.GetComponentsInChildren<AnimalHitZone>(true)) hz.gameObject.SetActive(false);
            WeaponModelUtil.SetLayer(model, PreviewLayer);
            WeaponModelUtil.DisableColliders(model);
            WeaponModelUtil.MakeUnlit(model);
            WeaponModelUtil.Frame(cam, pivot, model); // 先居中 + 默认 3/4 取景
            if (sideView) SideFrame();                // 需要则改为正侧视图
            image.enabled = true;
        });
    }

    /// <summary>把相机摆到模型正侧面(垂直于较长水平轴看其侧面),略微抬高。</summary>
    private void SideFrame()
    {
        var rs = model.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);

        float radius = Mathf.Max(b.extents.magnitude, 0.05f);
        float dist = radius / Mathf.Sin(Mathf.Deg2Rad * cam.fieldOfView * 0.5f) * 1.15f;
        Vector3 dir = (b.size.z >= b.size.x) ? new Vector3(1f, 0.12f, 0f) : new Vector3(0f, 0.12f, 1f);
        dir = dir.normalized;
        cam.transform.position = b.center + dir * dist;
        cam.transform.LookAt(b.center);
    }

    /// <summary>显隐:整体启用/停用预览 rig(而非只关相机)。
    /// 注意必须切 rig 的 active 而不是 cam.enabled——后者被禁用再启用后 RenderTexture 不会重新渲染,
    /// 导致切走页签再回来时模型不再出现(只剩卡片描边)。切 GameObject 的 active 则会强制重渲一帧。</summary>
    public void SetActive(bool on)
    {
        if (rig != null && rig.activeSelf != on) rig.SetActive(on);
        if (image != null) image.enabled = on && model != null;
    }

    public void Clear()
    {
        showVersion++; // 取消在途加载,避免回调把模型又建出来
        ClearModel();
        shownPath = null;
        if (image != null) image.enabled = false;
    }

    public void Dispose()
    {
        ClearModel();
        if (rig != null) { Object.Destroy(rig); rig = null; cam = null; pivot = null; }
        if (rt != null) { rt.Release(); Object.Destroy(rt); rt = null; }
        if (image != null) image.enabled = false;
        shownPath = null;
    }

    private void ClearModel()
    {
        if (model != null) { Object.Destroy(model); model = null; }
    }

    /// <summary>懒建(并在被场景卸载销毁后重建)离屏相机 + 转台 + RenderTexture。</summary>
    private void EnsureRig()
    {
        if (rt == null)
        {
            rt = new RenderTexture(rtSize, rtSize, 16, RenderTextureFormat.ARGB32) { name = "WeaponPreviewRT" };
            rt.Create();
        }
        if (image != null) image.texture = rt;

        if (rig != null) return;

        // rig 被场景切换销毁后,其子物体(pivot/model)也没了:重置 shownPath 以便重新加载
        shownPath = null;
        model = null;

        rig = new GameObject("WeaponPreviewRig");
        rig.transform.position = origin;

        var pv = new GameObject("Pivot");
        pv.transform.SetParent(rig.transform, false);
        pivot = pv.transform;
        if (spin) pv.AddComponent<AutoRotate>(); // 不转的卡片不挂自转

        var camGo = new GameObject("PreviewCam");
        camGo.transform.SetParent(rig.transform, false);
        cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        // 侧视卡片用透明底(衬出卡片色);默认大预览用深色底板
        cam.backgroundColor = sideView ? new Color(0f, 0f, 0f, 0f) : new Color(0.10f, 0.11f, 0.13f, 0.85f);
        cam.cullingMask = 1 << PreviewLayer;                          // 只拍武器
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 50f;
        cam.targetTexture = rt;
    }
}
