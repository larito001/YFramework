using UnityEngine;

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

    private Camera cam;
    private CameraSwipeLook look;

    private float normalFov;        // 进对局时的原始视野,退出瞄准时回到它
    private float baseSensitivity;  // CameraSwipeLook 原始灵敏度
    private float targetFov;
    private bool aiming;

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
    }

    /// <summary>从屏幕中心打射线,返回命中的动物(没打中动物返回 null)。</summary>
    public AnimalEntity FireRay()
    {
        var ray = cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
        if (Physics.Raycast(ray, out var hit, rayDistance))
            return hit.collider.GetComponentInParent<AnimalEntity>(); // 最近命中物不是动物(如地面)则为 null = 没打中
        return null;
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
