using UnityEngine;

/// <summary>命中部位:由动物身上的 <see cref="AnimalHitZone"/> 碰撞体标记,决定一枪是否致命。</summary>
public enum HitZone
{
    Body,   // 身体:非致命,累计 <see cref="AnimalEntity.bodyHitsToKill"/> 枪才死
    Head,   // 头:一枪致命
    Heart,  // 心脏:一枪致命
}

/// <summary>
/// 场景中的动物实例(低多边形动物模型,平时只播 idle)。持有配表 id 与击杀积分,
/// 被射击命中时按部位结算:<see cref="Hit"/> 头/心脏一枪致命、身体两枪;致命时 <see cref="Kill"/> 播死亡动画并延时销毁。
/// 三个部位碰撞体(头/心脏/身体)由 AnimalPrefabBuilder 按包围盒切分生成,各挂 <see cref="AnimalHitZone"/>。
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

    [Tooltip("击中身体需要的枪数(头/心脏无视此值,一枪即死)")]
    public int bodyHitsToKill = 2;

    [Tooltip("死亡动画对应的 Animator bool 参数名;不同动物可能不同")]
    public string deathBool = "isDead";
    [Tooltip("死亡动画时长(秒),播完后销毁")]
    public float deathDuration = 2f;

    public bool IsDead { get; private set; }

    private int bodyHits;       // 已累计的身体中弹数
    private Animator animator;

    private void Awake() => animator = GetComponentInChildren<Animator>();

    /// <summary>
    /// 按部位结算一次命中:头/心脏一枪致命;身体累计到 <see cref="bodyHitsToKill"/> 枪才致命。
    /// 返回本次是否致命(true=这一枪打死了)。已死再调用安全(返回 false)。
    /// </summary>
    public bool Hit(HitZone zone)
    {
        if (IsDead) return false;

        if (zone == HitZone.Head || zone == HitZone.Heart)
        {
            Kill();
            return true;
        }

        // 身体:累计中弹,达到阈值才死
        bodyHits++;
        if (bodyHits >= bodyHitsToKill)
        {
            Kill();
            return true;
        }
        return false;
    }

    /// <summary>被击杀:停止可再命中、播死亡动画、延时销毁。重复调用安全。</summary>
    public void Kill()
    {
        if (IsDead) return;
        IsDead = true;

        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = false; // 死了就不再接受命中

        if (animator != null && !string.IsNullOrEmpty(deathBool))
            animator.SetBool(deathBool, true);

        Destroy(gameObject, deathDuration);
    }
}
