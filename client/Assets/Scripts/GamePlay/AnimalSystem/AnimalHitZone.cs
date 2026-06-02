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

    /// <summary>所属动物(向上找一次并缓存)。</summary>
    public AnimalEntity Owner => owner != null ? owner : (owner = GetComponentInParent<AnimalEntity>());
}
