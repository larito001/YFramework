using UnityEngine;

/// <summary>
/// 场景中的动物实例(低多边形动物模型,平时只播 idle)。持有配表 id 与击杀积分,
/// 被射击命中时 <see cref="Kill"/> 播死亡动画并延时销毁。
///
/// 死亡动画各动物的 Animator 参数名不一样(Goose/Boar 用 isDead,Rabbit 用 isDead_0),
/// 所以把参数名挂在 prefab 上(由 AnimalPrefabBuilder 按动物填),通用战斗代码不必关心。
/// </summary>
public class AnimalEntity : MonoBehaviour
{
    public int animalId;
    public int score;

    [Tooltip("死亡动画对应的 Animator bool 参数名;不同动物可能不同")]
    public string deathBool = "isDead";
    [Tooltip("死亡动画时长(秒),播完后销毁")]
    public float deathDuration = 2f;

    public bool IsDead { get; private set; }

    private Animator animator;

    private void Awake() => animator = GetComponentInChildren<Animator>();

    /// <summary>被命中击杀:停止可再命中、播死亡动画、延时销毁。重复调用安全。</summary>
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
