using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 滑屏旋转相机:在游戏画面上按住拖动(鼠标/触摸)即左右环视(yaw)、上下俯仰(pitch,夹角限制)。
/// 由 <see cref="GameStartScene"/> 进入对局时挂到主相机上。
///
/// 真机直接读 <see cref="Input.touches"/>(按 fingerId 锁定单指),不依赖「触摸模拟鼠标」——
/// 后者在部分 Android 设备上不稳定,曾导致编辑器(鼠标)能转、真机滑屏不动。编辑器/桌面无触摸时回退到鼠标。
///
/// 门控:
///   - 起点落在 UI(瞄准/射击按钮等)上时不会立刻接管,而是要求滑动超过 <see cref="uiDragThreshold"/>
///     像素才开始环视——点按钮就是点按钮,从按钮上甩出去才算环视;空白处起手则立即生效。
///     这样即便某一帧 EventSystem 的 UI 命中判断不准(触摸按下帧 IsPointerOverGameObject 不可靠),
///     空白滑屏也永远能转,不会被误判成「点在 UI 上」而整体失灵。
///   - 仅在游戏输入开启且战斗输入未被屏蔽时生效(背包/商店/设置等面板打开 → CombatEnabled=false → 不环视),
///     未能取到 InputService 时退化为「只要不点在 UI 上就能转」。
/// </summary>
public class CameraSwipeLook : MonoBehaviour
{
    [Tooltip("灵敏度:度/像素")]
    public float sensitivity = 0.15f;
    public float minPitch = -70f;
    public float maxPitch = 70f;
    [Tooltip("起点落在 UI(按钮)上时,需滑动超过该像素阈值才接管环视;空白处起手则立即生效")]
    public float uiDragThreshold = 30f;
    [Tooltip("临时诊断:在屏幕左上角显示实时输入/门控状态,定位真机滑屏不动问题。定位完删掉或关掉。")]
    public bool debugOverlay = true;

    // 当前驱动环视的指针:鼠标用 MouseFinger,触摸用真实 fingerId(>=0);NoFinger 表示空闲。
    private const int MouseFinger = -100;
    private const int NoFinger = int.MinValue;

    private InputService input;
    private UIMgr uiMgr; // 仅诊断浮层用:列出当前"显示中"的面板,定位是谁把 CombatEnabled 压成 false
    private float yaw;
    private float pitch;

    private int activeFinger = NoFinger;
    private Vector2 lastPos;
    private Vector2 beganPos;
    private bool startedOverUI; // 起手是否压在 UI 上(决定是否需要先滑过阈值)
    private bool activated;     // 是否已越过阈值、开始真正旋转

    // 诊断用:记录最近一次的输入观测,OnGUI 显示。
    private Vector2 lastDelta;
    private int beginCount;
    private int dragCount;

    private void Start()
    {
        if (GameLoop.Instance != null && GameLoop.Instance.Ctx != null)
        {
            input = GameLoop.Instance.Ctx.Get<InputService>();
            GameLoop.Instance.Ctx.TryGet<UIMgr>(out uiMgr);
        }

        var e = transform.eulerAngles;
        yaw = e.y;
        pitch = Normalize(e.x);
    }

    private void Update()
    {
        // 仅游戏中且战斗输入未屏蔽时允许环视(面板打开时 CombatInputGate 会置 CombatEnabled=false)
        bool allowed = input == null || (input.IsEnabled && input.CombatEnabled);
        if (!allowed) { End(); return; }

        if (Input.touchCount > 0) TickTouch();
        else TickMouse();
    }

