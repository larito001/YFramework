using System;
using UnityEngine;

/// <summary>
/// TPS 输入抽象层：把 Unity 的 Input.* 收敛到一处，玩法层只依赖事件 + 状态字段。
/// 后续要换 InputSystem 或加 Gamepad，改这一个文件，调用方不动。
///
/// 用法：
///   连续值（移动/视角/滚轮）→ 直接读属性 Move / LookDelta / ScrollDelta
///   离散事件（开火/换弹/跳）→ 订阅 OnFireDown / OnReloadDown / OnJumpDown ...
///   持续按住（瞄准/冲刺/蹲）→ 读 AimHeld / SprintHeld / CrouchHeld + 订阅 OnDown/OnUp 事件
///
/// IsEnabled = false 时 Move/LookDelta 归零、状态归 false、事件不再触发；
/// 重新启用前正在按下的键，下次 GetKeyUp 仍会触发 Up 事件（Unity 输入层负责）。
/// </summary>
public class InputService : IGameService, ITickable
{
    public bool IsEnabled { get; set; } = true;

    /// <summary>鼠标视角整体灵敏度倍率，AimController 可在瞄准时再叠一层缩放。</summary>
    public float LookSensitivity { get; set; } = 1f;

    public Vector2 Move { get; private set; }
    public Vector2 LookDelta { get; private set; }
    public float ScrollDelta { get; private set; }
    /// <summary>鼠标屏幕坐标。光标锁定时 Unity 把它钉在屏幕中心，俯视角瞄准前先 SetCursorLocked(false)。</summary>
    public Vector2 MousePosition { get; private set; }

    public bool FireHeld { get; private set; }
    public bool AimHeld { get; private set; }
    public bool SprintHeld { get; private set; }
    public bool CrouchHeld { get; private set; }
    public bool JumpHeld { get; private set; }

    public event Action OnFireDown;
    public event Action OnFireUp;
    public event Action OnAimDown;
    public event Action OnAimUp;
    public event Action OnSprintDown;
    public event Action OnSprintUp;
    public event Action OnCrouchDown;
    public event Action OnCrouchUp;
    public event Action OnJumpDown;
    public event Action OnReloadDown;
    public event Action OnInteractDown;
    public event Action<float> OnScroll;

    // 按键绑定：后续接 Settings/Remap 时把这些挪到配置层
    private const string AxisMoveX = "Horizontal";
    private const string AxisMoveY = "Vertical";
    private const string AxisLookX = "Mouse X";
    private const string AxisLookY = "Mouse Y";
    private const string AxisScroll = "Mouse ScrollWheel";

    private const int MouseFire = 0;
    private const int MouseAim = 1;

    private const KeyCode KeySprint = KeyCode.LeftShift;
    private const KeyCode KeyCrouch = KeyCode.LeftControl;
    private const KeyCode KeyJump = KeyCode.Space;
    private const KeyCode KeyReload = KeyCode.R;
    private const KeyCode KeyInteract = KeyCode.E;

    public void Init(GameContext ctx)
    {
    }

    public void Shutdown()
    {
        OnFireDown = null;
        OnFireUp = null;
        OnAimDown = null;
        OnAimUp = null;
        OnSprintDown = null;
        OnSprintUp = null;
        OnCrouchDown = null;
        OnCrouchUp = null;
        OnJumpDown = null;
        OnReloadDown = null;
        OnInteractDown = null;
        OnScroll = null;
    }

    public void Tick(float dt)
    {
        if (!IsEnabled)
        {
            Move = Vector2.zero;
            LookDelta = Vector2.zero;
            ScrollDelta = 0f;
            // MousePosition 不归零，UI/瞄准在 disable 时仍可能需要读光标位置
            FireHeld = false;
            AimHeld = false;
            SprintHeld = false;
            CrouchHeld = false;
            JumpHeld = false;
            return;
        }

        Move = new Vector2(Input.GetAxisRaw(AxisMoveX), Input.GetAxisRaw(AxisMoveY));
        LookDelta = new Vector2(Input.GetAxis(AxisLookX), Input.GetAxis(AxisLookY)) * LookSensitivity;
        ScrollDelta = Input.GetAxis(AxisScroll);
        MousePosition = Input.mousePosition;
        if (ScrollDelta != 0f) OnScroll?.Invoke(ScrollDelta);

        FireHeld = Input.GetMouseButton(MouseFire);
        if (Input.GetMouseButtonDown(MouseFire)) OnFireDown?.Invoke();
        if (Input.GetMouseButtonUp(MouseFire)) OnFireUp?.Invoke();

        AimHeld = Input.GetMouseButton(MouseAim);
        if (Input.GetMouseButtonDown(MouseAim)) OnAimDown?.Invoke();
        if (Input.GetMouseButtonUp(MouseAim)) OnAimUp?.Invoke();

        SprintHeld = Input.GetKey(KeySprint);
        if (Input.GetKeyDown(KeySprint)) OnSprintDown?.Invoke();
        if (Input.GetKeyUp(KeySprint)) OnSprintUp?.Invoke();

        CrouchHeld = Input.GetKey(KeyCrouch);
        if (Input.GetKeyDown(KeyCrouch)) OnCrouchDown?.Invoke();
        if (Input.GetKeyUp(KeyCrouch)) OnCrouchUp?.Invoke();

        JumpHeld = Input.GetKey(KeyJump);
        if (Input.GetKeyDown(KeyJump)) OnJumpDown?.Invoke();

        if (Input.GetKeyDown(KeyReload)) OnReloadDown?.Invoke();
        if (Input.GetKeyDown(KeyInteract)) OnInteractDown?.Invoke();
    }

    /// <summary>
    /// 锁/解锁鼠标光标。进入战斗 SetCursorLocked(true)，开菜单/暂停时 SetCursorLocked(false)。
    /// </summary>
    public void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
