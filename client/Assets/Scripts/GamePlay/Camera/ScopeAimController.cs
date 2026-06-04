using UnityEngine;

/// <summary>一次射击命中结果:命中的动物(没打中为 null)+ 命中部位 + 子弹落点(供惊扰判定)。</summary>
public struct AnimalShot
{
    public AnimalEntity entity;
    public HitZone zone;
    public Vector3 point; // 子弹落点(射线命中处:动物身上 / 地面 / 场景);rayHit=false 时无意义
    public bool rayHit;   // 射线是否打到任何东西(打空到天空为 false)
    public bool HasHit => entity != null;
}

/// <summary>
/// 瞄准镜的相机端机制:瞄准时拉近视野(变焦放大)并降低环视灵敏度;开火时从屏幕中心(准星处)打一条射线做命中判定。
/// 由 <see cref="GameStartScene"/> 进对局时和 <see cref="CameraSwipeLook"/>/<see cref="ShootCameraShake"/> 一起挂到主相机。
///
/// 只管「相机怎么变 + 打中了谁」,不管 UI 黑边遮罩/准星显隐/积分——那些在 <see cref="GameMainPanel"/> 里。
/// 透视相机调 fieldOfView,正交相机调 orthographicSize,两种都支持。
/// </summary>
[RequireComponent(typeof(Camera))]
public class ScopeAimController : MonoBehaviour
{
    [Tooltip("瞄准时的视野(透视相机 FOV / 正交相机 size),越小越放大")]
    public float aimFov = 25f;
    [Tooltip("视野变焦的插值速度")]
    public float zoomSpeed = 12f;
    [Tooltip("瞄准时环视灵敏度倍率(放大后手感更稳)")]
    public float aimSensitivityScale = 0.4f;
    [Tooltip("命中判定的最大距离(米)")]
    public float rayDistance = 500f;

    [Header("瞄准晃动(正弦呼吸晃动;幅度由所选瞄准镜配表 aimSway 决定,越好的镜越小)")]
    [Tooltip("晃动幅度(度);0=不晃。由 SetSwayAmplitude 按所选瞄准镜设置")]
    public float swayAmplitude = 0f;
    [Tooltip("晃动频率(弧度/秒):水平方向")]
    public float swayFrequency = 1.6f;
    [Tooltip("垂直晃动相对水平的幅度比(<1 → 竖向更小,呈横向 8 字飘移)")]
    public float swayVerticalRatio = 0.6f;

    private Camera cam;
    private CameraSwipeLook look;

    private float normalFov;        // 进对局时的原始视野,退出瞄准时回到它
    private float baseSensitivity;  // CameraSwipeLook 原始灵敏度
    private float targetFov;
    private bool aiming;

    /// <summary>当前是否在瞄准(供手持武器等据此显隐)。</summary>
    public bool IsAiming => aiming;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        look = GetComponent<CameraSwipeLook>();
        normalFov = targetFov = CurrentFov;
        if (look != null) baseSensitivity = look.sensitivity;
    }

    /// <summary>进入/退出瞄准:切换目标视野与环视灵敏度。</summary>
    public void SetAiming(bool on)
    {
        if (aiming == on) return;
        aiming = on;
        targetFov = on ? aimFov : normalFov;
        if (look != null) look.sensitivity = baseSensitivity * (on ? aimSensitivityScale : 1f);
        // 退出瞄准:抹掉残留的晃动偏移,回到纯环视朝向(否则上一帧的正弦偏移会被冻结)
        if (!on && look != null) transform.rotation = look.LookRotation;
    }

    /// <summary>按所选瞄准镜设置晃动幅度(度)。配表 aimSway:越好的镜越小;0=不晃。</summary>
    public void SetSwayAmplitude(float amplitude) => swayAmplitude = Mathf.Max(0f, amplitude);

    /// <summary>瞄准时:在环视基准朝向上叠加正弦呼吸晃动(横向 8 字飘移)。LateUpdate 里做,确保在环视写入之后,成为最终朝向。</summary>
    private void LateUpdate()
    {
        if (!aiming || look == null || swayAmplitude <= 0f) return;
        float t = Time.time;
        float yawSway = swayAmplitude * Mathf.Sin(t * swayFrequency);
        float pitchSway = swayAmplitude * swayVerticalRatio * Mathf.Sin(t * swayFrequency * 0.85f + 1.3f);
        transform.rotation = look.LookRotation * Quaternion.Euler(pitchSway, yawSway, 0f);
    }

    /// <summary>
    /// 从屏幕中心打射线,返回命中的动物 + 部位(头/心脏/身体);没打中动物则 <see cref="AnimalShot.HasHit"/> 为 false。
    /// 命中碰撞体若挂了 <see cref="AnimalHitZone"/> 取其部位;否则(旧 prefab 只有整体碰撞体)按身体处理。
    /// </summary>
    public AnimalShot FireRay()
    {
        var ray = cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
        if (Physics.Raycast(ray, out var hit, rayDistance))
        {
            var zoneComp = hit.collider.GetComponent<AnimalHitZone>();
            if (zoneComp != null && zoneComp.Owner != null)
                return new AnimalShot { entity = zoneComp.Owner, zone = zoneComp.zone, point = hit.point, rayHit = true };

            var ent = hit.collider.GetComponentInParent<AnimalEntity>(); // 兼容无部位碰撞体的旧 prefab
            if (ent != null)
                return new AnimalShot { entity = ent, zone = HitZone.Body, point = hit.point, rayHit = true };

            // 打到地面/场景(没打中动物):entity 为 null,但仍记录落点供惊扰判定
            return new AnimalShot { entity = null, zone = HitZone.Body, point = hit.point, rayHit = true };
        }
        return default; // 射线打空(如天空):rayHit=false
    }

    private void Update()
    {
        if (Mathf.Abs(CurrentFov - targetFov) < 0.01f) return;
        SetFov(Mathf.Lerp(CurrentFov, targetFov, Time.deltaTime * zoomSpeed));
    }

    private float CurrentFov => cam.orthographic ? cam.orthographicSize : cam.fieldOfView;

    private void SetFov(float v)
    {
        if (cam.orthographic) cam.orthographicSize = v;
        else cam.fieldOfView = v;
    }
}