    /// <summary>真机:直接按 fingerId 锁定一根手指,从它按下到抬起全程驱动环视。</summary>
    private void TickTouch()
    {
        // 空闲(或上一段是鼠标)时,挑第一根刚按下的手指接管。
        if (activeFinger == NoFinger || activeFinger == MouseFinger)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase != TouchPhase.Began) continue;
                Begin(t.fingerId, t.position);
                break;
            }
            return; // 起手帧不旋转
        }

        // 已锁定:找回这根手指,按其位移环视;抬起/取消则结束。
        for (int i = 0; i < Input.touchCount; i++)
        {
            var t = Input.GetTouch(i);
            if (t.fingerId != activeFinger) continue;
            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) { End(); return; }
            Drag(t.position);
            return;
        }
        End(); // 手指丢失(被系统吞掉)
    }

    /// <summary>编辑器/桌面:无触摸时走鼠标左键。</summary>
    private void TickMouse()
    {
        if (activeFinger != NoFinger && activeFinger != MouseFinger) End(); // 清掉触摸残留

        if (Input.GetMouseButtonDown(0)) Begin(MouseFinger, Input.mousePosition);
        else if (activeFinger == MouseFinger)
        {
            if (!Input.GetMouseButton(0)) End();
            else Drag(Input.mousePosition);
        }
    }

    private void Begin(int finger, Vector2 pos)
    {
        activeFinger = finger;
        beganPos = lastPos = pos;
        startedOverUI = IsOverUI(finger);
        activated = !startedOverUI; // 空白处起手立即环视;UI 上起手要等滑过阈值
        beginCount++;
    }

    private void Drag(Vector2 pos)
    {
        if (!activated)
        {
            // 起点在 UI 上:先攒位移,超过阈值才接管,以此区分「点按钮」与「从按钮甩出去环视」。
            if ((pos - beganPos).sqrMagnitude < uiDragThreshold * uiDragThreshold)
            {
                lastPos = pos; // 持续更新基准,避免越阈那帧产生跳变
                return;
            }
            activated = true;
            lastPos = pos;
        }

        Vector2 d = pos - lastPos;
        lastPos = pos;
        yaw += d.x * sensitivity;
        pitch = Mathf.Clamp(pitch - d.y * sensitivity, minPitch, maxPitch);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        lastDelta = d;
        dragCount++;
    }

    private void End()
    {
        activeFinger = NoFinger;
        activated = false;
    }

    private static float Normalize(float angle) => angle > 180f ? angle - 360f : angle;

    private bool IsOverUI(int finger)
    {
        var es = EventSystem.current;
        if (es == null) return false;
        return finger == MouseFinger ? es.IsPointerOverGameObject() : es.IsPointerOverGameObject(finger);
    }

    // ---------------- 临时诊断浮层 ----------------
    // 真机滑屏不动时,出一次包看这层:能看到触摸数/门控/是否进入拖拽/yaw 是否在变,直接定位卡点。
    // 定位完把 debugOverlay 关掉或删掉本段。
    private GUIStyle dbgStyle;

    private void OnGUI()
    {
        if (!debugOverlay) return;
        if (dbgStyle == null)
        {
            dbgStyle = new GUIStyle(GUI.skin.label) { fontSize = 34, normal = { textColor = Color.green } };
        }

        bool enabled = input != null && input.IsEnabled;
        bool combat = input != null && input.CombatEnabled;
        bool allowed = input == null || (enabled && combat);
        string mouse = $"mBtn0={Input.GetMouseButton(0)} mPos={Input.mousePosition.x:0},{Input.mousePosition.y:0}";

        string text =
            $"[SwipeLook]\n" +
            $"input={(input == null ? "NULL(退化:不点UI即可转)" : "ok")}  allowed={allowed}\n" +
            $"IsEnabled={enabled}  CombatEnabled={combat}\n" +
            $"touchCount={Input.touchCount}  {mouse}\n" +
            $"activeFinger={(activeFinger == NoFinger ? "-" : activeFinger.ToString())}  startedOverUI={startedOverUI}  activated={activated}\n" +
            $"beginCount={beginCount}  dragCount={dragCount}  lastDelta={lastDelta.x:0.0},{lastDelta.y:0.0}\n" +
            $"yaw={yaw:0.0}  pitch={pitch:0.0}\n" +
            $"shownPanels=[{ShownBlockingPanels()}]";

        GUI.Label(new Rect(20, 20, 1200, 500), text, dbgStyle);
    }

    /// <summary>列出当前处于 Shown 的非主界面面板——它们就是把 CombatEnabled 压成 false 的元凶。</summary>
    private string ShownBlockingPanels()
    {
        if (uiMgr == null) return "uiMgr=NULL";
        var sb = new StringBuilder();
        foreach (UIEnum e in Enum.GetValues(typeof(UIEnum)))
        {
            if (e == UIEnum.None || e == UIEnum.GameMainPanel) continue;
            if (uiMgr.IsShown(e))
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(e);
            }
        }
        return sb.Length == 0 ? "none" : sb.ToString();
    }
}