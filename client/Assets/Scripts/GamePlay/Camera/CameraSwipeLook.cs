using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 滑屏旋转相机:在游戏画面上按住拖动(鼠标/触摸)即左右环视(yaw)、上下俯仰(pitch,夹角限制)。
/// 由 <see cref="GameStartScene"/> 进入对局时挂到主相机上。
///
/// 门控:
///   - 拖拽起点落在 UI 上(瞄准/射击按钮等)不触发——点按钮就是点按钮;
///   - 仅在游戏输入开启且战斗输入未被屏蔽时生效(背包/商店/设置等面板打开 → CombatEnabled=false → 不环视),
///     未能取到 InputService 时退化为「只要不点在 UI 上就能转」。
/// 触摸即映射到鼠标按键 0,故编辑器(鼠标拖)与真机(手指滑)同一套代码。
/// </summary>
public class CameraSwipeLook : MonoBehaviour
{
    [Tooltip("灵敏度:度/像素")]
    public float sensitivity = 0.15f;
    public float minPitch = -70f;
    public float maxPitch = 70f;

    private InputService input;
    private float yaw;
    private float pitch;
    private bool dragging;
    private Vector2 lastPos;

    private void Start()
    {
        if (GameLoop.Instance != null && GameLoop.Instance.Ctx != null)
            input = GameLoop.Instance.Ctx.Get<InputService>();

        var e = transform.eulerAngles;
        yaw = e.y;
        pitch = Normalize(e.x);
    }

    private void Update()
    {
        // 仅游戏中且战斗输入未屏蔽时允许环视(面板打开时 CombatInputGate 会置 CombatEnabled=false)
        bool allowed = input == null || (input.IsEnabled && input.CombatEnabled);
        if (!allowed)
        {
            dragging = false;
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (!IsOverUI()) { dragging = true; lastPos = Input.mousePosition; } // 起点在 UI 上则不接管
        }
        else if (!Input.GetMouseButton(0))
        {
            dragging = false;
        }

        if (dragging)
        {
            Vector2 cur = Input.mousePosition;
            Vector2 d = cur - lastPos;
            lastPos = cur;
            yaw += d.x * sensitivity;
            pitch = Mathf.Clamp(pitch - d.y * sensitivity, minPitch, maxPitch);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }

    private static float Normalize(float angle) => angle > 180f ? angle - 360f : angle;

    private bool IsOverUI()
    {
        var es = EventSystem.current;
        if (es == null) return false;
        if (Input.touchCount > 0) return es.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        return es.IsPointerOverGameObject();
    }
}
