using UnityEngine;

/// <summary>
/// 挂在动物某个部位碰撞体上的标记:告诉射击判定这块碰撞体是 头/心脏/身体(<see cref="HitZone"/>)。
/// 由 AnimalPrefabBuilder 在生成动物 prefab 时按包围盒切出的三个 BoxCollider 上各挂一个。
/// 射线命中该碰撞体后,<see cref="ScopeAimController.FireRay"/> 据此拿到部位 + 所属 <see cref="AnimalEntity"/>。
/// </summary>
[RequireComponent(typeof(Collider))]
public class AnimalHitZone : MonoBehaviour
{
    public HitZone zone = HitZone.Body;

    private AnimalEntity owner;
    private Renderer glow; // 弱点高亮盒(子物体 "Glow" 的 Renderer);AnimalPrefabBuilder.AddGlow 生成

    /// <summary>所属动物(向上找一次并缓存)。</summary>
    public AnimalEntity Owner => owner != null ? owner : (owner = GetComponentInParent<AnimalEntity>());

    private void Awake()
    {
        glow = GetComponentInChildren<Renderer>(true); // 部位盒下唯一的渲染器就是 Glow 高亮盒
        if (glow != null) glow.enabled = false;        // 默认隐藏:不开镜不显示,身体永不显示
    }

    /// <summary>设置弱点高亮显隐:仅头/心脏可显示(开镜时),身体永不显示。</summary>
    public void SetHighlight(bool show)
    {
        if (glow != null) glow.enabled = show && (zone == HitZone.Head || zone == HitZone.Heart);
    }
}
