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

    private RenderTexture rt;
    private Camera cam;
    private GameObject rig;
    private Transform pivot;
    private GameObject model;
    private string shownPath;

    public WeaponModelPreview(RectTransform host, ResMgr res)
    {
        this.res = res;
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
        var prefab = res != null ? res.Load<GameObject>(modelPath) : null;
        if (prefab == null)
        {
            Debug.LogWarning($"[WeaponModelPreview] 模型未找到(确认已在 Resources/ 下): {modelPath}");
            image.enabled = false;
            return;
        }

        model = Object.Instantiate(prefab, pivot);
        // 动物预制体带弱点高亮/命中盒(AnimalHitZone),UI 预览里不该显示:整块关掉
        foreach (var hz in model.GetComponentsInChildren<AnimalHitZone>(true)) hz.gameObject.SetActive(false);
        WeaponModelUtil.SetLayer(model, PreviewLayer);
        WeaponModelUtil.DisableColliders(model);
        WeaponModelUtil.MakeUnlit(model);
        WeaponModelUtil.Frame(cam, pivot, model);
        shownPath = modelPath;
        image.enabled = true;
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
            rt = new RenderTexture(640, 640, 16, RenderTextureFormat.ARGB32) { name = "WeaponPreviewRT" };
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
        pv.AddComponent<AutoRotate>();

        var camGo = new GameObject("PreviewCam");
        camGo.transform.SetParent(rig.transform, false);
        cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.10f, 0.11f, 0.13f, 0.85f); // 深色底板(想要透明改 alpha=0)
        cam.cullingMask = 1 << PreviewLayer;                          // 只拍武器
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 50f;
        cam.targetTexture = rt;
    }
}
