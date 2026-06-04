using UnityEngine;

/// <summary>
/// 让动物在出生点附近随意走动:idle 停留一会儿 → 随机选个目标点 → 转向并朝它走(射线贴地)→ 到点再 idle,如此循环。
/// 移动时把行走动画 bool 置 true、停下置 false;行走参数名按各动物 Animator 自动探测(野鸭/野猪=isWalking,野兔=isJumping)。
/// 死亡(<see cref="AnimalEntity.IsDead"/>)后立即停下、不再移动。
///
/// 不依赖 NavMesh(动物预制体已被 AnimalPrefabBuilder 剥掉 NavMeshAgent),纯 transform + 向下射线贴地,
/// 适配没有烘焙导航网格的随机地形。贴地射线会跳过动物自身/其它动物的碰撞体,只认地面。
/// 由 <see cref="YOTO.AnimalSystem.SpawnWave"/> 在生成每只动物时挂载,无需重建预制体。
/// </summary>
[RequireComponent(typeof(AnimalEntity))]
public class AnimalWander : MonoBehaviour
{
    [Tooltip("移动速度(米/秒)")]
    public float moveSpeed = 1.5f;
    [Tooltip("转向速度(度/秒)")]
    public float turnSpeed = 240f;
    [Tooltip("游走半径(米):目标点离出生点的最大距离")]
    public float wanderRadius = 6f;
    [Tooltip("到点后 idle 停留的最短/最长秒数")]
    public float pauseMin = 1.5f;
    public float pauseMax = 4f;
    [Tooltip("判定到达目标的水平距离(米)")]
    public float arriveDistance = 0.35f;

    [Header("惊慌(被枪声惊扰时:加速狂奔,到点不停顿立即换向)")]
    [Tooltip("惊慌时的移动速度倍率")]
    public float panicSpeedMul = 3f;
    [Tooltip("惊慌时游走半径倍率(乱跑范围更大)")]
    public float panicRadiusMul = 2f;

    // 行走动画候选 bool 参数名,按优先级探测(此美术包:多数动物 isWalking,兔子用 isJumping 触发 hop)
    private static readonly string[] WalkParamCandidates = { "isWalking", "isJumping" };
    private static readonly RaycastHit[] HitBuffer = new RaycastHit[8];

    private AnimalEntity entity;
    private Animator animator;
    private string walkParam;   // 探测到的行走 bool 参数名(空=该动物无行走动画,只移动不切动画)
    private Vector3 home;       // 出生点,游走围绕它
    private Vector3 target;     // 当前目标点
    private float pauseTimer;   // >0 表示正在 idle 停留
    private float panicTimer;   // >0 表示正处于惊慌狂奔状态(倒计时到 0 自动恢复)
    private bool moving;

    private bool Panicking => panicTimer > 0f;

    /// <summary>被枪声惊扰:进入(或刷新到)<paramref name="duration"/> 秒的惊慌狂奔。死亡个体忽略。</summary>
    public void Panic(float duration)
    {
        if (entity != null && entity.IsDead) return;
        panicTimer = Mathf.Max(panicTimer, duration); // 重复惊扰取最长,不缩短
        pauseTimer = 0f;   // 打断当前 idle 停留,立刻起步
        PickTarget();
        SetMoving(true);
    }

    private void Awake()
    {
        entity = GetComponent<AnimalEntity>();
        animator = GetComponentInChildren<Animator>();
        home = transform.position;
        ResolveWalkParam();
        PickPause(); // 出生先 idle 一小会儿再走,避免整波同时起步
    }

    private void Update()
    {
        // 死亡:停下并交给 AnimalEntity 播死亡动画,不再干预
        if (entity != null && entity.IsDead)
        {
            if (moving) SetMoving(false);
            enabled = false;
            return;
        }

        if (panicTimer > 0f) panicTimer -= Time.deltaTime; // 惊慌倒计时,归零自动恢复常态

        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.deltaTime;
            if (pauseTimer <= 0f) { PickTarget(); SetMoving(true); }
            return;
        }

        Vector3 flat = target - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude <= arriveDistance * arriveDistance)
        {
            PickPause(); // 到点:转 idle 停留(惊慌时不停顿,立即换向继续狂奔)
            return;
        }

        // 朝目标平滑转向
        Quaternion want = Quaternion.LookRotation(flat.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);

        // 沿朝向前进并贴地(惊慌时提速)
        float spd = moveSpeed * (Panicking ? panicSpeedMul : 1f);
        Vector3 next = transform.position + transform.forward * spd * Time.deltaTime;
        next.y = GroundY(next, transform.position.y);
        transform.position = next;
    }

    private void PickTarget()
    {
        float radius = wanderRadius * (Panicking ? panicRadiusMul : 1f);
        Vector2 r = Random.insideUnitCircle * radius;
        Vector3 p = home + new Vector3(r.x, 0f, r.y);
        p.y = GroundY(p, transform.position.y);
        target = p;
    }

    private void PickPause()
    {
        // 惊慌时不停顿:到点立刻换个目标继续狂奔
        if (Panicking) { PickTarget(); SetMoving(true); return; }
        pauseTimer = Random.Range(pauseMin, pauseMax);
        SetMoving(false);
    }

    private void SetMoving(bool on)
    {
        moving = on;
        if (animator != null && !string.IsNullOrEmpty(walkParam))
            animator.SetBool(walkParam, on);
    }

    /// <summary>按候选名在 Animator 里找出第一个存在的行走 bool 参数(找不到则留空,只移动不切动画)。</summary>
    private void ResolveWalkParam()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        var ps = animator.parameters;
        for (int c = 0; c < WalkParamCandidates.Length; c++)
            for (int i = 0; i < ps.Length; i++)
                if (ps[i].type == AnimatorControllerParameterType.Bool && ps[i].name == WalkParamCandidates[c])
                {
                    walkParam = WalkParamCandidates[c];
                    return;
                }
    }

    /// <summary>从点上方往下打射线贴地:取最高的非动物碰撞体命中点 y;打不到地面则保持 fallback(无地面碰撞体时不掉下去)。</summary>
    private float GroundY(Vector3 p, float fallback)
    {
        int n = Physics.RaycastNonAlloc(p + Vector3.up * 5f, Vector3.down, HitBuffer, 20f, ~0, QueryTriggerInteraction.Ignore);
        float bestY = fallback;
        float bestDist = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            // 跳过动物自身与其它动物的碰撞体,只认地面/场景
            if (HitBuffer[i].collider.GetComponentInParent<AnimalEntity>() != null) continue;
            if (HitBuffer[i].distance < bestDist) { bestDist = HitBuffer[i].distance; bestY = HitBuffer[i].point.y; }
        }
        return bestY;
    }
}
