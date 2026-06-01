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

    /// <summary>把材质换成 Unlit(保留主纹理/颜色),使其不依赖任何灯光即可清晰显示(UI 预览用)。</summary>
    public static void MakeUnlit(GameObject go)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Texture");
        if (shader == null) return;

        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var srcs = r.sharedMaterials;
            var dsts = new Material[srcs.Length];
            for (int i = 0; i < srcs.Length; i++)
            {
                var m = new Material(shader);
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
