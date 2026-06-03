using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YOTO;

/// <summary>
/// 一次性「模型快照」:用一台共用的离屏相机把 Resources 里的模型渲染**一帧**到 Texture2D 返回,然后相机闲置(不常驻渲染)。
/// 适合需要很多静态小图的场景(如装备选择列表):比每张卡挂一台常驻相机省得多。
/// URP 用 <see cref="RenderPipeline.SubmitRenderRequest"/> 同步渲染单帧(Unity 2022.2+);失败回退 Camera.Render。
///
/// 注意:同步 ReadPixels 会有一次性 GPU→CPU 等待,批量生成时集中在打开界面那一帧,属一次性开销。
/// 返回的 Texture2D 由调用方负责 Destroy 回收。
/// </summary>
public static class ModelSnapshot
{
    private const int PreviewLayer = 18; // 复用 WeaponPreview 层
    private static readonly Vector3 RigOrigin = new Vector3(8000f, 8000f, 0f); // 远离场景与其它预览 rig

    private static GameObject rig;
    private static Transform pivot;
    private static Camera cam;

    /// <summary>把模型渲一帧成 Texture2D(侧视、不旋转)。失败返回 null。<paramref name="bg"/> 用透明可衬卡片底色。</summary>
    public static Texture2D Capture(string modelPath, ResMgr res, int size = 256, bool sideView = true, Color bg = default)
    {
        if (string.IsNullOrEmpty(modelPath) || res == null) return null;
        var prefab = res.Load<GameObject>(modelPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[ModelSnapshot] 模型未找到: {modelPath}");
            return null;
        }

        EnsureRig();

        var model = Object.Instantiate(prefab, pivot);
        // 弱点高亮/命中盒不入镜
        foreach (var hz in model.GetComponentsInChildren<AnimalHitZone>(true)) hz.gameObject.SetActive(false);
        WeaponModelUtil.SetLayer(model, PreviewLayer);
        WeaponModelUtil.DisableColliders(model);
        WeaponModelUtil.MakeUnlit(model);
        WeaponModelUtil.Frame(cam, pivot, model);     // 居中 + 默认取景
        if (sideView) FrameSide(model);                // 改正侧视图

        var rt = RenderTexture.GetTemporary(size, size, 16, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.backgroundColor = bg;

        // —— 同步渲一帧 ——
        var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
        if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
        else cam.Render(); // 非 URP 回退

        // —— 读回像素 ——
        var prevActive = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        tex.Apply();
        RenderTexture.active = prevActive;

        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);

        // 清理:MakeUnlit 新建的材质 + 模型实例,避免泄漏(同步销毁,以免下一次快照重叠入镜)
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.sharedMaterials)
                if (m != null) Object.DestroyImmediate(m);
        Object.DestroyImmediate(model);

        return tex;
    }

    private static void EnsureRig()
    {
        if (rig != null && cam != null) return; // 注意:场景切换销毁后 Unity 假 null,会重建

        rig = new GameObject("ModelSnapshotRig") { hideFlags = HideFlags.HideAndDontSave };
        rig.transform.position = RigOrigin;

        var pv = new GameObject("Pivot");
        pv.transform.SetParent(rig.transform, false);
        pivot = pv.transform;

        var camGo = new GameObject("SnapshotCam");
        camGo.transform.SetParent(rig.transform, false);
        cam = camGo.AddComponent<Camera>();
        cam.enabled = false; // 不常驻渲染,只在 SubmitRenderRequest 时渲一帧
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.cullingMask = 1 << PreviewLayer;
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 50f;
    }

    /// <summary>把相机摆到模型正侧面(垂直于较长水平轴),略抬高。</summary>
    private static void FrameSide(GameObject model)
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
}
