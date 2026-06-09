using System;
using System.Collections.Generic;
using UnityEngine;
using YOTO;

/// <summary>可重绑定的离散按键动作(鼠标开火/瞄准、移动/视角轴不在此列,仍走固定绑定)。
/// 只列已实现的功能——跳跃/下蹲/E 交互等当前没有对应玩法,不在此暴露。</summary>
public enum InputAction
{
    Sprint,
    Reload,
    InteractWorld,
    ToggleBag,
    Melee,
    Dodge,
}

/// <summary>
/// TPS 输入抽象层：把 Unity 的 Input.* 收敛到一处，玩法层只依赖事件 + 状态字段。
/// 后续要换 InputSystem 或加 Gamepad，改这一个文件，调用方不动。
///
/// 离散按键(见 <see cref="InputAction"/>)可在设置里重绑定：绑定表走 StoreMgr 的 Settings 分类持久化，
/// 改键即存盘并触发 <see cref="BindingsChanged"/>。
///
/// 用法：
///   连续值（移动/视角/滚轮）→ 直接读属性 Move / LookDelta / ScrollDelta
///   离散事件（开火/换弹/近战）→ 订阅 OnFireDown / OnReloadDown / OnMeleeDown ...
///   持续按住（瞄准/冲刺）→ 读 AimHeld / SprintHeld + 订阅 OnDown/OnUp 事件
///
/// IsEnabled = false 时 Move/LookDelta 归零、状态归 false、事件不再触发；默认 false，
/// 由 <see cref="InputSceneGate"/> 仅在游戏场景开启（菜单/启动界面屏蔽快捷键）。
///
/// CombatEnabled = false 时只屏蔽“战斗相关”输入（移动/视角/滚轮/开火/瞄准/冲刺/
/// 换弹/近战/选武器），但保留 UI/交互键（B 开关背包、F 世界交互）——
/// 这样背包等面板打开时玩家仍能用 B 把它关掉。由 <see cref="CombatInputGate"/> 按 UI 状态驱动。
/// </summary>
public class InputService : IGameService, ITickable
{
    /// <summary>输入总开关。false 时连 UI/交互键(背包/世界交互)一并屏蔽。
    /// 默认 false:只有进入游戏场景后由 <see cref="InputSceneGate"/> 打开,菜单/启动界面按 B 不会唤起背包。</summary>
    public bool IsEnabled { get; set; } = false;

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

    public event Action OnFireDown;
    public event Action OnFireUp;
    public event Action OnAimDown;
    public event Action OnAimUp;
    public event Action OnSprintDown;
    public event Action OnSprintUp;
    public event Action OnReloadDown;
    /// <summary>世界交互键(默认 F):开宝箱、拾取等。</summary>
    public event Action OnInteractWorldDown;
    /// <summary>打开/关闭背包键(默认 B)。</summary>
    public event Action OnToggleBagDown;
    public event Action OnMeleeDown;
    /// <summary>闪避键（默认空格）：按移动方向翻滚/闪身，带无敌帧。<see cref="DodgeComponent"/> 经 InputComponent 订阅。</summary>
    public event Action OnDodgeDown;
    /// <summary>数字键 1~9 选择武器槽位，参数为 slot 索引（0..8）。</summary>
    public event Action<int> OnWeaponSelect;
    public event Action<float> OnScroll;

    // 固定绑定(暂不开放重绑)：移动/视角轴 + 鼠标开火/瞄准。
    private const string AxisMoveX = "Horizontal";
    private const string AxisMoveY = "Vertical";
    private const string AxisLookX = "Mouse X";
    private const string AxisLookY = "Mouse Y";
    private const string AxisScroll = "Mouse ScrollWheel";

    private const int MouseFire = 0;
    private const int MouseAim = 1;
    private const int WeaponSlotCount = 9;

    /// <summary>各动作的默认键。重绑表缺省/重置时回退到这里。</summary>
    private static readonly Dictionary<InputAction, KeyCode> DefaultBindings = new Dictionary<InputAction, KeyCode>
    {
        { InputAction.Sprint, KeyCode.LeftShift },
        { InputAction.Reload, KeyCode.R },
        { InputAction.InteractWorld, KeyCode.F },
        { InputAction.ToggleBag, KeyCode.B },
        { InputAction.Melee, KeyCode.V },
        { InputAction.Dodge, KeyCode.Space },
    };

