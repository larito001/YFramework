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
///
/// CombatEnabled = false 时只屏蔽“战斗相关”输入（移动/视角/滚轮/开火/瞄准/冲刺/蹲/跳/
/// 换弹/近战技能/选武器），但保留 UI/交互键（B 开关背包、E 交互、F 世界交互）——
/// 这样背包等面板打开时玩家仍能用 B 把它关掉。由 <see cref="CombatInputGate"/> 按 UI 状态驱动。
/// </summary>
public class InputService : IGameService, ITickable
{
    /// <summary>输入总开关。false 时连 UI/交互键一并屏蔽。</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>战斗输入闸门。false 时仅屏蔽战斗子集，UI/交互键仍有效（见类注释）。</summary>
    public bool CombatEnabled { get; set; } = true;

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
    /// <summary>世界交互键(默认 F):开宝箱、拾取等。与 <see cref="OnInteractDown"/>(E)分开。</summary>
    public event Action OnInteractWorldDown;
    /// <summary>打开/关闭背包键(默认 B)。</summary>
    public event Action OnToggleBagDown;
    public event Action OnMeleeDown;
    /// <summary>数字键 1~9 选择武器槽位，参数为 slot 索引（0..8）。</summary>
    public event Action<int> OnWeaponSelect;
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
    private const KeyCode KeyInteractWorld = KeyCode.F;
    private const KeyCode KeyToggleBag = KeyCode.B;
    private const KeyCode KeyMelee = KeyCode.V;
    private const int WeaponSlotCount = 9;

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
        OnInteractWorldDown = null;
        OnToggleBagDown = null;
        OnMeleeDown = null;
        OnWeaponSelect = null;
        OnScroll = null;
    }

    public void Tick(float dt)
    {
        if (!IsEnabled)
        {
            ZeroCombatState();
            // MousePosition 不归零，UI/瞄准在 disable 时仍可能需要读光标位置
            return;
        }

        // 光标位置任何时候都更新（UI 拖拽 / 瞄准都要读）。
        MousePosition = Input.mousePosition;

        // UI / 交互键不属于战斗输入：CombatEnabled=false（背包等面板打开）时仍然有效，
        // 否则背包打开后就没法用 B 关掉了。
        if (Input.GetKeyDown(KeyInteract)) OnInteractDown?.Invoke();
        if (Input.GetKeyDown(KeyInteractWorld)) OnInteractWorldDown?.Invoke();
        if (Input.GetKeyDown(KeyToggleBag)) OnToggleBagDown?.Invoke();

        if (!CombatEnabled)
        {
            ZeroCombatState();
            return;
        }

        Move = new Vector2(Input.GetAxisRaw(AxisMoveX), Input.GetAxisRaw(AxisMoveY));
        LookDelta = new Vector2(Input.GetAxis(AxisLookX), Input.GetAxis(AxisLookY)) * LookSensitivity;
        ScrollDelta = Input.GetAxis(AxisScroll);
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
        if (Input.GetKeyDown(KeyMelee)) OnMeleeDown?.Invoke();

        for (int i = 0; i < WeaponSlotCount; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                OnWeaponSelect?.Invoke(i);
        }
    }

    /// <summary>把所有“战斗相关”的连续值与持续按住状态清零（IsEnabled / CombatEnabled 关闭时共用）。</summary>
    private void ZeroCombatState()
    {
        Move = Vector2.zero;
        LookDelta = Vector2.zero;
        ScrollDelta = 0f;
        FireHeld = false;
        AimHeld = false;
        SprintHeld = false;
        CrouchHeld = false;
        JumpHeld = false;
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
