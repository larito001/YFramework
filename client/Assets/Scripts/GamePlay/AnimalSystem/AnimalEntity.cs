using UnityEngine;

/// <summary>命中部位:由动物身上的 <see cref="AnimalHitZone"/> 碰撞体标记,金色猎物的身体部位据此区分子弹等级差异。</summary>
public enum HitZone
{
    Body,   // 身体:普通猎物仍一枪死;金色猎物按子弹等级——空尖弹(2级)1 发、标准弹(1级)<see cref="AnimalEntity.goldenBodyHitsL1"/> 发
    Head,   // 头:一枪致命(普通/金色通用)
    Heart,  // 心脏:一枪致命(普通/金色通用)
}

/// <summary>
/// 场景中的动物实例(低多边形动物模型,平时只播 idle)。持有配表 id 与击杀积分,被射击命中时按 <see cref="Hit"/> 结算:
///   · 普通(非金色)猎物:打哪里、任何子弹都一枪死;
///   · 金色猎物:头/心脏任何子弹一枪死,身体部位空尖弹(2级)1 发、标准弹(1级)三发。
/// 致命时 <see cref="Kill"/> 播死亡动画并延时销毁。三个部位碰撞体(头/心脏/身体)由 AnimalPrefabBuilder 按包围盒切分生成,各挂 <see cref="AnimalHitZone"/>。
///
/// 死亡动画各动物的 Animator 参数名不一样(Goose/Boar 用 isDead,Rabbit 用 isDead_0),
/// 所以把参数名挂在 prefab 上(由 AnimalPrefabBuilder 按动物填),通用战斗代码不必关心。
/// </summary>
public class AnimalEntity : MonoBehaviour
{
    public int animalId;
    public int score;

    [Tooltip("是否为金色泛光稀有体(刷怪时按概率赋予,纯表现;后续可据此加积分/掉落)")]
    public bool isGolden;

    [Tooltip("金色猎物身体部位:标准弹(1级)需累计的枪数;空尖弹(2级)恒为 1 发。仅对金色生效——普通猎物打哪里都一枪死")]
    public int goldenBodyHitsL1 = 3;

    [Tooltip("死亡动画对应的 Animator bool 参数名;不同动物可能不同")]
    public string deathBool = "isDead";
    [Tooltip("死亡动画时长(秒),播完后销毁")]
    public float deathDuration = 2f;

    public bool IsDead { get; private set; }

    private int bodyHits;       // 已累计的身体中弹数
    private Animator animator;

    private void Awake() => animator = GetComponentInChildren<Animator>();

    /// <summary>头顶世界坐标(渲染包围盒顶部略上方),供击杀飘字定位。跳过弱点高亮盒,取不到渲染器则回退到身体上方。</summary>
    public Vector3 HeadTopWorld()
    {
        bool any = false;
        Bounds b = default;
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r.GetComponentInParent<AnimalHitZone>() != null) continue; // 跳过部位高亮盒
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (!any) return transform.position + Vector3.up;
        return new Vector3(b.center.x, b.max.y + 0.3f, b.center.z);
    }

    /// <summary>
    /// 结算一次命中(<paramref name="bulletLevel"/>:1=标准弹 / 2=空尖弹):
    ///   · 普通(非金色)猎物:任何部位、任何子弹一枪即死;
    ///   · 金色猎物:头/心脏任何子弹一枪即死;身体部位空尖弹(2级)1 发、标准弹(1级)<see cref="goldenBodyHitsL1"/> 发。
    /// 返回本次是否致命(true=这一枪打死了)。已死再调用安全(返回 false)。
    /// </summary>
    public bool Hit(HitZone zone, int bulletLevel)
    {
        if (IsDead) return false;

        // 普通猎物:打哪里都一枪死
        if (!isGolden) { Kill(); return true; }

        // 金色猎物:头/心脏任何子弹一枪死
        if (zone == HitZone.Head || zone == HitZone.Heart) { Kill(); return true; }

        // 金色猎物身体:子弹等级决定所需枪数(空尖弹 2 级=1 发;标准弹 1 级=goldenBodyHitsL1 发)
        int need = bulletLevel >= 2 ? 1 : Mathf.Max(1, goldenBodyHitsL1);
        bodyHits++;
        if (bodyHits >= need) { Kill(); return true; }
        return false;
    }

    /// <summary>被击杀:停止可再命中、播死亡动画、延时销毁。重复调用安全。</summary>
    public void Kill()
    {
        if (IsDead) return;
        IsDead = true;

        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = false; // 死了就不再接受命中

        foreach (var hz in GetComponentsInChildren<AnimalHitZone>())
            hz.SetHighlight(false); // 死亡即熄灭弱点高亮(避免死后仍亮着)

        if (animator != null && !string.IsNullOrEmpty(deathBool))
            animator.SetBool(deathBool, true);

        Destroy(gameObject, deathDuration);
    }
}
