using UnityEngine;
using YOTO;

/// <summary>
/// 开枪震屏:监听 <see cref="YOTOEventType.Shoot"/>,给相机一记短促位移抖动并衰减回位。
/// 只动 localPosition,与 <see cref="CameraSwipeLook"/> 改 rotation 互不干扰。由 <see cref="GameStartScene"/> 进对局时挂到主相机。
/// (框架里另有一个非 MonoBehaviour 的 CameraShake 用于旧 TPS 跟随相机的 kickback,两者用途不同,故本类单独命名。)
/// </summary>
public class ShootCameraShake : MonoBehaviour
{
    [Tooltip("单次抖动时长(秒)")]
    public float duration = 0.26f;
    [Tooltip("抖动幅度(米)")]
    public float magnitude = 0.55f;

    private EventMgr eventMgr;
    private Vector3 basePos;
    private float timeLeft;

    private void Awake() => basePos = transform.localPosition;

    private void OnEnable()
    {
        if (GameLoop.Instance != null && GameLoop.Instance.Ctx != null)
            eventMgr = GameLoop.Instance.Ctx.Get<EventMgr>();
        eventMgr?.Add(YOTOEventType.Shoot, OnShoot);
    }

    private void OnDisable()
    {
        eventMgr?.Remove(YOTOEventType.Shoot, OnShoot);
        transform.localPosition = basePos;
    }

    private void OnShoot() => timeLeft = duration;

    private void LateUpdate()
    {
        if (timeLeft <= 0f) return;
        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f)
        {
            transform.localPosition = basePos;
            return;
        }
        float damper = timeLeft / duration; // 1→0 衰减
        Vector3 off = Random.insideUnitSphere * (magnitude * damper);
        off.z = 0f; // 不前后推,只在屏幕平面抖
        transform.localPosition = basePos + off;
    }
}