    private readonly Dictionary<InputAction, KeyCode> _bindings = new Dictionary<InputAction, KeyCode>(DefaultBindings);
    private ISaveHandle _bindingsHandle;

    /// <summary>按键绑定变化(重绑/重置)时触发,设置界面据此刷新显示。</summary>
    public event Action BindingsChanged;

    /// <summary>所有可重绑定的动作(展示顺序固定)。</summary>
    public static IReadOnlyList<InputAction> RebindableActions { get; } = new[]
    {
        InputAction.Sprint, InputAction.Reload, InputAction.InteractWorld, InputAction.ToggleBag, InputAction.Melee, InputAction.Dodge,
    };

    public KeyCode GetBinding(InputAction action) => _bindings.TryGetValue(action, out var k) ? k : DefaultBindings[action];

    /// <summary>
    /// 重绑某动作到指定键,立即生效并存盘。若该键已被别的动作占用,则与之**交换**
    /// (对方拿到本动作原来的键)——保证不出现重键,也不会把某个动作弄成无键。
    /// </summary>
    public void SetBinding(InputAction action, KeyCode key)
    {
        if (_bindings.TryGetValue(action, out var old) && old == key) return; // 没变

        foreach (var other in RebindableActions)
        {
            if (other != action && GetBinding(other) == key)
            {
                _bindings[other] = old; // 把本动作原来的键让给冲突动作(交换)
                break;
            }
        }

        _bindings[action] = key;
        _bindingsHandle?.Save();
        BindingsChanged?.Invoke();
    }

    /// <summary>恢复全部默认键。</summary>
    public void ResetBindings()
    {
        foreach (var kv in DefaultBindings) _bindings[kv.Key] = kv.Value;
        _bindingsHandle?.Save();
        BindingsChanged?.Invoke();
    }

    public void Init(GameContext ctx)
    {
        // 绑定表持久化:Settings 分类(全局,不随存档槽)。
        _bindingsHandle = ctx.Get<StoreMgr>().Register<KeyBindingData>("keybindings",
            CaptureBindings, RestoreBindings, SaveCategory.Settings);
        _bindingsHandle.Load();
    }

    private KeyBindingData CaptureBindings()
    {
        var data = new KeyBindingData();
        foreach (var kv in _bindings)
        {
            data.bindings.Add(new KeyBindingEntry { action = kv.Key.ToString(), key = (int)kv.Value });
        }
        return data;
    }

    private void RestoreBindings(KeyBindingData data)
    {
        // 先铺默认,再用存档覆盖已知动作(忽略未知/已删动作,保证向后兼容)。
        foreach (var kv in DefaultBindings) _bindings[kv.Key] = kv.Value;
        if (data?.bindings != null)
        {
            foreach (var e in data.bindings)
            {
                if (Enum.TryParse(e.action, out InputAction action)) _bindings[action] = (KeyCode)e.key;
            }
        }
        BindingsChanged?.Invoke();
    }

    public void Shutdown()
    {
        OnFireDown = null;
        OnFireUp = null;
        OnAimDown = null;
        OnAimUp = null;
        OnSprintDown = null;
        OnSprintUp = null;
        OnReloadDown = null;
        OnInteractWorldDown = null;
        OnToggleBagDown = null;
        OnMeleeDown = null;
        OnDodgeDown = null;
        OnWeaponSelect = null;
        OnScroll = null;
        BindingsChanged = null;
        _bindingsHandle = null;
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
        if (Input.GetKeyDown(GetBinding(InputAction.InteractWorld))) OnInteractWorldDown?.Invoke();
        if (Input.GetKeyDown(GetBinding(InputAction.ToggleBag))) OnToggleBagDown?.Invoke();

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

        var keySprint = GetBinding(InputAction.Sprint);
        SprintHeld = Input.GetKey(keySprint);
        if (Input.GetKeyDown(keySprint)) OnSprintDown?.Invoke();
        if (Input.GetKeyUp(keySprint)) OnSprintUp?.Invoke();

        if (Input.GetKeyDown(GetBinding(InputAction.Reload))) OnReloadDown?.Invoke();
        if (Input.GetKeyDown(GetBinding(InputAction.Melee))) OnMeleeDown?.Invoke();
        if (Input.GetKeyDown(GetBinding(InputAction.Dodge))) OnDodgeDown?.Invoke();

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
