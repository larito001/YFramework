using UnityEngine;

/// <summary>
/// 武器模型展示的公用处理:设层 / 换 Unlit 自发光材质 / 去碰撞体 / 按包围盒取景。
/// 被 <see cref="WeaponModelPreview"/>(UI 离屏预览)与 <see cref="FpsWeaponViewModel"/>(第一人称手持)共用。
/// </summary>
public static class WeaponModelUtil
{
    /// <summary>递归设置层。</summary>
    public static void SetLayer(GameObject go, int layer)
    {
        go.layer = layer;
        var t = go.transform;
        for (int i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i).gameObject, layer);
    }

    /// <summary>关掉模型上的所有碰撞体(LowPolyWeapons 预制体自带 MeshCollider,展示/手持时不该参与物理或射线)。</summary>
    public static void DisableColliders(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
    }

    /// <summary>Resources 下的 URP/Unlit 模板材质路径(去后缀)。</summary>
    private const string UnlitTemplatePath = "Materials/UnlitPreview";
    private static Material unlitTemplate;

    /// <summary>
    /// 把材质换成 Unlit(保留主纹理/颜色),使其不依赖任何灯光即可清晰显示(UI 离屏预览 / 第一人称手持)。
    ///
    /// **真机务必走模板材质**:运行时 <c>new Material(Shader.Find("URP/Unlit"))</c> 没有任何序列化资产引用该 shader 变体,
    /// 打包时会被 URP 的变体裁剪删掉 → 真机渲染成品红(editor 因为全量变体在所以正常)。
    /// 改为从 <c>Resources/Materials/UnlitPreview.mat</c>(序列化资产,变体会被打包器收集)<c>Instantiate</c>,
    /// 只改主纹理/颜色不动 keyword,保持同一变体,真机即可正常显示。模板缺失时退回 Shader.Find(仅作 editor/兜底)。
    /// </summary>
    public static void MakeUnlit(GameObject go)
    {
        if (unlitTemplate == null) unlitTemplate = Resources.Load<Material>(UnlitTemplatePath);

        Shader fallbackShader = null;
        if (unlitTemplate == null)
        {
            fallbackShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (fallbackShader == null) fallbackShader = Shader.Find("Unlit/Texture");
            if (fallbackShader == null) return;
        }

        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var srcs = r.sharedMaterials;
            var dsts = new Material[srcs.Length];
            for (int i = 0; i < srcs.Length; i++)
            {
                var m = unlitTemplate != null ? new Material(unlitTemplate) : new Material(fallbackShader);
                var src = srcs[i];
                if (src != null)
                {
                    if (src.mainTexture != null) m.mainTexture = src.mainTexture;
                    else if (src.HasProperty("_BaseColor")) m.color = src.GetColor("_BaseColor");
                    else m.color = src.color;
                }
                dsts[i] = m;
            }
            r.sharedMaterials = dsts;
        }
    }

    /// <summary>把模型包围盒中心对齐到 pivot,并按其大小自动拉远相机取景(前-右-上 3/4 视角)。</summary>
    public static void Frame(Camera cam, Transform pivot, GameObject model)
    {
        if (cam == null || pivot == null || model == null) return;

        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        var b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

        model.transform.position += pivot.position - b.center; // 中心对齐 pivot

        float radius = Mathf.Max(b.extents.magnitude, 0.05f);
        float dist = radius / Mathf.Sin(Mathf.Deg2Rad * cam.fieldOfView * 0.5f) * 1.15f;
        Vector3 dir = new Vector3(0.6f, 0.35f, -1f).normalized;
        cam.transform.position = pivot.position + dir * dist;
        cam.transform.LookAt(pivot.position);
    }
}
